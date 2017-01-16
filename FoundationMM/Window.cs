using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Linq;

using Ini;
using System.Drawing;
using System.Net;

using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FoundationMM
{
    public partial class Window : Form
    {
        /// <summary>
        /// These are just here to store this information so that they are accessible from various parts of the program.
        /// </summary>
        // Used to store the "HostServer" object that's stored in the last-selected- ListViewItem's .Tag
        public static HostServer globalHost = new HostServer();
        // Used to store the last entered server-password (used when trying to connect to the globalHost through DewRcon).
        public static string globalPassword = string.Empty;
        // Used to store mods any mods that the last selected server requires which the user does not have installed.
        public static List<string> globalModsToSync = new List<string> { };
        // Used to store the USERS eldewrito version.
        public static string globalEDVersion = string.Empty;

        string[] files = {
                     @"fonts\font_package.bin",
                     "audio.dat",
                     "bunkerworld.map",
                     "chill.map",
                     "cyberdyne.map",
                     "deadlock.map",
                     "guardian.map",
                     "mainmenu.map",
                     "resources.dat",
                     "riverworld.map",
                     "s3d_avalanche.map",
                     "s3d_edge.map",
                     "s3d_reactor.map",
                     "s3d_turf.map",
                     "shrine.map",
                     "string_ids.dat",
                     "tags.dat",
                     "textures.dat",
                     "textures_b.dat",
                     "video.dat",
                     "zanzibar.map"
                 };

        List<string> locatedFMMInstallers = new List<string>();

        public Window()
        {
            InitializeComponent();
        }

        BackgroundWorker deleteOldBackupWorker = new BackgroundWorker();
        BackgroundWorker fileTransferWorker = new BackgroundWorker();
        BackgroundWorker modInstallWorker = new BackgroundWorker();
        BackgroundWorker restoreCleanWorker = new BackgroundWorker();
        BackgroundWorker dlFilesWorker = new BackgroundWorker();
        BackgroundWorker dlModWorkerStarter = new BackgroundWorker();
        BackgroundWorker dlModWorker = new BackgroundWorker();

        // Background worker used for populating/repopulating the Server-List
        BackgroundWorker serverBrowserWorker = new BackgroundWorker();
        // Background worker used for Mod-Sync (which downloads/installs mods in the "globalModsToSync" List.)
        BackgroundWorker modSyncWorker = new BackgroundWorker();

        bool refreshinprog = false;

        // True == the server list is being updated; false == it is not being updated.
        // This is needed so that we can check it before starting the background worker.
        bool serverRefreshProg = false;

        public static void SetDoubleBuffered(System.Windows.Forms.Control c)
        {
            //Taxes: Remote Desktop Connection and painting
            //http://blogs.msdn.com/oldnewthing/archive/2006/01/03/508694.aspx
            if (System.Windows.Forms.SystemInformation.TerminalServerSession)
                return;

            System.Reflection.PropertyInfo aProp =
                  typeof(System.Windows.Forms.Control).GetProperty(
                        "DoubleBuffered",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

            aProp.SetValue(c, true, null);
        }

        // Sets a control and all it's recursive children's foreground(text) color.
        public void UpdateColorControls(Control parent, Color color)
        {
            parent.ForeColor = color;
            foreach (Control child in parent.Controls)
            {
                UpdateColorControls(child, color);
            }
        }

        private void Window_Load(object sender, EventArgs e)
        {
            // attempt double buffering on OSes that support it.
            try
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
                SetDoubleBuffered(listView1);
                SetDoubleBuffered(listView2);
                SetDoubleBuffered(infobarDesc);
                SetDoubleBuffered(infobar2Desc);
            }
            catch
            {
                // lol okay then
                // if that's how you want to be
            }

            outputPanel.Dock = DockStyle.Fill;
            
            ToolTip incPriToolTip = new ToolTip();
            incPriToolTip.SetToolTip(this.button1, "Higher priority installs a mod later.\nThis means it can overwrite changes from other mods.");
            ToolTip topPriToolTip = new ToolTip();
            topPriToolTip.SetToolTip(this.button3, "Higher priority installs a mod later.\nThis means it can overwrite changes from other mods.");
            ToolTip decPriToolTip = new ToolTip();
            decPriToolTip.SetToolTip(this.button2, "Lower priority installs a mod earlier.\nThis means other mods can overwrite its changes.");
            ToolTip botPriToolTip = new ToolTip();
            botPriToolTip.SetToolTip(this.button4, "Lower priority installs a mod earlier.\nThis means other mods can overwrite its changes.");
            ToolTip deleteToolTip = new ToolTip();
            deleteToolTip.SetToolTip(this.button7, "Deletes a selected mod's installer files.\nIf installed, the mod will be removed from your game next time you apply.");
            ToolTip rootDirToolTip = new ToolTip();
            rootDirToolTip.SetToolTip(this.openGameRoot, "Opens your Halo Online root directory.");
            ToolTip modsDirToolTip = new ToolTip();
            modsDirToolTip.SetToolTip(this.openMods, "Opens your FMM mods directory.");
            ToolTip applyToolTip = new ToolTip();
            applyToolTip.SetToolTip(this.button5, "Installs checked mods to your Halo Online installation.");
            ToolTip launchToolTip = new ToolTip();
            launchToolTip.SetToolTip(this.button6, "Opens 'eldorado.exe' from FMM's current directory.");
            ToolTip dlToolTip = new ToolTip();
            dlToolTip.SetToolTip(this.button16, "Downloads checked mods to your 'My Mods' list.");


            deleteOldBackupWorker.WorkerSupportsCancellation = true;
            deleteOldBackupWorker.DoWork += new DoWorkEventHandler(deleteOldBackup_DoWork);

            dlModWorkerStarter.WorkerSupportsCancellation = true;
            dlModWorkerStarter.WorkerReportsProgress = true;
            dlModWorkerStarter.DoWork += new DoWorkEventHandler(dlModWorkerStarter_DoWork);
            dlModWorkerStarter.ProgressChanged += new ProgressChangedEventHandler(dlModWorkerStarter_ProgressChanged);
            dlModWorkerStarter.RunWorkerCompleted += new RunWorkerCompletedEventHandler(dlModWorkerStarter_RunWorkerCompleted);

            dlModWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(dlModWorker_RunWorkerCompleted);
            dlModWorker.WorkerSupportsCancellation = true;
            dlModWorker.WorkerReportsProgress = true;
            dlModWorker.DoWork += new DoWorkEventHandler(dlModWorker_DoWork);

            fileTransferWorker.WorkerSupportsCancellation = true;
            fileTransferWorker.WorkerReportsProgress = true;
            fileTransferWorker.DoWork += new DoWorkEventHandler(fileTransferWorker_DoWork);
            fileTransferWorker.ProgressChanged += new ProgressChangedEventHandler(fileTransferWorker_ProgressChanged);
            fileTransferWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(fileTransferWorker_RunWorkerCompleted);

            modInstallWorker.WorkerSupportsCancellation = true;
            modInstallWorker.WorkerReportsProgress = true;
            modInstallWorker.DoWork += new DoWorkEventHandler(modInstallWorker_DoWork);
            modInstallWorker.ProgressChanged += new ProgressChangedEventHandler(modInstallWorker_ProgressChanged);
            modInstallWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(modInstallWorker_RunWorkerCompleted);

            restoreCleanWorker.WorkerSupportsCancellation = true;
            restoreCleanWorker.WorkerReportsProgress = true;
            restoreCleanWorker.DoWork += new DoWorkEventHandler(restoreCleanWorker_DoWork);
            restoreCleanWorker.ProgressChanged += new ProgressChangedEventHandler(restoreCleanWorker_ProgressChanged);
            restoreCleanWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(restoreCleanWorker_RunWorkerCompleted);

            dlFilesWorker.WorkerSupportsCancellation = true;
            dlFilesWorker.WorkerReportsProgress = true;
            dlFilesWorker.DoWork += new DoWorkEventHandler(dlFilesWorker_DoWork);
            dlFilesWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(dlFilesWorker_RunWorkerCompleted);

            // serverList population background worker stufffff.
            serverBrowserWorker.WorkerSupportsCancellation = true;
            serverBrowserWorker.WorkerReportsProgress = true;
            serverBrowserWorker.DoWork += new DoWorkEventHandler(serverBrowserWorker_DoWork);
            serverBrowserWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(serverBrowserWorker_RunWorkerCompleted);

            // Mod-Sync background worker stufffff.
            modSyncWorker.WorkerSupportsCancellation = true;
            modSyncWorker.WorkerReportsProgress = true;
            modSyncWorker.DoWork += new DoWorkEventHandler(modSyncWorker_DoWork);
            modSyncWorker.ProgressChanged += new ProgressChangedEventHandler(modSyncWorker_ProgressChanged);
            modSyncWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(modSyncWorker_RunWorkerCompleted);

            DirectoryInfo dir0 = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "mods", "tagmods"));
            string identifier = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "fmm.ini");
            string langIdentifier = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "fmm_lang.ini");

            // Sets foreground color for every control to "Green" if the date is April 20.
            DateTime now = DateTime.Now;
            if (now.Month == 4 && now.Day == 20)
                UpdateColorControls(this, Color.Green);
