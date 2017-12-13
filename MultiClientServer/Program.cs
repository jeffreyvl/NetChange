using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MultiClientServer
{
    internal class Program
    {
        public static int MijnPoort;

        public static SortedDictionary<int, Connection> Buren = new SortedDictionary<int, Connection>();
        public static Dictionary<int, Tuple<int, int>> PreferredNeighbour = new Dictionary<int, Tuple<int, int>>();

        public static object BLocked = new object();
        public static object PrefNbLocked = new object();

        private static void Main(string[] args)
        {
            MijnPoort = int.Parse(args[0]);
            var mijnServer = new Server(MijnPoort);
            PreferredNeighbour.Add(MijnPoort, Tuple.Create(0, 0));
            for (var i = 1; i < args.Length; i++)
            {
                var poort = int.Parse(args[i]);
                if (poort > MijnPoort)
                    continue;
                Connect(poort);
            }

            Console.Title = "Netchange " + MijnPoort;

            while (true)
            {
                var input = Console.ReadLine().Trim();
                var split = input.Split(' ');
                int poort;
                switch (split[0])
                {
                    case "R":
                        // Routing table
                        lock (PrefNbLocked)
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
                        {
                            Console.WriteLine("Hier is geen verbinding naar!");
                        }
                        else
                        {
                            var bericht = string.Join(" ", split.Skip(2));
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
                        lock (BLocked)
                        {
                            if (Buren.ContainsKey(dPoort))
                                Buren[dPoort].Write.Close();
                            else
                                Console.WriteLine($"Geen verbinding met {dPoort}");
                        }
                        break;
                }
            }
        }

        public static void Connect(int poort)
        {
            var connected = false;
            Connection con = null;
            lock (BLocked)
            {
                if (Buren.ContainsKey(poort))
                {
                    Console.WriteLine("Hier is al verbinding naar!");
                }
                else
                {
                    while (!connected)
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
                    UpdatePreferredNeighbour(poort);
                    Console.WriteLine($"Verbonden: {poort}");
                    lock (PrefNbLocked)
                    {
                        SendRoutingTable(con);
                    }
                }
            }
        }

        public static void SendRoutingTable()
        {
            for (var j = 0; j < PreferredNeighbour.Count; j++)
            {
                var k = PreferredNeighbour.ElementAt(j);
                for (var i = 0; i < Buren.Count; i++)
                    Buren.ElementAt(i).Value.Write.WriteLine($"UpdateRoutingTable {k.Key} {k.Value.Item1}");
            }
        }

        public static void SendRoutingTable(Connection b)
        {
            for (var j = 0; j < PreferredNeighbour.Count; j++)
            {
                var k = PreferredNeighbour.ElementAt(j);
                b.Write.WriteLine($"UpdateRoutingTable {k.Key} {k.Value.Item1}");
            }
        }

        public static void SendRoutingTable(int node)
        {
            var k = PreferredNeighbour[node];
            for (var i = 0; i < Buren.Count; i++)
                Buren.ElementAt(i).Value.Write.WriteLine($"UpdateRoutingTable {node} {k.Item1}");
        }

        public static void SendRoutingTable(int node, int dis)
        {
            for (var i = 0; i < Buren.Count; i++)
                Buren.ElementAt(i).Value.Write.WriteLine($"UpdateRoutingTable {node} {dis}");
        }

        public static void UpdatePreferredNeighbour(int node)
        {
            if (node == MijnPoort)
                return;
            var minDis = 100;
            var prefNb = 0;
            lock (BLocked)
            {
                foreach (var b in Buren)
                    lock (b.Value.RtLocked)
                    {
                        if (b.Value.RoutingTable.ContainsKey(node) && b.Value.RoutingTable[node] < minDis)
                        {
                            minDis = b.Value.RoutingTable[node];
                            prefNb = b.Key;
                        }
                    }

                var tup = Tuple.Create(minDis + 1, prefNb);
                lock (PrefNbLocked)
                {
                    if (minDis < PreferredNeighbour.Count)
                    {
                        if (!PreferredNeighbour.ContainsKey(node))
                        {
                            PreferredNeighbour.Add(node, tup);
                            Console.WriteLine($"Afstand naar {node} is nu {minDis + 1} via {prefNb}");
                            SendRoutingTable(node);
                        }
                        else if (!tup.Equals(PreferredNeighbour[node]))
                        {
                            PreferredNeighbour[node] = tup;
                            Console.WriteLine($"Afstand naar {node} is nu {minDis + 1} via {prefNb}");
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