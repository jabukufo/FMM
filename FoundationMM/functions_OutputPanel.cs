using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FoundationMM
{
    public partial class Window : Form
    {
        public delegate void appendNewOutputCallback(object control, dynamic output);
        public delegate void showMessageBoxCallback(string output);

        // TODO: Clean this stuff up
        private void appendNewOutput(object control, dynamic output)
        {
            try
            {
                ((TextBox)control).AppendText(output + Environment.NewLine);
            }
            catch
            {
                try
                {
                    ((RichTextBox)control).Text = output + Environment.NewLine;
                }
                catch
                {
                    try
                    {
                        ((Button)control).Enabled = output;
                    }
                    catch { }
                }
            }
        }


        private void showMessageBox(string output)
        {
            FlashWindowEx(this);
            MessageBox.Show(output);
        }


        // following code not in use (yet) :p

        public delegate void appendNewLogCallback(string output);

        private void _appendNewLog(string output)
        {
            //debugTextBox.AppendText(output + Environment.NewLine);
        }
        
        private void Log(string output)
        {
            //debugTextBox.Invoke(new appendNewLogCallback(this._appendNewLog), new object[] { output });
        }
    }
}