#if !DEBUG

            if (!File.Exists(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "mtndew.dll")))
            {
                MessageBox.Show("The FMM zip should be extracted to the root of your ElDewrito directory.");

                Application.Exit();
                return;
            }

            if (!File.Exists(identifier))
            {
                IniFile ini = new IniFile(identifier);
                FileVersionInfo mtndewVersion = FileVersionInfo.GetVersionInfo(Path.Combine(Directory.GetCurrentDirectory(), "mtndew.dll"));
                ini.IniWriteValue("FMMPrefs", "EDVersion", mtndewVersion.FileVersion);
            }
            else
            {
                IniFile ini = new IniFile(identifier);

                Form thisForm = (Form)sender;
                thisForm.Width = Convert.ToInt32(ini.IniReadValue("FMMPrefs", "Width"));
                thisForm.Height = Convert.ToInt32(ini.IniReadValue("FMMPrefs", "Height"));
                if (ini.IniReadValue("FMMPrefs", "DevMode").ToLower() == "true")
                {
                    devModeGroupBox.Visible = true;
                }

                string savedversion = ini.IniReadValue("FMMPrefs", "EDVersion");
                string actualversion = FileVersionInfo.GetVersionInfo(Path.Combine(Directory.GetCurrentDirectory(), "mtndew.dll")).FileVersion;

                if (savedversion != actualversion)
                {
                    string mapsPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "maps");
                    deleteOldBackupWorker.RunWorkerAsync(new string[] { mapsPath });
                    ini.IniWriteValue("FMMPrefs", "EDVersion", actualversion);
                    showMessageBox("You appear to have updated ElDorito. Please reinstall mods you wish to keep using.");
                }
            }

            refreshMods.ToolTipText = "Reloads the current tab's mod list.";

            //languages
            if (!File.Exists(langIdentifier))
            {
                IniFile ini = new IniFile(langIdentifier);
                ini.IniWriteValue("FMMLang", "Tab_MyMods", tabPage1.Text);
                ini.IniWriteValue("FMMLang", "Tab_DownloadableMods", tabPage2.Text);
                ini.IniWriteValue("FMMLang", "GroupBox_Mods", groupBox1.Text);
                ini.IniWriteValue("FMMLang", "GroupBox_Directories", groupBox2.Text);
                ini.IniWriteValue("FMMLang", "GroupBox_DeveloperMode", devModeGroupBox.Text);
                ini.IniWriteValue("FMMLang", "GroupBox_Game", groupBox3.Text);
                ini.IniWriteValue("FMMLang", "Button_IncreasePriority", button1.Text);
                ini.IniWriteValue("FMMLang", "Button_DecreasePriority", button2.Text);
                ini.IniWriteValue("FMMLang", "Button_DeleteSelectedMod", button7.Text);
                ini.IniWriteValue("FMMLang", "Button_OpenGameFolder", openGameRoot.Text);
                ini.IniWriteValue("FMMLang", "Button_OpenModsFolder", openMods.Text);
                ini.IniWriteValue("FMMLang", "Button_EnableFileRestoration", lang_EnableFileRestoration);
                ini.IniWriteValue("FMMLang", "Button_DisableFileRestoration", lang_DisableFileRestoration);
                ini.IniWriteValue("FMMLang", "Button_EnableCMDWindows", lang_EnableCMDWindows);
                ini.IniWriteValue("FMMLang", "Button_DisableCMDWindows", lang_DisableCMDWindows);
                ini.IniWriteValue("FMMLang", "Button_ApplyCheckedMods", button5.Text);
                ini.IniWriteValue("FMMLang", "Button_LaunchElDewrito", button6.Text);
                ini.IniWriteValue("FMMLang", "Button_DownloadCheckedMods", button16.Text);
                ini.IniWriteValue("FMMLang", "ToolTip_IncreasePriority", "Higher priority installs a mod later.\nThis means it can overwrite changes from other mods.");
                ini.IniWriteValue("FMMLang", "ToolTip_DecreasePriority", "Lower priority installs a mod earlier.\nThis means other mods can overwrite its changes.");
                ini.IniWriteValue("FMMLang", "ToolTip_DeleteSelectedMod", "Deletes a selected mod's installer files.\nIf installed, the mod will be removed from your game next time you apply.");
                ini.IniWriteValue("FMMLang", "ToolTip_OpenGameFolder", "Opens your Halo Online root directory.");
                ini.IniWriteValue("FMMLang", "ToolTip_OpenModsFolder", "Opens your FMM mods directory.");
                ini.IniWriteValue("FMMLang", "ToolTip_ApplyCheckedMods", "Installs checked mods to your Halo Online installation.");
                ini.IniWriteValue("FMMLang", "ToolTip_LaunchElDewrito", "Opens 'eldorado.exe' from FMM's current directory.");
                ini.IniWriteValue("FMMLang", "ToolTip_DownloadCheckedMods", "Downloads checked mods to your 'My Mods' list.");
                ini.IniWriteValue("FMMLang", "ToolTip_Refresh", "Reloads the current tab's mod list.");
                ini.IniWriteValue("FMMLang", "Header_Name", header_Name.Text);
                ini.IniWriteValue("FMMLang", "Header_Author", header_Author.Text);
                ini.IniWriteValue("FMMLang", "Header_Version", header_Version.Text);
                ini.IniWriteValue("FMMLang", "Header_Description", header_Description.Text);
                ini.IniWriteValue("FMMLang", "Header_Warnings", header_Warnings.Text);
                ini.IniWriteValue("FMMLang", "Header_Location", header_Location.Text);
                ini.IniWriteValue("FMMLang", "String_ModAvailable", lang_ModAvailable);
                ini.IniWriteValue("FMMLang", "String_ModsAvailable", lang_ModsAvailable);
            }
            else
            {
                IniFile ini = new IniFile(langIdentifier);
                tabPage1.Text = ini.IniReadValue("FMMLang", "Tab_MyMods");
                tabPage2.Text = ini.IniReadValue("FMMLang", "Tab_DownloadableMods");
                groupBox1.Text = ini.IniReadValue("FMMLang", "GroupBox_Mods");
                groupBox7.Text = ini.IniReadValue("FMMLang", "GroupBox_Mods");
                groupBox2.Text = ini.IniReadValue("FMMLang", "GroupBox_Directories");
                devModeGroupBox.Text = ini.IniReadValue("FMMLang", "GroupBox_DeveloperMode");
                groupBox3.Text = ini.IniReadValue("FMMLang", "GroupBox_Game");
                button1.Text = ini.IniReadValue("FMMLang", "Button_IncreasePriority");
                button2.Text = ini.IniReadValue("FMMLang", "Button_DecreasePriority");
                button7.Text = ini.IniReadValue("FMMLang", "Button_DeleteSelectedMod");
                openGameRoot.Text = ini.IniReadValue("FMMLang", "Button_OpenGameFolder");
                openMods.Text = ini.IniReadValue("FMMLang", "Button_OpenModsFolder");
                lang_EnableFileRestoration = ini.IniReadValue("FMMLang", "Button_EnableFileRestoration");
                lang_DisableFileRestoration = ini.IniReadValue("FMMLang", "Button_DisableFileRestoration");
                toggleFileRestoration.Text = ini.IniReadValue("FMMLang", "Button_DisableFileRestoration");
                lang_EnableCMDWindows = ini.IniReadValue("FMMLang", "Button_EnableCMDWindows");
                toggleCmdWindows.Text = ini.IniReadValue("FMMLang", "Button_EnableCMDWindows");
                lang_DisableCMDWindows = ini.IniReadValue("FMMLang", "Button_DisableCMDWindows");
                button5.Text = ini.IniReadValue("FMMLang", "Button_ApplyCheckedMods");
                button6.Text = ini.IniReadValue("FMMLang", "Button_LaunchElDewrito");
                button16.Text = ini.IniReadValue("FMMLang", "Button_DownloadCheckedMods");
                incPriToolTip.SetToolTip(this.button1, ini.IniReadValue("FMMLang", "ToolTip_IncreasePriority"));
                topPriToolTip.SetToolTip(this.button3, ini.IniReadValue("FMMLang", "ToolTip_IncreasePriority"));
                decPriToolTip.SetToolTip(this.button2, ini.IniReadValue("FMMLang", "ToolTip_DecreasePriority"));
                botPriToolTip.SetToolTip(this.button4, ini.IniReadValue("FMMLang", "ToolTip_DecreasePriority"));
                deleteToolTip.SetToolTip(this.button7, ini.IniReadValue("FMMLang", "ToolTip_DeleteSelectedMod"));
                rootDirToolTip.SetToolTip(this.openGameRoot, ini.IniReadValue("FMMLang", "ToolTip_OpenGameFolder"));
                modsDirToolTip.SetToolTip(this.openMods, ini.IniReadValue("FMMLang", "ToolTip_OpenModsFolder"));
                applyToolTip.SetToolTip(this.button5, ini.IniReadValue("FMMLang", "ToolTip_ApplyCheckedMods"));
                launchToolTip.SetToolTip(this.button6, ini.IniReadValue("FMMLang", "ToolTip_LaunchElDewrito"));
                dlToolTip.SetToolTip(this.button16, ini.IniReadValue("FMMLang", "ToolTip_DownloadCheckedMods"));
                refreshMods.ToolTipText = ini.IniReadValue("FMMLang", "ToolTip_Refresh");
                header_Name.Text = ini.IniReadValue("FMMLang", "Header_Name");
                header_Author.Text = ini.IniReadValue("FMMLang", "Header_Author");
                header_Version.Text = ini.IniReadValue("FMMLang", "Header_Version");
                header_Description.Text = ini.IniReadValue("FMMLang", "Header_Description");
                header_Warnings.Text = ini.IniReadValue("FMMLang", "Header_Warnings");
                header_Location.Text = ini.IniReadValue("FMMLang", "Header_Location");
                columnHeader1.Text = ini.IniReadValue("FMMLang", "Header_Name");
                columnHeader2.Text = ini.IniReadValue("FMMLang", "Header_Author");
                columnHeader3.Text = ini.IniReadValue("FMMLang", "Header_Version");
                columnHeader4.Text = ini.IniReadValue("FMMLang", "Header_Description");
                columnHeader5.Text = ini.IniReadValue("FMMLang", "Header_Warnings");
                columnHeader6.Text = ini.IniReadValue("FMMLang", "Header_Location");
                lang_ModAvailable = ini.IniReadValue("FMMLang", "String_ModAvailable");
                lang_ModsAvailable = ini.IniReadValue("FMMLang", "String_ModsAvailable");
            }

