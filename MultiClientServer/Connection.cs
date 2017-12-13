using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace MultiClientServer
{
    internal class Connection
    {
        public int Poort;
        public StreamReader Read;
        public Dictionary<int, int> RoutingTable = new Dictionary<int, int>();
        public object RtLocked = new object();
        public StreamWriter Write;

        // Connection heeft 2 constructoren: deze constructor wordt gebruikt als wij CLIENT worden bij een andere SERVER
        public Connection(int port)
        {
            Poort = port;
            var client = new TcpClient("localhost", port);
            Read = new StreamReader(client.GetStream());
            Write = new StreamWriter(client.GetStream()) {AutoFlush = true};


            // De server kan niet zien van welke poort wij client zijn, dit moeten we apart laten weten
            Write.WriteLine("Poort: " + Program.MijnPoort);

            Console.WriteLine($"Verbonden: {port}");
            lock (Program.PrefNbLocked)
            {
                Program.SendRoutingTable(this);
            }
            // Start het reader-loopje
            new Thread(ReaderThread).Start();
        }

        // Deze constructor wordt gebruikt als wij SERVER zijn en een CLIENT maakt met ons verbinding
        public Connection(StreamReader read, StreamWriter write, int port)
        {
            Read = read;
            Write = write;
            Poort = port;
            Console.WriteLine($"Verbonden: {port}");
            lock (Program.PrefNbLocked)
            {
                Program.SendRoutingTable(this);
            }
            // Start het reader-loopje
            new Thread(ReaderThread).Start();
        }

        // LET OP: Nadat er verbinding is gelegd, kun je vergeten wie er client/server is (en dat kun je aan het Connection-object dus ook niet zien!)

        // Deze loop leest wat er binnenkomt en print dit
        public void ReaderThread()
        {
            try
            {
                while (true)
                {
                    var s = Read.ReadLine();
                    var split = s.Split(' ');

                    if (split[0] == "UpdateRoutingTable")
                    {
                        UpdateRoutingTable(int.Parse(split[1]), int.Parse(split[2]));
                    }
                    else if (split[0] == "Message")
                    {
                        var recip = int.Parse(split[1]);
                        if (recip == Program.MijnPoort)
                        {
                            Console.WriteLine(string.Join(" ", split.Skip(2)));
                        }
                        else
                        {
                            var sendTo = Program.PreferredNeighbour[recip].Item2;
                            Program.Buren[sendTo].Write.WriteLine(s);
                            Console.WriteLine($"Bericht voor {recip} doorgestuurd naar {sendTo}");
                        }
                    }
                    else
                    {
                        Console.WriteLine(Read.ReadLine());
                    }
                }
            }
            catch
            {
                Console.WriteLine($"Verbroken: {Poort}");
                lock (Program.BLocked)
                {
                    Program.Buren.Remove(Poort);
                }

                var nodes = new List<int>();
                lock (Program.PrefNbLocked)
                {
                    nodes.AddRange(Program.PreferredNeighbour.Keys);
                }

                var length = nodes.Count;
                for (var i = 0; i < length; i++)
                    Program.UpdatePreferredNeighbour(nodes[i]);
            } // Verbinding is kennelijk verbroken
        }

        public void Message(int to, string bericht)
        {
            Write.WriteLine($"Message {to} {bericht}");
        }

        public void UpdateRoutingTable(int node, int dis)
        {
            if (node == Program.MijnPoort) return;
            lock (RtLocked)
            {
                if (!RoutingTable.ContainsKey(node))
                    RoutingTable.Add(node, dis);
                else
                    RoutingTable[node] = dis;
            }
            Program.UpdatePreferredNeighbour(node);
        }
    }
}