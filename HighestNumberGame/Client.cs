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
        static readonly internal object m = new object();   //en cada acceso a clients --> recurso común

        internal Socket S { set; get; }
        IPEndPoint Ie { set; get; }
        internal int Port { set; get; }

        internal bool connected = true;
        internal int num = 1;

        public Client(Socket socket)
        {
            S = socket;

            Ie = (IPEndPoint)socket.RemoteEndPoint;
            Port = Ie.Port;

            Thread t = new Thread(Chat);
            t.Start();
        }

        private void Chat()
        {
            try
            {
                using (NetworkStream ns = new NetworkStream(S))
                using (StreamReader sr = new StreamReader(ns))
                using (StreamWriter sw = new StreamWriter(ns))
                {
                    string welcome = "Welcome. You will receive a number between 1-20. If your number is the highest, you win. Let the game begin!";
                    sw.WriteLine(welcome);
                    sw.Flush();

                    num = Server.RandomNumber(this);

                    lock (Server.l)
                    {
                        Monitor.Pulse(Server.l);
                    }                    

                    while (connected)
                    {                        
                        lock (m)
                        {
                            Monitor.Wait(m);
                        }
                    }
                }
            }
            catch (IOException)
            {
                EndConnection();
            }
        }

        private void EndConnection()
        {
            Server.DisconnectClient(this);      //borra de List clients & num de List numbers
        }
    }
}