#endif
        IniFile ini2 = new IniFile(identifier);

            Log("Looking for installers...");
            lookForFMMInstallers();
            Log("Adding installers to list...");
            addFMMInstallersToList();
            Log("Ordering installers as saved...");
            checkFMMInstallerOrder();
            
            // TODO: Check if the offlinemode stuff here is working for the serverbrowser tab.
            if (ini2.IniReadValue("FMMPrefs", "OfflineMode").ToLower() != "true")
            {
                // Begins updating the server-list when the app is launched.
                Log("Updating server list...");

                // True == the server list is being updated; false == it is not being updated.
                // This is needed so that we can check it before starting the background worker.
                serverRefreshProg = true;

                // Begin populating the server-list.
                serverBrowserWorker.RunWorkerAsync(new string[] { Path.Combine(System.IO.Directory.GetCurrentDirectory(), "mods", "tagmods") });

                Log("Downloading mod list...");
                refreshinprog = true;
                dlFilesWorker.RunWorkerAsync(new string[] { Path.Combine(System.IO.Directory.GetCurrentDirectory(), "mods", "tagmods") });
            }

            if (ini2.IniReadValue("FMMPrefs", "OfflineMode").ToLower() == "true")
            {
                tabControl1.TabPages.Remove(tabPage2);
                tabControl1.Appearance = TabAppearance.Buttons;
                tabControl1.ItemSize = new Size(0, 1);
                tabControl1.SizeMode = TabSizeMode.Fixed;
                tabControl1.Margin = new Padding(0, 0, 0, 0);

                // This should remove the serverbrowser tab when in offlinemode.
                tabControl1.TabPages.Remove(tabPage3);
                tabControl1.Appearance = TabAppearance.Buttons;
                tabControl1.ItemSize = new Size(0, 1);
                tabControl1.SizeMode = TabSizeMode.Fixed;
                tabControl1.Margin = new Padding(0, 0, 0, 0);
            }

            Log("Counting available mods...");

            int modCount = listView1.Items.Count;
            if (modCount == 1)
            {
                modNumberLabel.Text = "1 " + lang_ModAvailable;
            }
            else
            {
                modNumberLabel.Text = modCount + " " + lang_ModsAvailable;
            }

            infobar.Visible = false;
        }

        private void Window_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (tabControl1.Enabled == false)
            {
                DialogResult dialogResult = MessageBox.Show("FMM is working, and cancelling may leave critical files corrupt or missing.\n\nAre you sure you want to cancel?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                
                if (dialogResult == DialogResult.Yes)
                {
                    e.Cancel = false;
                }
                else if (dialogResult == DialogResult.No)
                {
                    e.Cancel = true;
                }
            }

            Form thisForm = (Form)sender;
            string identifier = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "fmm.ini");
            if (File.Exists(identifier))
            {
                IniFile ini = new IniFile(identifier);
                ini.IniWriteValue("FMMPrefs", "Width", thisForm.Width.ToString());
                ini.IniWriteValue("FMMPrefs", "Height", thisForm.Height.ToString());
            }
        }

        int enabledTab = 0;

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            enabledTab = tabControl1.SelectedIndex;

            // Used to store the amount of items the ListView of the current tab contains.
            int itemCount = 0;

            // Used to determine whether the modNumberLabel.Text should say "mods available" or "servers" available,
            // depending on the current tab.
            bool server = false;
            if (enabledTab == 0)
            {
                refreshMods.Visible = true;
                modNumberLabel.Visible = true;
                itemCount = listView1.Items.Count;
                // This IS NOT the server-browser tab, so this is set to FALSE.
                server = false;
            }
            else if (enabledTab == 1)
            {
                refreshMods.Visible = true;
                modNumberLabel.Visible = true;
                itemCount = listView2.Items.Count;
                // This IS NOT the server-browser tab, so this is set to FALSE.
                server = false;
            }
            else if (enabledTab == 2)
            {
                refreshMods.Visible = true;
                modNumberLabel.Visible = true;
                itemCount = listView3.Items.Count;
                // This IS the server-browser tab, so this is set to TRUE.
                server = true;
            }

            // If there is ONLY one item in the list, and the tab IS NOT the server-browser tab
            // set the text to "1 mod available"
            if (itemCount == 1 && server == false)
            {
                modNumberLabel.Text = "1 " + lang_ModAvailable;
            }
            // If there is NOT ONLY one item in the list, and the tab IS NOT the server-browser tab
            // set the text to "'X' mods available"
            else if (itemCount != 1 && server == false)
            {
                modNumberLabel.Text = itemCount + " " + lang_ModsAvailable;
            }
            // If there is ONLY one item in the list, and the tab IS the server-browser tab
            // set the text to "1 server available"
            else if (itemCount == 1 && server == true)
            {
                modNumberLabel.Text = "1 " + "server available";
            }
            // If there is NOT ONLY one item in the list, and the tab IS the server-browser tab
            // set the text to "'X' servers available"
            else if (itemCount != 1 && server == true)
            {
                modNumberLabel.Text = itemCount + " servers available";
            }

            infobar.Visible = false;
            infobar2.Visible = false;
        }

        bool listView1DND = false;
        private void listView1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (listView1DND) { return; }
            if (((Control.ModifierKeys & Keys.Shift) != 0))
            {
                listView1DND = true;
                e.NewValue = e.CurrentValue;
                if (listView1.CheckedItems.Count == listView1.Items.Count)
                {
                    foreach (ListViewItem item in listView1.Items)
                    {
                        item.Checked = false;
                    }
                }
                else
                {
                    foreach (ListViewItem item in listView1.Items)
                    {
                        item.Checked = true;
                    }
                }
            }
            listView1DND = false;
        }

        bool listView2DND = false;
        private void listView2_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (listView2DND) { return; }
            if (((Control.ModifierKeys & Keys.Shift) != 0))
            {
                listView2DND = true;
                e.NewValue = e.CurrentValue;
                if (listView2.CheckedItems.Count == listView2.Items.Count)
                {
                    foreach (ListViewItem item in listView2.Items)
                    {
                        item.Checked = false;
                    }
                }
                else
                {
                    foreach (ListViewItem item in listView2.Items)
                    {
                        item.Checked = true;
                    }
                }
            }
            listView2DND = false;
        }

        private int sortColumn1 = -1;

        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column != sortColumn1)
            {
                sortColumn1 = e.Column;
                listView1.Sorting = SortOrder.Ascending;
            }
            else
            {
                if (listView1.Sorting == SortOrder.Ascending)
                {
                    listView1.Sorting = SortOrder.Descending;
                }
                else
                {
                    listView1.Sorting = SortOrder.Ascending;
                }
            }
            
            listView1.Sort();
            listView1.ListViewItemSorter = new ListViewItemComparer(e.Column, listView1.Sorting);
            checkFMMInstallerOrder();
        }

        private int sortColumn2 = -1;

        private void listView2_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            
            if (e.Column != sortColumn2)
            {
                sortColumn2 = e.Column;
                listView2.Sorting = SortOrder.Ascending;
            }
            else
            {
                if (listView2.Sorting == SortOrder.Ascending)
                {
                    listView2.Sorting = SortOrder.Descending;
                }
                else
                {
                    listView2.Sorting = SortOrder.Ascending;
                }
            }

            listView2.Sort();
            listView2.ListViewItemSorter = new ListViewItemComparer(e.Column, listView2.Sorting);
            checkFMMInstallerOrder();
        }


        private int sortColumn3 = -1;
        /// <summary>
        /// Sorting stuff for server list... Uses special sort method for the "Ping" and "Player"
        /// columns since they are "numbers" and don't get sorted properly through standard
        /// string sorting.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listView3_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column != sortColumn3)
            {
                sortColumn3 = e.Column;
                listView3.Sorting = SortOrder.Ascending;
            }
            else
            {
                if (listView3.Sorting == SortOrder.Ascending)
                {
                    listView3.Sorting = SortOrder.Descending;
                }
                else
                {
                    listView3.Sorting = SortOrder.Ascending;
                }
            }

            listView3.Sort();

            if (e.Column == 3 || e.Column == 7) // If the "Ping" or "Players" column is being sorted, we need to do special sorting logic...
                listView3.ListViewItemSorter = new ListViewItemNumberComparer(e.Column, listView3.Sorting);
            else
                listView3.ListViewItemSorter = new ListViewItemComparer(e.Column, listView3.Sorting);

            checkFMMInstallerOrder();
        }

        /// <summary>
        /// Checks if the server requires any mods that the user does not have installed.
        /// If there are any required mods, they get displayed in richTextBox1 on the
        /// right.
        /// Also runs the ServerInfo() method which updates the ServerInfo panel that
        /// is shown below the ServerList.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listView3_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Clears the globalModsToSync list so that users don't end up having to sync mods from one server before joining another.
            globalModsToSync.Clear();
            // Enable the connect button... It will be disabled again if any mods-to-sync get added to globalModsToSync.
            button8.Enabled = true;
            // Hide the "Mod-Sync" groupbox and button... It will be unhidden if any mods-to-sync get added to globalModsToSync.
            groupBox5.Hide();
            button9.Hide();

            // Reads the list of mods that the user has installed that were marked "Required=True" in the .ini file.
            string requiredMods = string.Empty;
            if (File.Exists("fmmRequired.dat"))
                requiredMods = File.ReadAllText("fmmRequired.dat");

            // Sets "globalHost" to the "HostServer" object that is stored in the selected ListViewItem.Tag.
            if (listView3.SelectedItems.Count != 0)
                globalHost = (HostServer)listView3.SelectedItems[0].Tag;
            // If the selected server requires password show the "Password" groupboxe, otherwise hide it.
            textBox2.Text = "";
            if (globalHost.passworded == "🔒")
                groupBox10.Show();
            else
                groupBox10.Hide();

            // Updates the "Server-Info" panel to show additional information on the selected item.
            ServerInfo();

