using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;

namespace MultiClientServer
{
    class Program
    {
        static public int MijnPoort;

        static public SortedDictionary<int, Connection> Buren = new SortedDictionary<int, Connection>();
        static public Dictionary<int, Tuple<int, int>> PreferredNeighbour = new Dictionary<int, Tuple<int, int>>();

        static public object bLocked = new object();
        static public object prefNBLocked = new object();

        static void Main(string[] args)
        {
            string[] split;
            MijnPoort = int.Parse(args[0]);
            Server MijnServer = new Server(MijnPoort);
            PreferredNeighbour.Add(MijnPoort, Tuple.Create(0, 0));
            for (var i = 1; i < args.Length; i++)
            {
                int poort = int.Parse(args[i]);
                if (poort > MijnPoort)
                    continue;
                Connect(poort);
            }

            Console.Title = "Netchange " + MijnPoort;

            while (true)
            {
                string input = Console.ReadLine().Trim();
                split = input.Split(' ');
                int poort;
                switch (split[0])
                {
                    case "R":
                        // Routing table
                        lock (prefNBLocked)
                        {
                            foreach (var node in PreferredNeighbour)
                            {
                                var nearest = node.Value.Item2 == 0 ? "local" : node.Value.Item2.ToString();
                                if (node.Value.Item1 < 20)
                                    Console.WriteLine(node.Key + " " + node.Value.Item1 + " " + nearest);
                            }
                        }
                        break;
                    case "B":
                        // Send message

                        poort = int.Parse(split[1]);
                        if (!PreferredNeighbour.ContainsKey(poort))
                            Console.WriteLine("Hier is geen verbinding naar!");
                        else
                        {
                            string bericht = string.Join(" ", split.Skip(2));
                            Buren[PreferredNeighbour[poort].Item2].Message(poort, bericht);
                        }

                        break;
                    case "C":
                        // Create connection
                        poort = int.Parse(split[1]);
                        Connect(poort);
                        break;
                    case "D":
                        // Break connection
                        var dPoort = int.Parse(split[1]);
                        lock(bLocked)
                        {
                            if (Buren.ContainsKey(dPoort))
                                Buren[dPoort].Write.Close();
                            else
                                Console.WriteLine($"Geen verbinding met {dPoort}");
                        }
                        break;
                    default:
                        break;
                }

            }
        }

        static void Connect(int poort)
        {
            bool connected = false;
            Connection con = null;
            lock (bLocked)
            {
                if (Buren.ContainsKey(poort))
                    Console.WriteLine("Hier is al verbinding naar!");
                else
                {
                    while (!connected)
                    {
                        try
                        {
                            con = new Connection(poort);
                            Buren.Add(poort, con);
                            connected = true;
                        }
                        catch
                        {
                            Thread.Sleep(10);
                        }
                    }
                    UpdatePreferredNeighbour(poort);
                    Console.WriteLine($"Verbonden: {poort}");
                    lock (prefNBLocked)
                         SendRoutingTable(con);
                }
            }
        }

        public static void SendRoutingTable()
        {
            for (int j = 0; j < PreferredNeighbour.Count; j++)
            {
                var k = PreferredNeighbour.ElementAt(j);
                for (int i = 0; i < Buren.Count; i++)
                    Buren.ElementAt(i).Value.Write.WriteLine($"UpdateRoutingTable {k.Key} {k.Value.Item1}");
            }
        }

        public static void SendRoutingTable(Connection b)
        {
            for (int j = 0; j < PreferredNeighbour.Count; j++)
            {
                var k = PreferredNeighbour.ElementAt(j);
                b.Write.WriteLine($"UpdateRoutingTable {k.Key} {k.Value.Item1}");
            }
        }

        public static void SendRoutingTable(int node)
        {
            var k = PreferredNeighbour[node];
            for (int i = 0; i < Buren.Count; i++)
                Buren.ElementAt(i).Value.Write.WriteLine($"UpdateRoutingTable {node} {k.Item1}");

        }

        public static void SendRoutingTable(int node, int dis)
        {
            for (int i = 0; i < Buren.Count; i++)
                Buren.ElementAt(i).Value.Write.WriteLine($"UpdateRoutingTable {node} {dis}");
        }

        public static void UpdatePreferredNeighbour(int node)
        {
            if (node == MijnPoort)
                return;
            int minDis = 100;
            int prefNB = 0;
            lock (bLocked)
            {
                foreach (var b in Buren)
                    lock (b.Value.rtLocked)
                        if (b.Value.RoutingTable.ContainsKey(node) && b.Value.RoutingTable[node] < minDis)
                        {
                            minDis = b.Value.RoutingTable[node];
                            prefNB = b.Key;
                        }

                var tup = Tuple.Create(minDis + 1, prefNB);
                lock (prefNBLocked)
                {
                    if (minDis < PreferredNeighbour.Count)
                    {
                        if (!PreferredNeighbour.ContainsKey(node))
                        {
                            PreferredNeighbour.Add(node, tup);
                            Console.WriteLine($"Afstand naar {node} is nu {minDis + 1} via {prefNB}");
                            SendRoutingTable(node);
                        }
                        else if (!tup.Equals(PreferredNeighbour[node]))
                        {
                            PreferredNeighbour[node] = tup;
                            Console.WriteLine($"Afstand naar {node} is nu {minDis + 1} via {prefNB}");
                            SendRoutingTable(node);
                        }
                    }
                    else
                    {
                        if (PreferredNeighbour.ContainsKey(node))
                        {
                            SendRoutingTable(node, minDis + 1);
                            PreferredNeighbour.Remove(node);
                        }
                        Console.WriteLine($"Onbereikbaar: {node}");
                    }
                }
            }
        }
    }
}


