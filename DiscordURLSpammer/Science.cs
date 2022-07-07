using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordURLSpammer
{
    public class Science
    {
        private Random random = new Random();
        private int eventNum = 0;
        private ulong userID = 0;

        public Science(ulong userID)
        {
            this.userID = userID;
        }

        public string client_track_timestamp()
        {
            return Convert.ToString(timeStamp() * 1000);
        }

        public string client_send_timestamp()
        {
            return Convert.ToString((timeStamp() * 1000) + random.Next(40, 1000));
        }

        public string client_uuid()
        {
            List<byte> payload = new List<byte>();

            double multipy = 4294967296;
            double maxify = 2147483647;
            double randomPrefix = multipy * random.NextDouble();
            long creationTime = timeStamp() * 1000;

            payload.AddRange(INT2LE(userID % multipy <= maxify ? userID % multipy : userID % multipy - maxify));
            payload.AddRange(INT2LE(userID >> 32));
            payload.AddRange(INT2LE(randomPrefix <= maxify ? randomPrefix : randomPrefix - multipy));
            payload.AddRange(INT2LE(creationTime % multipy <= maxify ? creationTime % multipy : creationTime % multipy - maxify));
            payload.AddRange(INT2LE(creationTime >> 32));
            payload.AddRange(INT2LE(eventNum));

            ++eventNum;
            return Convert.ToBase64String(payload.ToArray());
        }

        private long timeStamp()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        private byte[] INT2LE(double data)
        {
            byte[] b = new byte[4];
            b[0] = (byte)data;
            b[1] = (byte)(((uint)data >> 8) & 0xFF);
            b[2] = (byte)(((uint)data >> 16) & 0xFF);
            b[3] = (byte)(((uint)data >> 24) & 0xFF);
            return b;
        }

    }
}