#if RELEASE
            // Sets the USER's ED version so that it can be compared with the server's ED version...
            if (File.Exists(Environment.CurrentDirectory + "\\mtndew.dll"))
            {
                var _eldoritoVersion = FileVersionInfo.GetVersionInfo(Environment.CurrentDirectory + "\\mtndew.dll");
                globalEDVersion = _eldoritoVersion.ProductVersion;
            }

            // Checks if USER and SERVER ED versions match, if not, disable the connect button for this server, and "return"
            if (globalHost.eldewritoVersion != globalEDVersion)
            {
                // Disables the connect button so users can't connect to a server running a different ED version.
                button8.Enabled = false;
                //button8.Enabled = true; NOTE: for debugging purposes ONLY, uncommenting this line will keep the connect button enabled
                // to allow attempting to connect servers running a differed ED version... Again, for debugging purposes ONLY. 
                richTextBox1.Text = $"Host running different version!\n\nTheirs: {globalHost.eldewritoVersion}\nYours: {globalEDVersion}";
                return;
            }
#endif


            // If the globalHost's "mods" property is not null, the server has mods that are required to be installed before connecting...
            if (globalHost.mods != null)
            {
                // Check each mod that the globalHost requires to have installed...
                foreach (var mod in globalHost.mods)
                {
                    // If the user doesn't have the mod listed inside "fmmRequired.dat", it needs to be installed.
                    // (it should get added to "fmmRequired.dat" when a mod marked "Require=True" is installed.)
                    if (!requiredMods.Contains(mod))
                    {
                        // Disables the "Connect" button - Don't allow connecting to the server if the server requires mods that have not
                        // been installed yet.
                        button8.Enabled = false;

                        // Downloads and Installs any mods that it needs to.
                        globalModsToSync.Add(mod);
                    }
                }
            }

            // Clear the RichTextBox to the right of the server list - this will be used to display mods that require syncing.
            richTextBox1.Text = string.Empty;

            // If the globalModsToSync List is not empty this means there are mods that need to be synced.
            if (globalModsToSync.Count() != 0)
            {
                // Show the "Mod-Sync" groupbox and button.
                groupBox5.Show();
                button9.Show();

                // Add each mod in the globalModsToSync List to the RichTextBox to the right of the server list.
                foreach (var mod in globalModsToSync)
                    richTextBox1.Text += Path.GetFileNameWithoutExtension(mod) + "\n";

                // Finally, after all mods in the globalModsToSync List are added to the RichTextBox to the right of the server list,
                // add text to the end of that in the RichTextBox to the right of the server list informing the user to click
                // the "Mod-Sync" button to Download/Install the required mods.
                richTextBox1.Text += "\nClick the 'Mod-Sync' button below to download and install the above mods.";
            }
        }

        /// <summary>
        /// "Connect" button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button8_Click(object sender, EventArgs e)
        {
            ConnectToServer();
        }

        /// <summary>
        /// "Mod-Sync" button.
        /// Kills all running ED processes then begins Mod-Sync
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button9_Click(object sender, EventArgs e)
        {
            // Disables all the controls so they can't be messed with while Mod-Syncing is happening.
            tabControl1.Enabled = false;

            //Close all detected ED processes so that Mods can be installed properly.
            try
            {
                foreach (Process EDProcess in Process.GetProcessesByName("eldorado"))
                {
                    EDProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            // True == the server list is being updated; false == it is not being updated.
            // This is needed so that we can check it before starting the background worker.
            serverRefreshProg = true;

            // Downloads and installs mods from the "globalModsToSync" List.
            modSyncWorker.RunWorkerAsync(new string[] { Path.Combine(System.IO.Directory.GetCurrentDirectory(), "mods", "tagmods") });
        }

        /// <summary>
        /// "Close Button" for the server-info panel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button10_Click(object sender, EventArgs e)
        {
            // Clear selected items so that the panel doesn't become visible again if
            // the user sorts a column... Sorting a colum changes the selected index for the selected
            // ListViewItem and calls "listView3_SelectedIndexChanged"
            listView3.SelectedItems.Clear();
            // Hide the server-info panel.
            panel7.Visible = false;
        }

        /// <summary>
        /// "Quick Match" button. Selects the server with the lowest ping, that requires
        /// no mods, that has more than "0" players, and is that's status is not "inLobby".
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button11_Click(object sender, EventArgs e)
        {
            // Clear selected items so if the user selects the same item after clicking "QuickMatch"
            // the "richTextBox1" to the right of the Server-List gets updated properly.
            // (this is because otherwise "listView3_SelectedIndexChanged" doesn't get called when clicking that item).
            listView3.SelectedItems.Clear();

            HostServer bestServer = new HostServer();


            foreach (ListViewItem server in listView3.Items)
            {
                // Storing the current "server" in this object instance so it doesn't have to be parsed multiple times.
                HostServer evaluateServer = (HostServer)server.Tag;

                // Skip servers that are full, passworded, non-matching ELDEWRITO version, or non-matching GAME version.
                if (evaluateServer.numPlayers == evaluateServer.maxPlayers
                    || evaluateServer.passworded == "🔒")
                    continue;
#if RELEASE
                                // Set the USER's ED version based off of their mtndew.dll, if it gets located...
                                if (File.Exists(Environment.CurrentDirectory + "\\mtndew.dll"))
                                {
                                    var _eldoritoVersion = FileVersionInfo.GetVersionInfo(Environment.CurrentDirectory + "\\mtndew.dll");
                                    globalEDVersion = _eldoritoVersion.ProductVersion;
                                }
                                if (evaluateServer.eldewritoVersion != globalEDVersion)
                                    continue;
#endif

                // Reads the list of mods that the user has installed that were marked "Required=True" in the .ini file.
                string requiredMods = string.Empty;
                if (File.Exists("fmmRequired.dat"))
                    requiredMods = File.ReadAllText("fmmRequired.dat");

                bool skip = false;
                if (evaluateServer.mods.Count != 0 && requiredMods != string.Empty)
                {
                    // Check each mod that the globalHost requires to have installed...
                    foreach (var mod in evaluateServer.mods)
                    {
                        // If the user doesn't have the mod listed inside "fmmRequired.dat", it needs to be installed.
                        // (it should get added to "fmmRequired.dat" when a mod marked "Require=True" is installed.)
                        if (!requiredMods.Contains(mod))
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip) continue;
                }

                // Tries to choose a server with players, with the best ping, that is in a game.
                // In that priority order. Could probably be refactored to be more readable.
#region "Best Server" logic...
                if (evaluateServer.numPlayers > 0)
                {
                    if (int.Parse(evaluateServer.ping) < int.Parse(bestServer.ping))
                    {
                        if (bestServer.status.ToLower() == "inlobby")
                        {
                            if (evaluateServer.status.ToLower() != "inlobby")
                            {
                                bestServer = evaluateServer;
                            }
                        }
                        else
                        {
                            bestServer = evaluateServer;
                        }
                    }
                }
                else if (bestServer.numPlayers == 0)
                {
                    if (int.Parse(evaluateServer.ping) < int.Parse(bestServer.ping))
                    {
                        if (bestServer.status.ToLower() == "inlobby")
                        {
                            if (evaluateServer.status.ToLower() != "inlobby")
                            {
                                bestServer = evaluateServer;
                            }
                        }
                        else
                        {
                            bestServer = evaluateServer;
                        }
                    }
                }
#endregion
            }

            // Set globalHost to the server that was determined to be "best", and then connect to it (unless no possible matches
            // were found which could be due to ED version mismatches, or no server's that aren't "full".) Notify user if
            // no servers are available.
            if (bestServer.ipAddress != string.Empty) // The ipAddress property in HostServer objects is by default an empty string...
            {
                globalHost = bestServer;
                ConnectToServer();
            }
            else
                richTextBox1.Text = "No server's are available for QuickMatch! This could be because no servers which are " +
                    "online are running the same ElDewrito version as you, the servers are 'passworded', or because " +
                    "the servers are full.";
        }
    }
}
