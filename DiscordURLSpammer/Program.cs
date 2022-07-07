using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading; 
using System.Threading.Tasks;

namespace DiscordURLSpammer
{
    class Program
    {
        public static string guild = "";

        public static async Task Main(string[] args)
        {
            Console.Write("Sunucu ID");
            guild = Console.ReadLine();
            Console.Write("Token : ");
            string token = Console.ReadLine();

            while (true)
            {
                await DiscordSocket.startWebSocket(token);
                DiscordSocket.Dispose();
            }

            Console.ReadLine();
        }
    }
}
