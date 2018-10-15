using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using ProtoBuf;
//自定义
using ProtoMyData;
using ProtoMyRequest;
using ProtoMyResponse;

namespace protobuf_net
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
            TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.1"), 9527);
            server.Start();
            server.BeginAcceptTcpClient(clientConnected, server);
            Console.WriteLine("SERVER : 等待数据 ---");

            //启动客户端
            ThreadPool.QueueUserWorkItem(runClient);
            allDone.WaitOne();

            Console.WriteLine("SERVER : 退出 ---");
            server.Stop();
        }

        //服务端处理
        private static void clientConnected(IAsyncResult result)
        {
            try
            {
                TcpListener server = (TcpListener)result.AsyncState;
                using (TcpClient client = server.EndAcceptTcpClient(result))
                using (NetworkStream stream = client.GetStream())
                {
                    //获取
                    Console.WriteLine("SERVER : 客户端已连接，读取数据 ---");
                    //proto-buf 使用 Base128 Varints 编码
                    MyRequest myRequest = Serializer.DeserializeWithLengthPrefix<MyRequest>(stream, PrefixStyle.Base128);

                    //使用C# BinaryFormatter
                    IFormatter formatter = new BinaryFormatter();
                    MyData myData = (MyData)formatter.Deserialize(new MemoryStream(myRequest.data));
                    //MyData.MyData mydata = Serializer.DeserializeWithLengthPrefix<MyData.MyData>(new MemoryStream(request.data), PrefixStyle.Base128);

                    Console.WriteLine("SERVER : 获取成功, myRequest.version={0}, myRequest.name={1}, myRequest.website={2}, myData.resume={3}", myRequest.version, myRequest.name, myRequest.website, myData.resume);

                    //响应(MyResponse)
                    MyResponse myResponse = new MyResponse();
                    myResponse.version = myRequest.version;
                    myResponse.result = 99;
                    Serializer.SerializeWithLengthPrefix(stream, myResponse, PrefixStyle.Base128);
                    Console.WriteLine("SERVER : 响应成功 ---");

                    //DEBUG
                    //int final = stream.ReadByte();
                    //if (final == 123)
                    //{
                    //    Console.WriteLine("SERVER: Got client-happy marker");
                    //}
                    //else
                    //{
                    //    Console.WriteLine("SERVER: OOPS! Something went wrong");
                    //}
                    Console.WriteLine("SERVER: 关闭连接 ---");
                    stream.Close();
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
                MyData myData = new MyData();
                myData.resume = "我的个人简介";

                //构造MyRequest
                MyRequest myRequest = new MyRequest();
                myRequest.version = 1;
                myRequest.name = "吴剑";
                myRequest.website = "www.paotiao.com";

                //使用C# BinaryFormatter
                using (MemoryStream ms = new MemoryStream())
                {
                    IFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(ms, myData);
                    //Serializer.Serialize(ms, mydata);
                    myRequest.data = ms.GetBuffer();
                    ms.Close();
                }
                Console.WriteLine("CLIENT : 对象构造完毕 ...");

                using (TcpClient client = new TcpClient())
                {
                    client.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9527));
                    Console.WriteLine("CLIENT : socket 连接成功 ...");

                    using (NetworkStream stream = client.GetStream())
                    {
                        //发送
                        Console.WriteLine("CLIENT : 发送数据 ...");
                        ProtoBuf.Serializer.SerializeWithLengthPrefix(stream, myRequest, PrefixStyle.Base128);

                        //接收
                        Console.WriteLine("CLIENT : 等待响应 ...");
                        MyResponse myResponse = ProtoBuf.Serializer.DeserializeWithLengthPrefix<MyResponse>(stream, PrefixStyle.Base128);

                        Console.WriteLine("CLIENT : 成功获取结果, version={0}, result={1}", myResponse.version, myResponse.result);

                        //DEBUG client-happy marker
                        //stream.WriteByte(123);

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