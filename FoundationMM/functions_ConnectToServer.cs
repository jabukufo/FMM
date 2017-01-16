using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using WebSocketSharp;
using Microsoft.VisualBasic;

namespace FoundationMM
{
    public partial class Window : Form
    {
        /// <summary>
        /// Updates then displays the "ServerInfo" panel below the server list.
        /// </summary>
        private void ServerInfo()
        {
            // Enable connect button first, then disable it if the game is full. 
            // NOTE: Since this method gets called everytime a server is selected, enabling/disabling the connect button fits well here.
            button8.Enabled = true;
            if (globalHost.numPlayers >= globalHost.maxPlayers)
                button8.Enabled = false;

            // Set groupBox6.Text to the server-name for the current globalHost (globalHost is set equal to the selected item's .Tag)
            groupBox6.Text = globalHost.name;

            // Clears each player list.
            listView4.Items.Clear();
            listView5.Items.Clear();

            // Hides each player list's enclosing groupBox.
            groupBox8.Visible = false;
            groupBox9.Visible = false;

            // Iff the server has "Teams" enabled (AKA not a FFA)...
            if (globalHost.teams)
            {
                // Each player's score will get added to these - total is displayed as the team's score.
                // NOTE: DOES NOT REMAIN ACCURATE IF PLAYERS LEAVE.
                int redScore = 0, blueScore = 0;

                // The "HostServer" type has a "players" field of type "List<Player>"
                foreach (Player player in globalHost.players)
                {
                    // This is used to skip over "ghost/non-existent" players.
                    if (player.name == string.Empty)
                        continue;

                    // If the player is on Red-Team...
                    if (player.team == 0)
                    {
                        // Add their score to the Red-Team's score.
                        redScore += player.score;

                        // Create a new ListViewItem with the player's Name, Score, Kills, Deaths, and Assists.
                        var item = new ListViewItem(new[] {
                            player.name, player.score.ToString(), player.kills.ToString(), player.deaths.ToString(), player.assists.ToString()
                            });
                        // Add the player object to the new ListViewItem.Tag
                        item.Tag = player;

                        // Finally add the player's ListViewItem to the Blue-Team's player list.
                        listView4.Items.Add(item);
                    }

                    // If the player is on Blue-Team...
                    if (player.team == 1)
                    {
                        // Add their score to the Blue-Team's score.
                        blueScore += player.score;

                        // Create a new ListViewItem with the player's Name, Score, Kills, Deaths, and Assists.
                        var item = new ListViewItem(new[] {
                            player.name, player.score.ToString(), player.kills.ToString(), player.deaths.ToString(), player.assists.ToString()
                            });
                        // Add the player object to the new ListViewItem.Tag
                        item.Tag = player;

                        // Finally add the player's ListViewItem to the Blue-Team's player list.
                        listView5.Items.Add(item);
                    }
                }

                // Set the player-list's enclosing groupBoxes to correspond to the teams and their total scores.
                groupBox8.Text = "Red Team - " + redScore.ToString();
                groupBox9.Text = "Blue Team - " + blueScore.ToString();
            }

            // Else (meaning Teams.Enabled == False), the game is a FFA match.
            else
            {
                // The score of the highest player in the game.
                int highestScore = 0;

                // The "HostServer" type has a "players" field of type "List<Player>"
                foreach (Player player in globalHost.players)
                {
                    // This is used to skip over "ghost/non-existent" players.
                    if (player.name == string.Empty)
                        continue;

                    // If the player's score is higher than any previous player's checked (or 0 if none have been checked)
                    // then they have the highest current score.
                    if (player.score > highestScore)
                        highestScore = player.score;

                    // Since teams are not enabled, I'm using the "Blue-Team" player list as overflow for players, if there are more than 8 players.
                    if (listView4.Items.Count < 8)
                    {
                        // Create a new ListViewItem with the player's Name, Score, Kills, Deaths, and Assists.
                        var item = new ListViewItem(new[] {
                            player.name, player.score.ToString(), player.kills.ToString(), player.deaths.ToString(), player.assists.ToString()
                                });
                        // Add the player object to the new ListViewItem.Tag
                        item.Tag = player;

                        // Finally add the player's ListViewItem to the playerlist... Using the "Red-Team"'s playerlist for the first 8 players.
                        listView4.Items.Add(item);
                    }
                    else
                    {
                        // Create a new ListViewItem with the player's Name, Score, Kills, Deaths, and Assists.
                        var item = new ListViewItem(new[] {
                            player.name, player.score.ToString(), player.kills.ToString(), player.deaths.ToString(), player.assists.ToString()
                            });
                        // Add the player object to the new ListViewItem.Tag
                        item.Tag = player;

                        // Finally add the player's ListViewItem to the playerlist... Using the "Blue-Team"'s playerlist for the last 8 players.
                        listView5.Items.Add(item);
                    }
                }

                // Setting the "Red-Team" playerlist groupbox text to "FFA" and the highest score out of all the players.
                // && Setting the "Blue-Team" playerlist groupbox text to empty.
                groupBox8.Text = "FFA - " + highestScore.ToString();
                groupBox9.Text = string.Empty;
            }

            // Displaying various additional information about the server in the RichTextBox that is on the Right side of the
            // Server-Info Panel.
            richTextBox2.Text =
                //"Server: " + globalHost.name + "\n\n" +
                "Host: " + globalHost.hostPlayer + "\n\n" +
                "VoIP: " + globalHost.VoIP + "\n\n" +
                "Sprint: " + globalHost.sprintEnabled + "\n\n" +
                "Ass.: " + globalHost.assassinationEnabled + "\n\n" +
                "Status: " + globalHost.status + "\n\n" +
                "Version: " + globalHost.eldewritoVersion;

            // Only displaying the player-lists if there are players added to it (so that FFA games that have less than 9 players
            // Don't unecessarily draw a completely empty ListView, for example).
            if (listView4.Items.Count != 0)
                groupBox8.Visible = true;
            if (listView5.Items.Count != 0)
                groupBox9.Visible = true;

            // Finally, make the Server-Info panel visible.
            panel7.Visible = true;
        }

