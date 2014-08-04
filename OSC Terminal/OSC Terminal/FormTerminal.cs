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
using System.Net;

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
        /// Received messages counter.
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
        /// Receiver thread
        /// </summary>
        private Thread thread;

        /// <summary>
        /// Sent messages history.
        /// </summary>
        private List<string> sentMessageStrings = new List<string>();

        /// <summary>
        /// Selected message to send.
        /// </summary>
        private OscMessage selectedSendMessage;

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

            // Set default receive port
            OpenReceiver(8000);

            // Populate Send Message drop down list
            toolStripMenuItemSendMessage_DropDownItemClicked_Task("/example, 1.0f \"Hello World!\" 1 2 3");

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
            toolStripStatusLabelMessagesReceived.Text = "Messages Received: " + messageCounter.MessagesReceived.ToString();
            toolStripStatusLabelMessageRate.Text = "Message Rate: " + messageCounter.MessageRate.ToString();
        }

        private void textBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            SendCurrentMessage();
            e.Handled = true;   // don't print character
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
                    if (formGetValue.value == "")
                    {
                        return;
                    }
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
        /// toolStripMenuItemSendMessage DropDownItemClicked event to send selected message
        /// </summary>
        private void toolStripMenuItemSendMessage_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            toolStripMenuItemSendMessage_DropDownItemClicked_Task(((ToolStripMenuItem)e.ClickedItem).Text);

        }

        /// <summary>
        /// toolStripMenuItemSendMessage DropDownItemClicked task called by event or programmatically.
        /// </summary>
        /// <param name="text">
        /// DropDownItem text to be processed.
        /// </param>
        private void toolStripMenuItemSendMessage_DropDownItemClicked_Task(string text)
        {

            // Create message from string
            string oscMessageString;
            if (text == "...")
            {
                FormGetValue formGetValue = new FormGetValue();
                formGetValue.ShowDialog();
                try
                {
                    if (formGetValue.value == "")
                    {
                        return;
                    }
                    OscMessage.Parse(formGetValue.value);
                    oscMessageString = formGetValue.value;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                    return;
                }
            }
            else
            {
                oscMessageString = text;
            }

            // Update message list
            if (!sentMessageStrings.Contains(oscMessageString))
            {
                sentMessageStrings.Add(oscMessageString);
                sentMessageStrings.Sort();
            }
            toolStripMenuItemSendMessage.DropDownItems.Clear();
            foreach (string s in sentMessageStrings)
            {
                toolStripMenuItemSendMessage.DropDownItems.Add(s);
            }
            toolStripMenuItemSendMessage.DropDownItems.Add("...");

            // Check selected port
            foreach (ToolStripMenuItem toolStripMenuItem in toolStripMenuItemSendMessage.DropDownItems)
            {
                if (toolStripMenuItem.Text == oscMessageString)
                {
                    toolStripMenuItem.Checked = true;
                }
            }

            // Set selected send message
            selectedSendMessage = OscMessage.Parse(oscMessageString);
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

            // Open receiver
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

        #region OSC sender

        private void SendCurrentMessage()
        {
            OscSender m_Sender;
            m_Sender = new OscSender(IPAddress.Parse("255.255.255.255"), 9000);
            m_Sender.Connect();
            m_Sender.Send(selectedSendMessage);
            m_Sender.Close(); 
        }

        #endregion
    }
}
