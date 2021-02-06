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
        private static int gamers = 4;

        private static List<Client> clients;

        private static List<int> numbers = new List<int>();
        private static Random n = new Random();

        private static DateTime endTime;
        private static int seg = 60;
        private static System.Timers.Timer timer;

        private static bool end = false;
        private static bool start = false;

        internal static bool SendMessageFromClientToAllClients(Client c, string msg)     //envía desde un client al resto de clients
        {            
            if (msg != null)
            {
                Console.WriteLine("From: "+c.UserName+" - Message: "+msg);
                lock (l)
                {
                    for (int i = 0; i < clients.Count; i++)
                    {
                        IPEndPoint ieReceptor = (IPEndPoint)clients[i].S.RemoteEndPoint;
                        if (ieReceptor.Port != c.Port && clients[i].connected)
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
                                Console.WriteLine("Error sending the message from client "+c.UserName+" to all clients: " + msg);
                            }
                        }
                    }
                    return true;
                }                                                                   
            }
            return false;
        }

        private static bool SendMessageToAllClients(string msg)     //envía a todos los clients desde server
        {
            if (msg != null) 
            {
                Console.WriteLine("From server to all clients: "+msg);
                lock (l)
                {
                    for (int i = 0; i < clients.Count; i++)
                    {
                        if (clients[i].connected)
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
                                Console.WriteLine("Error sending the message to all clients: "+msg);
                            }
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        private static bool SendMessageToClient(Client c, string msg)     //envía a un cliente desde server
        {
            if (msg != null && c.connected)
            {
                Console.WriteLine("From server to client: "+c.UserName+"- Msg: "+msg);
                try
                {
                    using (NetworkStream ns = new NetworkStream(c.S))
                    using (StreamReader sr = new StreamReader(ns))
                    using (StreamWriter sw = new StreamWriter(ns))
                    {
                        sw.WriteLine(msg);
                        sw.Flush();
                        return true;
                    }
                }
                catch (IOException)
                {
                    Console.WriteLine("Error sending the message to client "+c.UserName+" : " + msg);
                }
            }                                                
            return false;
        }

        internal static int RandomNumber(Client c)
        {
            bool repeat = true;
            bool numAsig = false;
            int num = -1;
            while (repeat)
            {
                num = n.Next(1, 5);
                lock (l)
                {
                    if (!numbers.Contains(num))
                    {
                        repeat = false;
                        numbers.Add(num);
                        numAsig = true;
                    }
                }  
            }
            if (numAsig)
            {
                SendMessageToClient(c, "Your number is: " + num);
            }
            return num;                     
        }

        internal static void DisconnectClient(Client c)
        {
            lock (l)
            {
                numbers.Remove(c.num);
                clients.Remove(c);                
            }
        }
        
        private static void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            if (!end)
            {
                TimeSpan dif = endTime - DateTime.Now;

                string time = string.Format($"{dif:mm\\:ss}");
                SendMessageToAllClients(time);

                if (dif.Seconds == 0)
                {
                    Console.WriteLine("--- END ---");
                    SendMessageToAllClients("--- END ---");
                    timer.Stop();
                    end = true;
                    CheckNumbersWinner();
                }                
            }            
        }

        static  void CheckNumbersWinner()
        {
            int number = 1;
            Client winner = clients[0];

            lock (l)
            {
                for (int i = 0; i < clients.Count; i++)
                {
                    SendMessageToClient(clients[i], "Your number is: " + clients[i].num);
                    if(clients[i].num >= number)
                    {
                        winner = clients[i];
                        number = clients[i].num;
                    }
                }

                for (int i = 0; i < clients.Count; i++)
                {
                    IPEndPoint ieReceptor = (IPEndPoint)clients[i].S.RemoteEndPoint;
                    if (ieReceptor.Port != winner.Port && clients[i].connected)
                    {
                        SendMessageToClient(clients[i], "The winner is " + winner.UserName + " --- Number: " + winner.num);
                    }
                    else if (ieReceptor.Port == winner.Port)
                    {
                        SendMessageToClient(winner, "Congratulations! You are the winner);
                    }
                }
                EndGame();
            }
        }

        private static void EndGame()
        {
            lock (l)
            {
                for (int i = 0; i < clients.Count; i++)
                {
                    clients[i].S.Close();
                    clients[i].connected = false;
                }
                clients.Clear();
                numbers.Clear();
            }            
        }

        private static void StartMark()
        {
            while (!start)
            {
                bool allConnected = true;

                lock (l)
                {
                    for (int i = 0; i < clients.Count; i++)
                    {
                        if (!clients[i].connected)
                        {
                            allConnected = false;
                            break;
                        }
                    }
                }                

                if (allConnected)
                {
                    start = true;
                    SendMessageToAllClients("--- START ---");

                    endTime = DateTime.Now.AddSeconds(seg);
                    timer = new System.Timers.Timer(1000);
                    timer.Elapsed += OnTimedEvent;
                    timer.Start();
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
                        server.Listen(gamers);
                        portFree = true;

                        Console.WriteLine("Server waiting at port {0}", ie.Port);

                        clients = new List<Client>();

                        while (true)
                        {
                            Socket sClient = server.Accept();
                            lock (l)
                            {
                                if(clients.Count <= gamers)
                                {
                                    clients.Add(new Client(sClient));  //lanzamos hilo/cliente
                                    Console.WriteLine("Connected clients: "+clients.Count);

                                    if (clients.Count == gamers)
                                    {
                                        Thread starting = new Thread(StartMark);
                                        starting.Start();
                                    }
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
