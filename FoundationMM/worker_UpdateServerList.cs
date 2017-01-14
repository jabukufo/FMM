using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace FoundationMM
{
    public partial class Window : Form
    {
        private void serverBrowserWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            string[] args = (string[])e.Argument;
            string mapsPath = args[0];
            BackgroundWorker worker = sender as BackgroundWorker;

            // Populates the server browser list.
            UpdateServerList();
        }

        private void serverBrowserWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!(e.Error == null))
            {
                percentageLabel.Text = ("Error: " + e.Error.Message);
            }

            // True == the server list is being updated; false == it is not being updated.
            // This is needed so that we can check it before starting the background worker.
            serverRefreshProg = false;
        }
    }
}
