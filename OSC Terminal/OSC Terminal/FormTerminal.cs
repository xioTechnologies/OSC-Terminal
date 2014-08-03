using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.Threading;
using Rug.Osc;

namespace OSC_Terminal
{
    public partial class FormTerminal : Form
    {
        #region Variables and objects

        /// <summary>
        /// Timer to update terminal textbox at fixed interval.
        /// </summary>
        private System.Windows.Forms.Timer formUpdateTimer = new System.Windows.Forms.Timer();

        /// <summary>
        /// Sample counter to calculate performance statics.
        /// </summary>
        private MessageCounter messageCounter = new MessageCounter();

        /// <summary>
        /// TextBoxBuffer containing text printed to terminal.
        /// </summary>
        private TextBoxBuffer textBoxBuffer = new TextBoxBuffer(4096);

        /// <summary>
        /// Receive port history
        /// </summary>
        private List<ushort> receivePorts = new List<ushort>();

        /// <summary>
        /// OscTimeTag Stack for packet deconstruction.
        /// </summary>
        private Stack<OscTimeTag> oscTimeTagStack = new Stack<OscTimeTag>();

        /// <summary>
        /// OscReceiver object.
        /// </summary>
        private OscReceiver oscReceiver;

        /// <summary>
        /// Reciever thread
        /// </summary>
        private Thread thread;

        #endregion

        /// <summary>
        /// Constructor.
        /// </summary>
        public FormTerminal()
        {
            InitializeComponent();
        }

        #region Form load and close

        /// <summary>
        /// From load event.
        /// </summary>
        private void FormTerminal_Load(object sender, EventArgs e)
        {
            // Set form caption
            this.Text = Assembly.GetExecutingAssembly().GetName().Name;

            // Set default port
            OpenReceiver(8000);

            // Setup form update timer
            formUpdateTimer.Interval = 50;
            formUpdateTimer.Tick += new EventHandler(formUpdateTimer_Tick);
            formUpdateTimer.Start();
        }

        private void FormTerminal_FormClosing(object sender, FormClosingEventArgs e)
        {
            oscReceiver.Close();
            thread.Join();
        }

        #endregion

        #region Terminal textbox

        /// <summary>
        /// formUpdateTimer Tick event to update terminal textbox.
        /// </summary>
        void formUpdateTimer_Tick(object sender, EventArgs e)
        {
            // Print textBoxBuffer to terminal
            if (textBox.Enabled && !textBoxBuffer.IsEmpty())
            {
                textBox.AppendText(textBoxBuffer.Get());
                if (textBox.Text.Length > textBox.MaxLength)    // discard first half of textBox when number of characters exceeds length
                {
                    textBox.Text = textBox.Text.Substring(textBox.Text.Length / 2, textBox.Text.Length - textBox.Text.Length / 2);
                }
            }
            else
            {
                textBoxBuffer.Clear();
            }

            // Update sample counter values
            toolStripStatusLabelMessagesReceived.Text = "Messages Recieved: " + messageCounter.MessagesReceived.ToString();
            toolStripStatusLabelMessageRate.Text = "Message Rate: " + messageCounter.MessageRate.ToString();
        }

        #endregion

        #region Menu strip

        /// <summary>
        /// toolStripMenuItemReceivePort DropDownItemClicked event to set the receive port
        /// </summary>
        private void toolStripMenuItemReceivePort_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            ushort port;
            if (((ToolStripMenuItem)e.ClickedItem).Text == "...")
            {
                FormGetValue formGetValue = new FormGetValue();
                formGetValue.ShowDialog();
                try
                {
                    port = ushort.Parse(formGetValue.value);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                    return;
                }
            }
            else
            {
                port = ushort.Parse(((ToolStripMenuItem)e.ClickedItem).Text);
            }
            OpenReceiver(port);
        }

        /// <summary>
        /// toolStripMenuItemEnabled CheckStateChanged event to toggle enabled state of the terminal text box.
        /// </summary>
        private void toolStripMenuItemEnabled_CheckStateChanged(object sender, EventArgs e)
        {
            if (toolStripMenuItemEnabled.Checked)
            {
                textBox.Enabled = true;
            }
            else
            {
                textBox.Enabled = false;
            }
        }

        /// <summary>
        /// toolStripMenuItemClear Click event to clear terminal text box.
        /// </summary>
        private void toolStripMenuItemClear_Click(object sender, EventArgs e)
        {
            textBox.Text = "";
        }

        /// <summary>
        /// toolStripMenuItemAbout Click event to display version details.
        /// </summary>
        private void toolStripMenuItemAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Assembly.GetExecutingAssembly().GetName().Name + " " + Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "." + Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString(), "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// toolStripMenuItemSourceCode Click event to open web browser.
        /// </summary>
        private void toolStripMenuItemSourceCode_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://github.com/xioTechnologies/OSC-Terminal");
            }
            catch { }
        }

        #endregion

        #region OSC receiver

        private void OpenReceiver(ushort port)
        {
            // Update port list
            if (!receivePorts.Contains(port))
            {
                receivePorts.Add(port);
                receivePorts.Sort();
            }
            toolStripMenuItemReceivePort.DropDownItems.Clear();
            foreach (ushort p in receivePorts)
            {
                toolStripMenuItemReceivePort.DropDownItems.Add(p.ToString());
            }
            toolStripMenuItemReceivePort.DropDownItems.Add("...");

            // Check selected port
            foreach (ToolStripMenuItem toolStripMenuItem in toolStripMenuItemReceivePort.DropDownItems)
            {
                if (toolStripMenuItem.Text == port.ToString())
                {
                    toolStripMenuItem.Checked = true;
                }
            }

            // Open reciever
            if (oscReceiver != null)
            {
                oscReceiver.Close();
            }
            if (thread != null)
            {
                thread.Join();
            }
            oscReceiver = new OscReceiver(port);
            thread = new Thread(new ThreadStart(delegate()
            {
                try
                {
                    while (oscReceiver.State != OscSocketState.Closed)
                    {
                        if (oscReceiver.State == OscSocketState.Connected)
                        {
                            DeconstructPacket(oscReceiver.Receive());
                        }
                    }
                }
                catch { }
            }));
            oscReceiver.Connect();
            thread.Start();
        }

        private void DeconstructPacket(OscPacket oscPacket)
        {
            if (oscPacket is OscBundle)
            {
                OscBundle oscBundle = (OscBundle)oscPacket;
                oscTimeTagStack.Push(oscBundle.Timestamp);
                foreach (OscPacket bundleElement in oscBundle)
                {
                    DeconstructPacket(bundleElement);
                }
                oscTimeTagStack.Pop();
            }
            else if (oscPacket is OscMessage)
            {
                OscMessage oscMessage = (OscMessage)oscPacket;
                if (oscTimeTagStack.Count > 0)
                {
                    OscTimeTag oscTimeTag = oscTimeTagStack.Peek();
                    textBoxBuffer.WriteLine(oscTimeTag.ToString() + " " + oscMessage.ToString());
                }
                else
                {
                    textBoxBuffer.WriteLine(oscMessage.ToString());
                }
                messageCounter.Increment();
            }
        }

        #endregion
    }
}