        /// <summary>
        /// If the selected server requires a password, it prompts for user input,
        /// then attempts to connect to the server through DewRcon.
        /// If ElDewrito is not running, the DewRcon method will try to launch it...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void ConnectToServer()
        {
            globalPassword = textBox2.Text;

            // Kill background ED processes - This is necessary, because otherwise ED is detected as "Running"
            // and does not get launched by the DewRcon method.
            try
            {
                Process[] EDProcesses = Process.GetProcessesByName("eldorado");
                if (EDProcesses.Count() > 0)
                {
                    foreach (var EDprocess in EDProcesses)
                    {
                        // Background processes don't have a MainWindow handle, so this can be used to identify if the ED
                        // process is running in the background.
                        if (EDprocess.MainWindowHandle == null || EDprocess.MainWindowHandle == (IntPtr)0)
                        {
                            EDprocess.Kill();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return;
            }

            // DewRcon launches ED if it detects no running processes, and then sends a "connect <ip> <password>" command
            // to the game through DewRcon.
            await DewRcon($"connect {globalHost.ipAddress} {globalPassword}");
        }

        /// <summary>
        /// Tries to connect to DewRcon at "ws://127.0.0.1:11776", if it can't connect, checks if ED is running.
        /// If it is not running, it launches it, then tries to connect to DewRcon again. Finally, after a connection
        /// to DewRcon has been established, a command is sent.
        /// </summary>
        /// <param name="cmd"> The command to get passed into the game (i.e. "connect [ip] [password]"). </param>
        /// <param name="uri">
        /// 
        /// "ws://" needs to be at the beginning for a WebSocket connection.
        /// 
        /// Port :11776 seems more reliable than :2448 (if anyone tries to rewrite this to work on :2448, keep in mind
        /// that :2448 only works via TCP AFAIK, and that the entire method would need to be rewritten.
        /// (FishPhd's DewRcon code works on :2448 through TCP, however his code seems to either not receive a response
        /// from certain commands - notably, the "connect [ip]" command - or his response parsing drops it.
        /// This probably has something to do with connecting to DewRcon through :2448, the code behind it may be different
        /// than that of DewRcon running on :11776.
        /// </param>
        /// <returns></returns>
        private async Task DewRcon(string cmd, string uri = "ws://localhost:11776")
        {
            await Task.Run(async () =>
            {
                // The "dew-rcon" subprotocol is needed for a successful connection to DewRcon.
                WebSocket webSocket = new WebSocket(uri, "dew-rcon");

                // Attempt to connect to the DewRcon server (does not throw an exception if failed, so instead we check the WebSocketState
                // to see if it connected successfully.
                webSocket.Connect();

                if (webSocket.ReadyState == WebSocketState.Open)
                {
                    // If it gets this far, it connected to DewRcon successfully.
                    // Update the RichTextBox to the right of the Server-List to inform the user that it has connected.
                    richTextBox1.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] {
                    richTextBox1, "Connected to DewRcon!" + webSocket.ReadyState
                });
                    // Re-enable the "Connect" button.
                    button8.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] { button8, true });
                }
                else
                {
                    if (Process.GetProcessesByName("eldorado").Count() == 0)
                    {
                        try
                        {
                            if (File.Exists("eldorado.exe"))
                            {
                                // Try to launch ElDewrito
                                ProcessStartInfo eldorado = new ProcessStartInfo("eldorado.exe");
                                eldorado.WindowStyle = ProcessWindowStyle.Normal;
                                eldorado.Arguments = "-launcher";
                                Process.Start(eldorado);
                                // If it got this far without throwing an exception, ElDewrito was successfully launched.
                                // Notify the user of this.
                                richTextBox1.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] {
                                    richTextBox1, "ElDewrito has ben launched!"
                                });
                            }
                            else // If eldorado.exe doesn't exist, inform the user and return to break out of the DewRcon loop.
                            {
                                MessageBox.Show("Unable to locate \"eldorado.exe\". Is it in the correct folder?");
                                return;
                            }
                        }
                        catch (Exception e)
                        {
                            // If an exception was thrown, ElDewrito did not launch for a reason other than it not existing in the
                            // correct folder, as that would have been handled above. Inform the user and return to break out of the DewRcon loop;
                            MessageBox.Show(e.Message);
                            return;
                        }
                    }

                    // Close the websocket (even though it's probably already closed because
                    // it failed to connect; no harm in being sure it's properly closed here.
                    webSocket.Close();

                    // Retry to connect to DewRcon and send the command.
                    await DewRcon(cmd);
                    return;
                }

                // Don't send an empty DewRcon command.
                if (cmd != string.Empty)
                {
                    webSocket.Send(Encoding.UTF8.GetBytes(cmd));
                    webSocket.OnMessage += (sender, m) =>
                    {
                        // Display response in the RichTextBox to the right of the Server-List then close the WebSocket.
                        richTextBox1.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] {
                            richTextBox1, Encoding.UTF8.GetString(m.RawData).TrimEnd('\0')
                        });
                        webSocket.Close();
                        if (webSocket.ReadyState != WebSocketState.Closed && webSocket.ReadyState != WebSocketState.Closing)
                            MessageBox.Show("OPEND!!!");
                    };
                }
            });
        }
    }
}
