using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HighestNumberGame
{   
    class Client
    {
        internal Socket S { set; get; }
        IPEndPoint Ie { set; get; }
        internal IPAddress Ip { set; get; }
        internal int Port { set; get; }
        internal string UserName { set; get; }

        internal bool connected = false;

        internal int num = 1;

        public Client(Socket socket)
        {
            S = socket;

            Ie = (IPEndPoint)socket.RemoteEndPoint;
            Port = Ie.Port;

            String localHost = Dns.GetHostName();
            InfoHostClient(localHost);

            Thread t = new Thread(Chat);
            t.Start();
        }

        private void InfoHostClient(string name)
        {
            IPHostEntry hostInfo;
            hostInfo = Dns.GetHostEntry(name);
            foreach (IPAddress ip in hostInfo.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    Ip = ip;
                }
            }
        }

        private void Chat()
        {
            string msg = null;
            bool sendMsgDisconnect = false;
            try
            {
                using (NetworkStream ns = new NetworkStream(S))
                using (StreamReader sr = new StreamReader(ns))
                using (StreamWriter sw = new StreamWriter(ns))
                {
                    string welcome = "Welcome. You will receive a number between 1-20. If your number is the highest, you win. Let the game begin!";
                    sw.WriteLine(welcome);
                    sw.Flush();

                    while (!connected)
                    {
                        sw.WriteLine("Introduce your name");
                        sw.Flush();
                        msg = sr.ReadLine();

                        if (msg != null && msg.Trim().Length > 0)
                        {
                            UserName = msg;
                            string userNew = string.Format("Connected with client {0} at port {1}", UserName, Port);
                            Server.SendMessageFromClientToAllClients(this, userNew);
                            connected = true;
                            sw.WriteLine("Connected: " + connected);
                            num = Server.RandomNumber(this);
                        }
                    }

                    while (connected)
                    {
                        sendMsgDisconnect = true;
                        sr.ReadLine();
                    }
                }
            }
            catch (IOException)
            {
                EndConnection(sendMsgDisconnect);
            }
        }

        private void EndConnection(bool msg)
        {
            if (msg)
            {
                Server.SendMessageFromClientToAllClients(this, UserName + " disconnected ");
            }

            Server.DisconnectClient(this);      //borra de List clients & num de List numbers
            S.Close();
            connected = false;            
        }
    }
}
