using Ini;
using SharpSvn;
using System;
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
        // TODO: Comment everything in this file.
        private void modSyncWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            int i = 0;
            worker.ReportProgress(i);

            // Mod-Sync Downloading
            foreach (var mod in globalModsToSync)
            {
                // TODO: test this. Checks if the mod is downloaded but not installed before downloading... If this is the case, skip downloading
                // it. (It will still get installed through the mod-install foreach.)
                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "mods", mod)))
                    continue;

                string modName = string.Empty;
                string locLocation = string.Empty;

                for (var t = 0; t < 3; t++)
                {
                    try
                    {
                        SvnClient svnClient = new SvnClient();
                        modName = Path.GetDirectoryName(mod).Replace(@"tagmods\", "") + "/";
                        string remLocation = "https://github.com/Clef-0/FMM-Mods/trunk/" + modName;
                        locLocation = Path.Combine(Directory.GetCurrentDirectory(), "mods", "tagmods", modName);
                        locLocation = Path.GetDirectoryName(locLocation) + Path.DirectorySeparatorChar;
                        richTextBox1.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] { richTextBox1, $"Downloading {remLocation}" });

                        svnClient.CheckOut(new Uri(remLocation), locLocation);

                        // TODO: test this. Updates qmarchi's fractalcore mod-download counter.
                        using (var httpClient = new HttpClient())
                        {
                            try
                            {
                                var response = httpClient.GetAsync(@"https://dev.fractalcore.net/fmm/api/mod/" + Path.GetFileNameWithoutExtension(mod) + @"/downloaded");
                                Console.WriteLine(response.Result.ToString());
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                i++;
                float progress = ((float)i / (float)globalModsToSync.Count()) * 100;
                worker.ReportProgress(Convert.ToInt32(progress));
            }

            //Mod-Sync Installing
            string fmmdat = Path.Combine(Directory.GetCurrentDirectory(), "fmm.dat");
            StreamWriter fmmdatWriter = new StreamWriter(fmmdat);

            string fmmdatRequired = Path.Combine(Directory.GetCurrentDirectory(), "fmmRequired.dat");
            StreamWriter fmmdatRequiredWriter = new StreamWriter(fmmdatRequired);

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

                        richTextBox1.Invoke(new appendNewOutputCallback(this.appendNewOutput), new object[] { richTextBox1, "Installing " + mod });

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

                        i++;
                        float progress = ((float)i / (float)globalModsToSync.Count()) * 100;
                        worker.ReportProgress(Convert.ToInt32(progress));

                    }
                    catch (Exception ex)
                    {
                        FlashWindowEx(this);
                        MessageBox.Show("Error installing " + Path.GetDirectoryName(mod).Replace(@"tagmods\", "") + "/" + ".\nPlease consult the #eldorito IRC for help.\n\n\"" + ex.Message + "\"", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        IniFile ini = new IniFile(Path.Combine(Directory.GetCurrentDirectory(), "mods", mod));
                        if (ini.IniReadValue("FMMInfo", "Required").ToLower() == "true")
                        {
                            fmmdatRequiredWriter.WriteLine(mod);
                        }
                        fmmdatWriter.WriteLine(Path.GetFileNameWithoutExtension(mod));
                        File.Delete(batFile);
                    }
                }
                catch { }
            }
            fmmdatWriter.Close();
            fmmdatRequiredWriter.Close();
            globalModsToSync.Clear();
        }

        private void modSyncWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
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

            tabControl1.Enabled = true;

            // TODO: add a check to make sure all the mods the server requires were installed before
            // enabling connect button and hiding the mod-sync button.
            button8.Enabled = true;
            groupBox5.Hide();
            button9.Hide();
        }
    }
}
