using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRPA.Interfaces;
using ProtoBuf;
using System.Windows.Documents;
using System.IO.Pipelines;

namespace OpenRPA.proto3
{
    [ProtoContract]
    public class AwesomeMessage
    {
        public AwesomeMessage() { }
        public AwesomeMessage(string command) { this.command = command; }
        public AwesomeMessage(string command, byte[] data) { this.command = command; this.data = data; }
        [ProtoMember(1)]
        public string command { get; set; }
        [ProtoMember(2)]
        public byte[] data { get; set; }
    }
    public static class IPCServer
    {
        public static event Action<AwesomeMessage> onAwesomeMessage;
        public static byte[] Take_Byte_Arr_From_Int(long Source_Num)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(Source_Num >> 24);
            bytes[1] = (byte)(Source_Num >> 16);
            bytes[2] = (byte)(Source_Num >> 8);
            bytes[3] = (byte)Source_Num;
            return bytes;

            //Int64 Int64_Num = Source_Num;
            //byte Byte_Num;
            //byte[] Byte_Arr = new byte[8];
            //for (int i = 0; i < 8; i++)
            //{
            //    if (Source_Num > 255)
            //    {
            //        Int64_Num = Source_Num / 256;
            //        Byte_Num = (byte)(Source_Num - Int64_Num * 256);
            //    }
            //    else
            //    {
            //        Byte_Num = (byte)Int64_Num;
            //        Int64_Num = 0;
            //    }
            //    Byte_Arr[i] = Byte_Num;
            //    Source_Num = Int64_Num;
            //}
            //return (Byte_Arr);
        }

        public static void Send(AwesomeMessage message)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.Serialize(memoryStream, message);
                var byteArray = memoryStream.ToArray();
                server.Write(Take_Byte_Arr_From_Int(byteArray.Length), 0, 4);
                server.Write(byteArray, 0, byteArray.Length);
            }

        }
        private static AwesomeMessage ReadMessage(PipeStream stream)
        {
            if (stream == null || !stream.IsConnected) return null;
            using (var ms = new MemoryStream())
            {
                var sizebuf = new byte[4];
                stream.Read(sizebuf, 0, sizebuf.Length);
                // convert sizebuf to UInt32
                // var size = BitConverter.ToUInt32(sizebuf.Reverse().ToArray(), 0);
                var size = (int)BitConverter.ToUInt32(sizebuf, 0);
                if (size > 0)
                {
                    var buffer = new byte[size];
                    stream.Read(buffer, 0, size);
                    var myPerson = Serializer.Deserialize<AwesomeMessage>(new ReadOnlySpan<byte>(buffer));
                    return myPerson;
                    //var buffer = new byte[size];
                    //var readBytes = stream.Read(buffer, 0, (int)size);
                    //var myPerson = Serializer.Deserialize<AwesomeMessage>(new ReadOnlySpan<byte>(buffer));
                    //return myPerson;
                }
                //using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                //{
                //    // Get size as UInt32 / writeUInt32LE in nodejs
                //    var size = reader.ReadUInt32();
                //    var buffer = new byte[size];
                //    var readBytes = stream.Read(buffer, 0, (int)size);
                //    ms.Write(buffer, 0, readBytes);
                //}
                //ms.Position = 0;
                //var myPerson = Serializer.Deserialize<AwesomeMessage>(ms);
                //return myPerson;
            }
            return null;
        }
        private static NamedPipeServerStream server;
        private static Queue<AwesomeMessage> messageQueue = new Queue<AwesomeMessage>();
        private static bool processing = false;
        public static void Start()
        {
            // compile the model - to avoid using reflection, for better speed
            Serializer.PrepareSerializer<AwesomeMessage>();
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {

                        using (server = new NamedPipeServerStream("testpipe", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte))
                        {
                            server.WaitForConnection();
                            while (true)
                            {
                                var msg = ReadMessage(server);
                                if (msg == null) continue;
                                messageQueue.Enqueue(msg);
                                if(!processing)
                                {
                                    Task.Run(() => {
                                        processing = true;
                                        do
                                        {
                                            var m = messageQueue.Dequeue();
                                            onAwesomeMessage(m);
                                        } while (messageQueue.Count > 0);
                                        processing = false;
                                    });
                                }
                                proto3.IPCServer.Send(new AwesomeMessage(msg.command));
                                //// Console.WriteLine("Message received from client: " + Encoding.UTF8.GetString(messageBytes));
                                //Console.WriteLine("Message received from client: " + msg.awesome_field);

                                //var response = Encoding.UTF8.GetBytes((++cnt).ToString() + " Did you say " + msg.awesome_field + "?");
                                //server.Write(response, 0, response.Length);

                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.ToString());
                    }
                }
            });
        }
    }
}
