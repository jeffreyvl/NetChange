using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace MultiClientServer
{
    class Server
    {
        public Server(int port)
        {
            // Luister op de opgegeven poort naar verbindingen
            TcpListener server = new TcpListener(IPAddress.Any, port);
            server.Start();

            // Start een aparte thread op die verbindingen aanneemt
            new Thread(() => AcceptLoop(server)).Start();
        }

        private void AcceptLoop(TcpListener handle)
        {
            while (true)
            {
                TcpClient client = handle.AcceptTcpClient();
                StreamReader clientIn = new StreamReader(client.GetStream());
                StreamWriter clientOut = new StreamWriter(client.GetStream());
                clientOut.AutoFlush = true;

                // De server weet niet wat de poort is van de client die verbinding maakt, de client geeft dus als onderdeel van het protocol als eerst een bericht met zijn poort
                if (clientIn.ReadLine().Split(' ')[0] == "Poort:")
                {
                    int zijnPoort = int.Parse(clientIn.ReadLine().Split(' ')[1]);
                    lock (Program.bLocked)
                    {
                        if (!Program.Buren.ContainsKey(zijnPoort) && zijnPoort != Program.MijnPoort)
                        {

                            Console.WriteLine("Verbonden: " + zijnPoort);

                            // Zet de nieuwe verbinding in de verbindingslijst
                            Connection con = new Connection(clientIn, clientOut, zijnPoort);
                            Program.Buren.Add(zijnPoort, con);
                            Program.UpdatePreferredNeighbour(zijnPoort);
                            lock (Program.prefNBLocked)
                                Program.SendRoutingTable(con);

                        }
                    }
                }
            }
        }
    }
}
