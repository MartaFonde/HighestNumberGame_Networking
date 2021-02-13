using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using System.Threading;

namespace HighestNumberGame
{
    class Server
    {
        static readonly internal object l = new object();   //en cada acceso a clients --> recurso común

        private static int port = 31416;

        private static List<Client> clients;

        private static List<int> numbers = new List<int>();
        private static Random n = new Random();

        private static DateTime endTime;
        const int TIMEPLAY = 15;
        private static int seg = TIMEPLAY;

        private static bool end = false;


        private static void SendMessageToAllClients(string msg)     //envía a todos los clients desde server
        {
            if (msg != null)
            {
                lock (l)
                {
                    for (int i = 0; i < clients.Count; i++)
                    {
                        try
                        {
                            using (NetworkStream ns = new NetworkStream(clients.ElementAt(i).S))
                            using (StreamReader sr = new StreamReader(ns))
                            using (StreamWriter sw = new StreamWriter(ns))
                            {
                                sw.WriteLine(msg);
                                sw.Flush();
                            }
                        }
                        catch (IOException)
                        {
                            DisconnectClient(clients[i]);
                            Console.WriteLine("Error sending the message to all clients: " + msg);
                        }                                                                
                    }              
                }
            }
        }

        private static void SendMessageToClient(Client c, string msg)     //envía a un cliente desde server
        {
            if (msg != null )
            {
                lock (l)
                {
                    try
                    {
                        using (NetworkStream ns = new NetworkStream(c.S))
                        using (StreamReader sr = new StreamReader(ns))
                        using (StreamWriter sw = new StreamWriter(ns))
                        {
                            sw.WriteLine(msg);
                            sw.Flush();
                        }
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Error sending the message to client");
                        DisconnectClient(c);
                    }
                }                
            }
        }

        internal static int RandomNumber(Client c)
        {
            bool repeat = true;
            bool numAsig = false;
            int num = 1;
            while (repeat)
            {
                num = n.Next(1, 21);
                lock (l)
                {
                    if (!numbers.Contains(num))
                    {
                        repeat = false;
                        numbers.Add(num);
                        numAsig = true;
                    }
                    if (numAsig)
                    {
                        SendMessageToClient(c, "Your number is: " + num);
                    }
                }
            }            
            return num;
        }

        internal static void DisconnectClient(Client c)
        {
            lock (l)
            {
                c.S.Close();
                numbers.Remove(c.num);
                clients.Remove(c);
            }
        }

        static void CheckNumbersWinner()
        {
            int number = 1;
            
            //lock (l)  xa está detro dun lock
            if(clients.Count > 0)
            {
                Client winner = clients[0];

                for (int i = 0; i < clients.Count; i++)
                {
                    SendMessageToClient(clients[i], "Your number is: " + clients[i].num);
                    if (clients[i].num >= number)
                    {
                        winner = clients[i];
                        number = clients[i].num;                    
                    }
                }

                for (int i = 0; i < clients.Count; i++)
                {
                    IPEndPoint ieReceptor = (IPEndPoint)clients[i].S.RemoteEndPoint;
                    if (ieReceptor.Port != winner.Port)
                    {
                        SendMessageToClient(clients[i], "The winner is number: " + winner.num);
                    }                    
                }
                SendMessageToClient(winner, "Congratulations! You are the winner");
            }
        }

        private static void EndGame()
        {
            //lock (l) xa se chama dentro dun lock
            if(clients.Count > 0)
            {
                for (int i = 0; i < clients.Count; i++)
                {
                    clients[i].S.Close();
                }

                //reinicio para nueva partida
                clients.Clear();
                numbers.Clear();

                seg = TIMEPLAY;
                //end = false;
            }
        }

        private static void StartMark()
        {
            SendMessageToAllClients("--- START ---");
            endTime = DateTime.Now.AddSeconds(seg + 1);

            while (!end)
            {
                lock (l)
                {
                    if (!end)
                    {                        
                        DateTime now = DateTime.Now;
                        TimeSpan dif = endTime - now;

                        if (dif.Milliseconds % 1000 == 0 && dif.Seconds == seg)
                        {
                            string time = string.Format($"{dif:mm\\:ss}");
                            SendMessageToAllClients(time);
                            seg--;
                        }

                        if (dif.Seconds == 0)
                        {
                            string time = string.Format($"{dif:mm\\:ss}");
                            SendMessageToAllClients(time);

                            SendMessageToAllClients("--- END ---");
                            CheckNumbersWinner();
                            EndGame();
                        }                        
                    }
                }
            }
        }

        static void Main(string[] args)
        {

            bool portFree = false;
            IPEndPoint ie = new IPEndPoint(IPAddress.Any, port);

            using (Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                while (!portFree)
                {
                    try
                    {
                        server.Bind(ie);
                        server.Listen(6);
                        portFree = true;

                        Console.WriteLine("Server waiting at port {0}", ie.Port);

                        clients = new List<Client>();

                        while (true)
                        {
                            Socket sClient = server.Accept();

                            lock (l)
                            {
                                clients.Add(new Client(sClient));  //lanzamos hilo/cliente

                                if (clients.Count == 2)
                                {
                                    Thread t = new Thread(StartMark);
                                    t.Start();                                    
                                }                                                                                                                             
                            }
                        }
                    }
                    catch (SocketException e) when (e.ErrorCode == (int)SocketError.AddressAlreadyInUse)
                    {
                        Console.WriteLine($"Port {port} in use");
                        portFree = false;
                        port++;
                    }
                    catch (SocketException e)
                    {
                        Console.WriteLine("Error: " + e.Message);
                    }
                }
            }
        }        
    }
}
