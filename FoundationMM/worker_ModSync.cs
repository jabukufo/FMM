using Ini;
using SharpSvn;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows.Forms;

namespace FoundationMM
{
    public partial class Window : Form
    {
        private void modSyncWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            // See comments in modSyncWorker_ProgressChanged() below to read how progress percentage is done...
            int i = 0;
            float progress = 0;
            worker.ReportProgress(Convert.ToInt32(progress));

            // Mod-Sync Downloading
            foreach (var mod in globalModsToSync)
            {
                // TODO: test this. Checks if the mod is downloaded but not installed before downloading... If this is the case, skip downloading
                // it. (It will still get installed through the mod-install foreach.) We also need to update the progress here, even though 
                // nothing was downloaded, it still "finished" downloading it...
                if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "mods", mod)))
                {
                    // See comments in modSyncWorker_ProgressChanged() below to read how progress percentage is done...
                    i++;
                    progress = ((float)i / (float)globalModsToSync.Count()) * 100;
                    worker.ReportProgress(Convert.ToInt32(progress));
                    continue;
                }

                // Try downloading the mod 3 times...
                for (var t = 0; t < 3; t++)
                {
                    try
                    {
                        // Svn client is used to download specific folders from a githup repo.
                        SvnClient svnClient = new SvnClient();
                        // Use the "mod" string, which may look something like: "tagmods\\modName\\modName.fm"
                        // to parse the download-link(remLocation) and local location to save the mod to (locLocation).
                        string modName = Path.GetDirectoryName(mod).Replace(@"tagmods\", "") + "/";
                        string remLocation = "https://github.com/Clef-0/FMM-Mods/trunk/" + modName;
                        string locLocation = Path.Combine(Directory.GetCurrentDirectory(), "mods", "tagmods", modName);
                        locLocation = Path.GetDirectoryName(locLocation) + Path.DirectorySeparatorChar;

                        // Inform user that it is downloading a mod, and the mod-name.
                        richTextBox1.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] { richTextBox1, $"Downloading {remLocation}" });

                        // "Checkout" the mod folder from the URL, to the local location...
                        svnClient.CheckOut(new Uri(remLocation), locLocation);

                        // Updates qmarchi's fractalcore mod-download counter.
                        using (var httpClient = new HttpClient())
                        {
                            try
                            {
                                var response = httpClient.GetAsync(@"https://dev.fractalcore.net/fmm/api/mod/" + Path.GetFileNameWithoutExtension(mod) + @"/downloaded");
                            }
                            catch { }
                        }
                        // If it gets this far, the mod was successfully downloaded... break so it downloading it isn't attempted again.
                        break;
                    }
                    catch { }
                }

                // See comments in modSyncWorker_ProgressChanged() below to read how progress percentage is done...
                i++;
                progress = ((float)i / (float)globalModsToSync.Count()) * 100;
                worker.ReportProgress(Convert.ToInt32(progress));
            }

            // File used to store the mod-names in, and a StreamWriter for that file.
            string fmmdat = Path.Combine(Directory.GetCurrentDirectory(), "fmm.dat");
            StreamWriter fmmdatWriter = new StreamWriter(fmmdat);

            // File used to store the mod-locations in (for mods with Required=True set in their .ini), and a StreamWriter for that file.
            string fmmdatRequired = Path.Combine(Directory.GetCurrentDirectory(), "fmmRequired.dat");
            StreamWriter fmmdatRequiredWriter = new StreamWriter(fmmdatRequired);

            // This list is used to add mods mods from globalModsToSync to, after they have been fully synced.
            // This is so we can in turn remove those mods from globalModsToSync, and then check if it is empty
            // to determine if ALL of the mods were synced fully.
            List<string> modsSynced = new List<string> { };

            //Mod-Sync Installing
            foreach (var mod in globalModsToSync)
            {
                try
                {
                    // init variables
                    string fmFile = Path.Combine(Directory.GetCurrentDirectory(), "mods", mod);
                    string batFile = Path.Combine(Path.GetDirectoryName(fmFile), "fm_temp.bat");

                    try
                    {
                        // duplicate .fm as temp .bat installer.
                        File.Copy(fmFile, batFile, true);

                        // startInfo for installer
                        ProcessStartInfo startInfo = new ProcessStartInfo();
                        if (showInstallers == false)
                        {
                            startInfo.CreateNoWindow = true;
                            startInfo.UseShellExecute = false;
                            startInfo.RedirectStandardOutput = true;
                            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        }
                        startInfo.FileName = batFile;
                        startInfo.WorkingDirectory = Directory.GetCurrentDirectory();

                        // Inform user that it is installing a mod, and the mod-name.
                        richTextBox1.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] { richTextBox1, "Installing " + mod });

                        // Write the mod-name in fmm.dat before starting the installer.
                        fmmdatWriter.WriteLine(Path.GetFileNameWithoutExtension(mod));
                        // start installer
                        using (Process exeProcess = Process.Start(startInfo))
                        {
                            if (startInfo.RedirectStandardOutput == true)
                            {
                                string standard_output;
                                while (!exeProcess.StandardOutput.EndOfStream)
                                {
                                    standard_output = exeProcess.StandardOutput.ReadLine();
                                    if (standard_output.StartsWith("FMM_OUTPUT "))
                                    {
                                        standard_output = standard_output.Trim().Replace("FMM_OUTPUT ", "");
                                        richTextBox1.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] { richTextBox1, standard_output });
                                    }
                                    else if (standard_output.StartsWith("FMM_ALERT "))
                                    {
                                        standard_output = standard_output.Trim().Replace("FMM_ALERT ", "");
                                        Invoke(new showMessageBoxCallback(this.showMessageBox), new object[] { standard_output });
                                    }
                                }
                            }
                            exeProcess.WaitForExit();
                        }

                        // See comments in modSyncWorker_ProgressChanged() below to read how progress percentage is done...
                        i++;
                        progress = ((float)i / (float)globalModsToSync.Count()) * 100;
                        worker.ReportProgress(Convert.ToInt32(progress));

                        // Write the mod into fmmRequired.dat.
                        fmmdatRequiredWriter.WriteLine(mod);
                        // Add the mod to the modsSynced list.
                        modsSynced.Add(mod);
                    }
                    catch (Exception ex)
                    {
                        FlashWindowEx(this);
                        MessageBox.Show("Error installing " + Path.GetDirectoryName(mod).Replace(@"tagmods\", "") + "/" + ".\nPlease consult the #eldorito IRC for help.\n\n\"" + ex.Message + "\"", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        File.Delete(batFile);
                    }
                }
                catch { }
            }
            // Close both StreamWriters
            fmmdatWriter.Close();
            fmmdatRequiredWriter.Close();

            // Remove mods that were synced from the globalModsToSync list.
            foreach (string mod in modsSynced)
                globalModsToSync.Remove(mod);
        }

        private void modSyncWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Progress "percentage" goes from 0-200. Downloading is 0-100, installing is 100-200.
            // This is because it makes it easy to differentiate the two. A model of how this works is below.

            /// EXAMPLE:
            ///     int i = 0;  // Initialized progress counter to 0.
            ///     foreach (var mod in globalModsToSync) // Enumerate the globalModsToSync list.
            ///     {
            ///         // download mod 
            ///         i++; // Increment the progress counter
            ///         progress = ((float)i / (float)globalModsToSync.Count()) * 100;
            ///         worker.ReportProgress(Convert.ToInt32(progress));
            ///     }
            ///     foreach (var mod in globalModsToSync) // Enumerate the globalModsToSync list AGAIN
            ///     {
            ///         // install mod
            ///         i++; // Increment the progress counter (note, it never got reset after the "download" foreach.
            ///         progress = ((float)i / (float)globalModsToSync.Count()) * 100;
            ///         worker.ReportProgress(Convert.ToInt32(progress));
            ///     }  

            if (e.ProgressPercentage < 100)
                percentageLabel.Text = "Downloading mods: " + e.ProgressPercentage.ToString() + "%";
            else
                percentageLabel.Text = "Installing mods: " + (e.ProgressPercentage / 2).ToString() + "%";
        }

        private void modSyncWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!(e.Error == null))
            {
                percentageLabel.Text = ("Error: " + e.Error.Message);
            }
            percentageLabel.Text = string.Empty;

            // Re-enable the tab control
            tabControl1.Enabled = true;

            // Check to make sure all the mods the server requires were installed before
            // enabling connect button and hiding the mod-sync button.
            if (globalModsToSync == null || globalModsToSync.Count == 0)
            {
                button8.Enabled = true;
                groupBox5.Hide();
                button9.Hide();
            }
        }
    }
}
