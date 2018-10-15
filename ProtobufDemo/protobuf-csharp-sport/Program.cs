using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Google.ProtocolBuffers;

namespace protobuf_csharp_sport
{
    class Program
    {
        private static ManualResetEvent allDone = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            beginDemo();
        }

        private static void beginDemo()
        {
            //启动服务端
            TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.1"), 9528);
            server.Start();
            server.BeginAcceptTcpClient(clientConnected, server);
            Console.WriteLine("SERVER : 等待数据 ---");

            //启动客户端
            ThreadPool.QueueUserWorkItem(runClient);
            allDone.WaitOne();

            Console.WriteLine("SERVER : 退出 ---");
            server.Stop();
            Console.ReadLine();
        }

        //服务端处理
        private static void clientConnected(IAsyncResult result)
        {
            try
            {
                TcpListener server = (TcpListener)result.AsyncState;
                using (TcpClient client = server.EndAcceptTcpClient(result))
                {
                    using (NetworkStream stream = client.GetStream())
                    {
                        //获取
                        Console.WriteLine("SERVER : 客户端已连接，数据读取中 --- ");
                        byte[] myRequestBuffer = new byte[49];
                        int myRequestLength = 0;
                        do
                        {
                            myRequestLength = stream.Read(myRequestBuffer, 0, myRequestBuffer.Length);
                        }
                        while (stream.DataAvailable);
                        MyRequest myRequest = MyRequest.ParseFrom(myRequestBuffer);
                        MyData myData = MyData.ParseFrom(myRequest.Data);
                        Console.WriteLine("SERVER : 获取成功, myRequest.Version={0}, myRequest.Name={1}, myRequest.Website={2}, myData.Resume={3}", myRequest.Version, myRequest.Name, myRequest.Website, myData.Resume);

                        //响应(MyResponse)
                        MyResponse.Builder myResponseBuilder = MyResponse.CreateBuilder();
                        myResponseBuilder.Version = myRequest.Version;
                        myResponseBuilder.Result = 99;
                        MyResponse myResponse = myResponseBuilder.Build();
                        myResponse.WriteTo(stream);
                        Console.WriteLine("SERVER : 响应成功 ---");

                        Console.WriteLine("SERVER: 关闭连接 ---");
                        stream.Close();                        
                    }
                    client.Close();
                }
            }
            finally
            {
                allDone.Set();
            }
        }

        //客户端请求
        private static void runClient(object state)
        {
            try
            {
                //构造MyData
                MyData.Builder myDataBuilder = MyData.CreateBuilder();
                myDataBuilder.Resume = "我的个人简介";
                MyData myData = myDataBuilder.Build();
                
                //构造MyRequest
                MyRequest.Builder myRequestBuilder = MyRequest.CreateBuilder();
                myRequestBuilder.Version = 1;
                myRequestBuilder.Name = "吴剑";
                myRequestBuilder.Website = "www.paotiao.com";
                //注：直接支持ByteString类型
                myRequestBuilder.Data = myData.ToByteString();
                MyRequest myRequest = myRequestBuilder.Build();
                                
                Console.WriteLine("CLIENT : 对象构造完毕 ...");

                using (TcpClient client = new TcpClient())
                {
                    client.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9528));
                    Console.WriteLine("CLIENT : socket 连接成功 ...");

                    using (NetworkStream stream = client.GetStream())
                    {
                        //发送
                        Console.WriteLine("CLIENT : 发送数据 ...");
                        myRequest.WriteTo(stream);

                        //接收
                        Console.WriteLine("CLIENT : 等待响应 ...");
                        byte[] myResponseBuffer = new byte[4];
                        int myResponseLength = 0;
                        do
                        {
                            myResponseLength = stream.Read(myResponseBuffer, 0, myResponseBuffer.Length);
                        }
                        while (stream.DataAvailable);                        
                        MyResponse myResponse = MyResponse.ParseFrom(myResponseBuffer);
                        Console.WriteLine("CLIENT : 成功获取结果, myResponse.Version={0}, myResponse.Result={1}", myResponse.Version, myResponse.Result);

                        //关闭
                        stream.Close();
                    }
                    client.Close();
                    Console.WriteLine("CLIENT : 关闭 ...");
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("CLIENT ERROR : {0}", error.ToString());
            }
        }

    }//end class
}
