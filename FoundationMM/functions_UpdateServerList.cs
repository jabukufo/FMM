using Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Net.NetworkInformation;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Sockets;
using System.Globalization;
using System.Drawing;

namespace FoundationMM
{
    public partial class Window : Form
    {
        /// <summary>
        /// Hardcoded string array of master-server url's.
        /// dynamically instead.
        /// </summary>
        private string[] masterServers = new string[]
        {
            "http://eldewrito.red-m.net/list",
            "http://158.69.166.144:8080/list"
        };

        /// <summary>
        /// Downloads the .json from each master server in the masterServers list as a string, then serializes it into a MasterServer object.
        /// Downloads the .json from each server listed in each MasterServer object as a string while pinging the servers.
        /// Then serializes the server-info .jsons into HostServer objects, and adds them as ListViewItem's to "listView3"
        /// </summary>
        private async void UpdateServerList()
        {
        // This is used for downloading the server-info .jsons...
        HttpClient client = new HttpClient() { MaxResponseContentBufferSize = 1000000 };

            // List of the Task<string[]> from the output of ProcessURLAsync for each server, so that they can be awaited
            // in the "Add Servers To Listview" #Region.
            var serverTaskStrings = new List<Task<string[]>> { };

            // Using a WebClient object for downloading the MasterServer .jsons.
            using (var wc = new WebClient())
            {
                // TODO: Check if this is actually needed...
                wc.Proxy = WebRequest.DefaultWebProxy;

                // This is to prevent Red-M's master server from 403'ing.
                wc.Headers.Add("User-Agent: Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/55.0.2883.87 Safari/537.36");

                // Used for storing IP's of server's that have been added to "listView3" already, so that it can be checked
                // if a server has already been added before adding it potentially again.
                var serversAdded = new List<string> { };
                // Foreach "masterServer" url-string in the "masterServers" url array...
                foreach (string masterServer in masterServers)
                {
                    try
                    {
                        // Download the masterServer .json
                        string masterJson = wc.DownloadString(masterServer);
                        // Serialize the masterServer .json into a MasterServer object.
                        MasterServer master = JsonConvert.DeserializeObject<MasterServer>(masterJson);

                        // Foreach "server" url-string in the MasterServer object...
                        foreach (string server in master.result.servers)
                        {
                            // If the server has already been added to "serversAdded", don't doawnload the server-info json again.
                            if (!serversAdded.Contains(server))
                            {
                                // Add the server to "serversAdded" so that any following duplicate servers get filtered out.
                                serversAdded.Add(server);
                                // Download the server-info .json for the "server"
                                Task<string[]> download = ProcessURLAsync(server, client);
                                // Add the task to serverTaskStrings so that it can be awaited in the "Add Servers To Listview" #Region.
                                serverTaskStrings.Add(download);
                            }
                        }
                    }
                    catch { }
                }

                /// Awaits the tasks in "serverTaskStrings", serializes the .jsons from that output into "HostServer" objects,
                /// creates a ListViewItem from the properties of that HostServer, and then adds it to "listView3".
                #region Add Servers To Listview

                #region NOTE: Everthing inside this "#if DEBUG" is for adding a fake server to the server browser for debugging purposes.
#if DEBUG
                HostServer debugHost = new HostServer();
                debugHost.assassinationEnabled = "True";
                debugHost.eldewritoVersion = "0.5.1.1";
                debugHost.gameVersion = "1.106708_cert_ms23___release";
                debugHost.hostPlayer = "debugHostPlayer";
                debugHost.ipAddress = "debug.host.fake.ip";
                debugHost.isDedicated = true;
                debugHost.map = "debugMap";
                debugHost.mapFile = "guardian";
                debugHost.maxPlayers = 16;
                debugHost.mods.Add("tagmods\\Station\\Station.fm");
                debugHost.name = "Debug Fake Server";
                debugHost.numPlayers = 15;
                debugHost.passworded = "🔒";
                debugHost.ping = "-1 (fakeIP)";
                debugHost.port = 11774;
                debugHost.sprintEnabled = "true";
                debugHost.sprintUnlimitedEnabled = "true";
                debugHost.status = "inGame";
                debugHost.teams = true;
                debugHost.variant = "Debug BR's";
                debugHost.variantType = "slayer";
                debugHost.VoIP = true;
                debugHost.xnaddr = "n0t4r341xn4ddr355";
                debugHost.xnkid = "n0t4r341xnk1d";

                for (int i = 1; i <= debugHost.numPlayers; i++)
                {
                    Player player = new Player();
                    player.name = "player" + i;
                    player.score = i;
                    player.assists = i;
                    player.kills = i;
                    player.deaths = i;
                    player.isAlive = true;
                    player.uid = "n0t4r341u1d";

                    if (i <= debugHost.maxPlayers / 2)
                        player.team = 0;
                    else
                        player.team = 1;

                    debugHost.players.Add(player);
                }

                listView3.Invoke((MethodInvoker)delegate
                {
                    var item = new ListViewItem(new[] {
                                    debugHost.passworded, debugHost.name, debugHost.hostPlayer, debugHost.ping, debugHost.map, debugHost.variantType, debugHost.variant, debugHost.numPlayers.ToString() + '/' + debugHost.maxPlayers.ToString()
                                });
                    item.Tag = debugHost;

                    listView3.Items.Add(item);
                });
#endif
                #endregion

                // Every Task<string[]> in "serverTaskStrings" needs to be awaited, serialized, and then added to "listView3".
                foreach (Task<string[]> serverTaskString in serverTaskStrings)
                {
                    try
                    {
                        // Await the "serverTaskString", which returns a string[] containing: server-info json text, server IP, and Ping.
                        string[] server = await serverTaskString;
                        // Serialize the server-info .json at index [0] in the "server-string[]" into a HostServer object.
                        HostServer host = JsonConvert.DeserializeObject<HostServer>(server[0]);
                        // Set the "ipAddress" in the HostServer object to index [1] in the "server-string[]".
                        host.ipAddress = server[1].Substring(0, server[1].IndexOf(":"));

                        // Set the "ping" in the HostServer object to index [2] in the "server-string[]"... after doing some warfare on it.
                        // This is some weird math to calculate the "ping multiplier" based on the user's own connection
                        // instead of just using a flat 0.45 multiplier for everyone... If no server's responded to an
                        // actual ping request, the multiplier cannot be derived through this, so 0.45 is used instead.
                        if (pings != 0 && pingEsts != 0)
                            host.ping = ((int)(int.Parse(server[2]) * ((float)pings / pingEsts))).ToString();
                        else
                            host.ping = (int.Parse(server[2]) * 0.45).ToString();

                        // NOTE: only here for evaluation purposes during debuging/testing.
                        //host.hostPlayer = ((float)pings / pingEsts).ToString();

                        // "sprintEnabled" is marked as 0 for false and 1 for true in the server-info .json, so they are
                        // renamed to "False" and "True" so that it looks nicer in the server-list.
                        if (host.sprintEnabled == "0")
                            host.sprintEnabled = "False";
                        else if (host.sprintEnabled == "1")
                            host.sprintEnabled = "True";

                        // "assassinationEnabled" is marked as 0 for false and 1 for true in the server-info .json, so they are
                        // renamed to "False" and "True" so that it looks nicer in the server-list.
                        if (host.assassinationEnabled == "0")
                            host.assassinationEnabled = "False";
                        else if (host.assassinationEnabled == "1")
                            host.assassinationEnabled = "True";

                        // host.passworded is only in the server-info .json if it is true, other wise it is left blank due to
                        // the defaults of the HostServer class. If this is "true", it's set to 🔒 because unicode.
                        if (host.passworded.ToLower() == "true")
                            host.passworded = "🔒";

                        // Capitalize the first letter of the "variantType"
                        TextInfo myTI = new CultureInfo("en-US", false).TextInfo;
                        host.variantType = myTI.ToTitleCase(host.variantType);

                        // Invoke needs to be called here to access the control outside of the thread it was created on...
                        listView3.Invoke((MethodInvoker)delegate
                        {
                            // Create a new ListViewItem using the HostServer's properties.
                            var item = new ListViewItem(new[] {
                                    host.passworded, host.name, host.hostPlayer, host.ping, host.map, host.variantType, host.variant, host.numPlayers.ToString() + '/' + host.maxPlayers.ToString()
                                });
                            // Set the ListViewItem.Tag to the HostServer object, so that additional information that isn't
                            // being displayed in the listview can be easily looked up when a server is selected.
                            item.Tag = host;

                            listView3.Items.Add(item);
                        });

                        // Update the statusStrip at bottom left corner to show the amount of server's available.
                        if (enabledTab == 2)
                        {
                            int itemCount = listView3.Items.Count;
                            if (itemCount == 1) // If ONLY one server is available, use singular "server".
                            {
                                statusStrip1.Invoke((MethodInvoker)delegate { modNumberLabel.Text = "1 server available"; });
                            }
                            else // If MORE than one server is available, use plural "servers".
                            {
                                statusStrip1.Invoke((MethodInvoker)delegate { modNumberLabel.Text = itemCount + " servers available"; });
                            }
                        }
                    }
                    catch { continue; }
                }
                #endregion
            }
            client.Dispose();
        }

