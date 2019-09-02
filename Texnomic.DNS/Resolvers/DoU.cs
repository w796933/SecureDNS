﻿using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Texnomic.DNS.Models;

namespace Texnomic.DNS.Resolvers
{
    public class DoU : IResolver
    {
        private IPEndPoint IPEndPoint;
        private readonly UdpClient Client;

        public DoU(IPAddress IPAddress)
        {
            IPEndPoint = new IPEndPoint(IPAddress, 53);
            Client = new UdpClient
            {
                Client =
                {
                    SendTimeout = 2000, 
                    ReceiveTimeout = 2000
                }
            };
        }

        public void Dispose()
        {
            Client.Dispose();
        }

        //private static byte[] PrependLength(byte[] Query)
        //{
        //    var Length = BitConverter.GetBytes((ushort)Query.Length);
        //    Array.Reverse(Length);
        //    Length[1] = (byte)(Length[1] >> 8);
        //    var Buffer = new byte[Query.Length + 2];
        //    Array.Copy(Length, Buffer, 2);
        //    Array.Copy(Query, 0, Buffer, 2, Query.Length);
        //    return Buffer;
        //}

        public byte[] Resolve(byte[] Query)
        {
            //Query = PrependLength(Query);

            Client.Send(Query, Query.Length, IPEndPoint);

            return Client.Receive(ref IPEndPoint);
        }

        public Message Resolve(Message Query)
        {
            var Buffer = Query.ToArray();

            //Buffer = PrependLength(Buffer);

            Client.Send(Buffer, Buffer.Length, IPEndPoint);

            Buffer = Client.Receive(ref IPEndPoint);

            return Message.FromArray(Buffer);
        }

        public async Task<byte[]> ResolveAsync(byte[] Query)
        {
            //Query = PrependLength(Query);

            await Client.SendAsync(Query, Query.Length, IPEndPoint);

            var Result = await Client.ReceiveAsync();

            return Result.Buffer;
        }

        public async Task<Message> ResolveAsync(Message Query)
        {
            var Buffer = Query.ToArray();

            //Buffer = PrependLength(Buffer);

            await Client.SendAsync(Buffer, Buffer.Length, IPEndPoint);

            var Result = await Client.ReceiveAsync();

            return Message.FromArray(Result.Buffer);
        }
    }
}
