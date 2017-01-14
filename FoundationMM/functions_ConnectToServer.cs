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
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using SharpSvn;
using System.Net.WebSockets;
using System.Threading;

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
        /// TODO: figure out if my "background process detection" in here actually works...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void ConnectToServer()
        {
            // If the selected server requires password, prompt user to input it.
            // TODO: test this (see "Window.cs" TODO in the "button8_Click" method.
            if (globalHost.passworded == "🔒")
                globalPassword = PasswordDialog();

            // Kill background ED processes - This is necessary, because otherwise ED is detected as "Running"
            // and does not get launched by the DewRcon method.
            // TODO: Test if this works. Difficult to test.
            try
            {
                Process[] EDProcesses = Process.GetProcessesByName("eldorado");
                if (EDProcesses.Count() > 0)
                {
                    foreach (var EDprocess in EDProcesses)
                    {
                        // NOTE: Someone on interwebz said that background processes don't have a MainWindowHandle... Hard to test this
                        // thoroughly since the "ED keeps running in background" bug is very random...
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
        /// Displays a simple form with a textbox, and a "continue" button.
        /// Whatever is typed into the textbox when "continue" is pressed is returned to the method-call.
        /// </summary>
        /// <returns></returns>
        public static string PasswordDialog()
        {
            // Setup the form and controls.
            Form prompt = new Form();
            prompt.Width = 200;
            prompt.Height = 100;
            prompt.FormBorderStyle = FormBorderStyle.FixedSingle; // Disable resizing the form...
            prompt.Text = "";
            Label textLabel = new Label() { Left = 5, Top = 5, Text = "Server-Password" };
            TextBox inputBox = new TextBox() { Left = 5, Top = 35, Width = 95 };
            Button confirmation = new Button() { Text = "Continue", Left = 105, Width = 75, Top = 34 };

            // "Continue" button is clicked, close the form.
            confirmation.Click += (sender, e) => { prompt.Close(); };

            /// NOTE: Uncommenting the following will make what the user types be displayed as *'s.
            /// This does not replace what is returned by the method.
            //inputBox.PasswordChar = '*'; 

            // Add the controls to the form.
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(inputBox);

            // Show the form.
            prompt.ShowDialog();

            // Return the text that was entered into the input box to the method-call.
            return inputBox.Text;
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
        /// than that of DewRcon running on :11775.
        /// 
        /// </param>
        /// <returns></returns>
        private async Task DewRcon(string cmd, string uri = "ws://localhost:11776")
        {

            // Initialize a null ClientWebSocket object to attempt to connect to DewRcon through.
            // This needs to be initialized here so it can be used both inside and outside of the "Try" block.
            ClientWebSocket webSocket = new ClientWebSocket();
            try // Try to connect to DewRcon
            {
                // NOTE: Not sure if this is needed, but the other server browsers appear to use it when checking in Fiddler2
                webSocket.Options.AddSubProtocol("dew-rcon");
                // Attempt to connect to DewRcon.
                await webSocket.ConnectAsync(new Uri(uri), CancellationToken.None);
                // If it gets this far without throwing an exception, it connected to DewRcon successfully.
                // Update the RichTextBox to the right of the Server-List to inform the user that it has connected.
                richTextBox1.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] { richTextBox1, "Connected to DewRcon!" });
                // Re-enable the "Connect" button.
                button8.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] { button8, true });
            }
            // NOTE: An exception probably means that ElDewrito isn't running and should be launched... Or maybe that this app isn't being run with
            // administrator priviledges...
            catch (Exception e)
            {
                // NOTE: if the exception is "UnauthorizedAccessException" then this application probably needs to be run as administrator
                // (the "WebSocket" class that I used in the DewRcon method may require admin rights... TODO: Test if admin rights are needed
                // and if this message box shows properly)
                if (e.InnerException is UnauthorizedAccessException)
                    MessageBox.Show(e.Message);

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
                            richTextBox1.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] { richTextBox1, "ElDewrito has ben launched!" });
                        }
                        else // If eldorado.exe doesn't exist, inform the user and return to break out of the DewRcon loop.
                        {
                            MessageBox.Show("Unable to locate \"eldorado.exe\". Is it in the correct folder?");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        // If an exception was thrown, ElDewrito did not launch for a reason other than it not existing in the
                        // correct folder. Inform the user and return to break out of the DewRcon loop;
                        MessageBox.Show(ex.Message);
                        return;
                    }
                }

                // Dispose of the ClientWebSocket object (even though it's probably already disposed automatically because
                // it failed to connect; no harm in being sure it's properly disposed.
                webSocket.Dispose();

                // Retry to connect to DewRcon and send the command.
                await DewRcon(cmd);
                return;
            }

            // Don't send a blank command, as no response will be received
            if (cmd != string.Empty)
            {
                // Send Command
                byte[] sendBuffer = Encoding.UTF8.GetBytes(cmd);
                await webSocket.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Text, false, CancellationToken.None);

                // Receive response                        8kb seems to be enough to contain any response, using 16kb for good measure...
                byte[] receiveBuffer = new byte[16384]; // not sure if this is good practice, but it seems to work perfectly...
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                // Display response in the RichTextBox to the right of the Server-List.
                richTextBox1.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] { richTextBox1, Encoding.UTF8.GetString(receiveBuffer).TrimEnd('\0') });
                Console.Write(Encoding.UTF8.GetString(receiveBuffer).TrimEnd('\0'));
            }

            // Send "Close" message to the DewRcon server.
            await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            // Dispose the ClientWebSocketObject
            webSocket.Dispose();
            return;
        }
    }
}