        // These are used to add successive pings into, to later be used in the multiplier derivation.
        // If either of these are empty, 0.45 will be used as the multiplier instead.
        public static long pings = 0;
        public static long pingEsts = 0;
        private async Task<string[]> ProcessURLAsync(string url, HttpClient client)
        {
            try
            {
                //Downloads the infoserver json (uses the download time for a rough ping estimate...)
                var serverString = await client.GetStringAsync($"http://{url}");

                // Intialize ping variable to "9999" so that if ping-estimation fails for a server, it gets sorted
                // to the bottom when sorting by ping..
                int ping = 9999;

                // Timing this to estimate a ping... Other server browsers use a 0.45 multiplier, and through
                // testing that seems like a pretty good one. This is innacurate for very slow connections though...
                // Not timing "GetStringAsync" above because the number is much higher... Because it is "async" maybe???
                // - really unsure on that... Server-Hosts really need to set up their machines to respond to
                // ping requests properly.
                try
                {
                    Stopwatch timer = new Stopwatch();
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"http://{url}");
                    timer.Start();
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    response.Close();
                    timer.Stop();
                    ping = (int)timer.ElapsedMilliseconds;
                }
                catch { }

                // If an actual ping request is successful, add both ping types to their totaller, so that they can
                // be used to derive an average ratio to use as a multiplier for calculating ping from the HTTP RTT
                Ping pingSender = new Ping();
                long reply = pingSender.Send(url.Replace(":11775", "")).RoundtripTime;
                if (reply != 0 && ping != 9999)
                {
                    pings += reply;
                    pingEsts += ping;
                }


                string[] serverArray = { serverString, url, ping.ToString() };
                return serverArray;
            }
            catch { return null; }
        }
    }

    public class Result
    {
        public int code { get; set; }
        public string msg { get; set; }
        public List<string> servers { get; set; }
    }
    public class MasterServer
    {
        public int listVersion { get; set; }
        public Result result { get; set; }
    }

    public class Player
    {
        // Initializing these to a value so that no errors related to null objects appear in ListView3 updating
        // this is a problem because some values are not parsed in the server info json if they are null, so they remain null.
        public string name { get; set; } = "";
        public int score { get; set; } = 0;
        public int kills { get; set; } = 0;
        public int assists { get; set; } = 0;
        public int deaths { get; set; } = 0;
        public int team { get; set; } = 0;
        public bool isAlive { get; set; } = false;
        public string uid { get; set; } = "";
    }
    public class HostServer
    {
        // Initializing these to a value so that no errors related to null objects appear in ListView3 updating
        // this is a problem because some values are not parsed in the server info json if they are null, so they remain null.
        public string name { get; set; } = "";
        public int port { get; set; } = 11775;
        public string hostPlayer { get; set; } = "";
        public bool isDedicated { get; set; } = false;
        public string sprintEnabled { get; set; } = "";
        public string sprintUnlimitedEnabled { get; set; } = "";
        public string assassinationEnabled { get; set; } = "";
        public bool VoIP { get; set; } = false;
        public bool teams { get; set; } = false;
        public string map { get; set; } = "";
        public string mapFile { get; set; } = "";
        public string variant { get; set; } = "";
        public string variantType { get; set; } = "";
        public string status { get; set; } = "";
        public int numPlayers { get; set; } = 0;
        public int maxPlayers { get; set; } = 0;
        public string xnkid { get; set; } = "";
        public string xnaddr { get; set; } = "";
        public List<Player> players { get; set; } = new List<Player> { new Player() };
        public List<string> mods { get; set; } = new List<string> { };
        public string gameVersion { get; set; } = "";
        public string eldewritoVersion { get; set; } = "";
        public string ipAddress { get; set; } = "";
        public string ping { get; set; } = "9999";
        public string passworded { get; set; } = "";
    }
}
