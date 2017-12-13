using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MultiClientServer
{
    internal class Server
    {
        public Server(int port)
        {
            // Luister op de opgegeven poort naar verbindingen
            var server = new TcpListener(IPAddress.Any, port);
            server.Start();

            // Start een aparte thread op die verbindingen aanneemt
            new Thread(() => AcceptLoop(server)).Start();
        }

        private static void AcceptLoop(TcpListener handle)
        {
            while (true)
            {
                var client = handle.AcceptTcpClient();
                var clientIn = new StreamReader(client.GetStream());
                var clientOut = new StreamWriter(client.GetStream()) {AutoFlush = true};

                // De server weet niet wat de poort is van de client die verbinding maakt, de client geeft dus als onderdeel van het protocol als eerst een bericht met zijn poort
                if (clientIn.ReadLine().Split(' ')[0] == "Poort:")
                {
                    var zijnPoort = int.Parse(clientIn.ReadLine().Split(' ')[1]);
                    lock (Program.BLocked)
                    {
                        if (!Program.Buren.ContainsKey(zijnPoort) && zijnPoort != Program.MijnPoort)
                        {
                            Console.WriteLine("Verbonden: " + zijnPoort);

                            // Zet de nieuwe verbinding in de verbindingslijst
                            var con = new Connection(clientIn, clientOut, zijnPoort);
                            Program.Buren.Add(zijnPoort, con);
                            Program.UpdatePreferredNeighbour(zijnPoort);
                            lock (Program.PrefNbLocked)
                            {
                                Program.SendRoutingTable(con);
                            }
                        }
                    }
                }
            }
        }
    }
}