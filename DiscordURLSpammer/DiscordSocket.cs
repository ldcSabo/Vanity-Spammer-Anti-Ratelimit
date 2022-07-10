using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordURLSpammer
{
    public class DiscordSocket
    {
        private static ClientWebSocket socket = new ClientWebSocket();
        private static CancellationTokenSource cancelsource = new CancellationTokenSource();
        private static CancellationToken cancel = cancelsource.Token;
        private static long sequence = 0;
        private static bool ack = false;

        public static async Task<string> startWebSocket()
        {
            string analytic_token = "NULL";

            await socket.ConnectAsync(new Uri("wss://gateway.discord.gg/?encoding=json&v=9&compress=zlib-stream"), cancel);

            MemoryStream _compressed = new MemoryStream();
            DeflateStream _decompressor = new DeflateStream(_compressed, CompressionMode.Decompress);

            while (!cancel.IsCancellationRequested)
            {
                var buffer = new ArraySegment<byte>(new byte[16 * 1024]);
                WebSocketReceiveResult socketResult = await socket.ReceiveAsync(buffer, cancel);

                byte[] result;
                int resultCount;

                if (socketResult.MessageType == WebSocketMessageType.Close)
                    throw new Exception(socketResult.CloseStatus + " - " + socketResult.CloseStatusDescription);

                if (!socketResult.EndOfMessage)
                {
                    using (var stream = new MemoryStream())
                    {
                        stream.Write(buffer.Array, 0, socketResult.Count);
                        do
                        {
                            if (cancel.IsCancellationRequested) return "CANCELLED";
                            socketResult = await socket.ReceiveAsync(buffer, cancel).ConfigureAwait(false);
                            stream.Write(buffer.Array, 0, socketResult.Count);
                        }
                        while (socketResult == null || !socketResult.EndOfMessage);

                        resultCount = (int)stream.Length;

                        result = stream.GetBuffer();
                    }
                }
                else
                {
                    resultCount = socketResult.Count;
                    result = buffer.Array;
                }

                if (socketResult.MessageType == WebSocketMessageType.Text)
                {
                    string text = Encoding.UTF8.GetString(result, 0, resultCount);
                    Console.WriteLine("Text message : " + text);
                }
                else
                {
                    var data = result;
                    var index = 0;
                    var count = resultCount;

                    using (var decompressed = new MemoryStream())
                    {
                        if (data[0] == 0x78)
                        {
                            _compressed.Write(data, index + 2, count - 2);
                            _compressed.SetLength(count - 2);
                        }
                        else
                        {
                            _compressed.Write(data, index, count);
                            _compressed.SetLength(count);
                        }

                        _compressed.Position = 0;
                        _decompressor.CopyTo(decompressed);
                        _compressed.Position = 0;
                        decompressed.Position = 0;

                        using (var reader = new StreamReader(decompressed))
                        {
                            string strdata = await reader.ReadToEndAsync();

                            JObject jsonObject = JsonConvert.DeserializeObject<JObject>(strdata);

                            int op = jsonObject["op"].ToObject<int>();
                            if (!jsonObject["s"].ToString().Equals(""))
                            {
                                sequence = jsonObject["s"].ToObject<long>();
                            }

                            if (op == 10)
                            {
                                byte[] dataGoing = createSocketBytes(2, createIdentify(Program.token));

                                await SendSocketAsync(dataGoing, 0, dataGoing.Length, false);
                            }

                            await processMessage(jsonObject, op, sequence);

                        }
                    }
                }
            }

            return analytic_token;
        }

        public static void Dispose()
        {
            socket = new ClientWebSocket();
            cancelsource = new CancellationTokenSource();
            cancel = cancelsource.Token;
        }

        public static async Task processMessage(JObject jsonObject, int op, long sequence)
        {
            if (jsonObject.ContainsKey("t") && jsonObject["t"].ToString().Equals("GUILD_UPDATE"))
            {
                string vanity = jsonObject["d"].Value<JObject>()["vanity_url_code"].Value<string>();

                HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.Add("Authorization", Program.token);

                HttpRequestMessage message = new HttpRequestMessage(new HttpMethod("PATCH"), "https://discord.com/api/v9/guilds/" + Program.guild + "/vanity-url");

                message.Content = new StringContent("{\"code\":\"" + Program.code + "\"}", Encoding.UTF8, "application/json");
                
                await client.SendAsync(message);
            }

            if (jsonObject.ContainsKey("t") && jsonObject["t"].ToString().Equals(""))
            {
                if (op == 11)
                {
                    Console.WriteLine("ACK");
                    ack = true;
                }
                if (op == 10)
                {
                    int interval = jsonObject["d"].Value<JObject>()["heartbeat_interval"].Value<int>();

                    if (interval == 0)
                    {
                        interval = 10000;
                    }

                    new Thread(() =>
                    {
                        var heartbeatTimer = new System.Timers.Timer();
                        heartbeatTimer.Interval = interval;
                        heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
                        heartbeatTimer.Start();
                        ack = true;
                    }).Start();
                }
            }

            if (op == 1)
            {
                //heartbeat

                Console.WriteLine(jsonObject);
            }

            if (op == 6)
            {
                Console.WriteLine(jsonObject);

                //resume
            }

            if (op == 7)
            {
                Console.WriteLine(jsonObject);

                //reconnect
            }

            if (op == 9)
            {
                Console.WriteLine(jsonObject);

                //invalid sess (reocnnect)
                cancelsource.Cancel();

            }
        }

        private static async void HeartbeatTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (ack && sequence != 0)
            {
                JObject payload = new JObject();
                payload["op"] = 1;
                payload["d"] = sequence;

                byte[] patch = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));

                await SendSocketAsync(patch, 0, patch.Length, false);

                ack = false;
            }
            else
            {
                Console.WriteLine("reconnect");
            }
        }

        private static byte[] createSocketBytes(int op, JObject data)
        {
            JObject obj = new JObject();

            obj.Add("op", op);
            obj.Add("d", data);

            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));
        }

        private static async Task SendSocketAsync(byte[] data, int index, int count, bool isText)
        {
            var SendChunkSize = 4 * 1024;

            int frameCount = (int)Math.Ceiling((double)count / SendChunkSize);

            for (int i = 0; i < frameCount; i++, index += SendChunkSize)
            {
                bool isLast = i == (frameCount - 1);

                int frameSize;
                if (isLast)
                    frameSize = count - (i * SendChunkSize);
                else
                    frameSize = SendChunkSize;

                var type = isText ? WebSocketMessageType.Text : WebSocketMessageType.Binary;
                await socket.SendAsync(new ArraySegment<byte>(data, index, count), type, isLast, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static JObject createIdentify(string token)
        {
            JObject prop = new JObject();

            prop.Add("os", "Android");
            prop.Add("browser", "Firefox");
            prop.Add("device", "");
            prop.Add("system_locale", "tr-TR");
            prop.Add("browser_user_agent", "Mozilla/5.0 (Android 12; Mobile; rv:68.0) Gecko/68.0 Firefox/102.0");
            prop.Add("browser_version", "102");
            prop.Add("os_version", "9");
            prop.Add("referrer", "");
            prop.Add("referring_domain", "");
            prop.Add("referrer_current", "");
            prop.Add("referring_domain_current", "");
            prop.Add("release_channel", "stable");
            prop.Add("client_build_number", 134842);
            prop.Add("client_event_source", null);

            JObject presence = new JObject();

            presence.Add("status", "Online");
            presence.Add("since", 0);//can change
            presence.Add("activities", new JArray());
            presence.Add("afk", false);

            JObject state = new JObject();

            state.Add("guild_hashes", new JObject());//all can change
            state.Add("highest_last_message_id", "0");
            state.Add("read_state_version", 0);
            state.Add("user_guild_settings_version", -1);
            state.Add("user_settings_version", -1);

            JObject obj = new JObject();
            obj.Add("token", token);
            obj.Add("capabilites", 509);
            obj.Add("properties", prop);
            obj.Add("presence", presence);
            obj.Add("compress", false);
            obj.Add("client_status", state);

            return obj;
        }

    }
}
