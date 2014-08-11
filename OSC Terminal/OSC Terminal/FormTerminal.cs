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
        private MessageCounter receiveCounter = new MessageCounter();

        /// <summary>
        /// Sent messages counter.
        /// </summary>
        private MessageCounter sendCounter = new MessageCounter();

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
        /// Send port history
        /// </summary>
        private List<ushort> sendPorts = new List<ushort>();

        /// <summary>
        /// IP address string history
        /// </summary>
        private List<string> ipAddressStrings = new List<string>();

        /// <summary>
        /// OscSender object.
        /// </summary>
        OscSender oscSender;

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

            // Set default send port/IP
            OpenSender(9000, IPAddress.Parse("255.255.255.255"));

            // Populate Send Message drop down list
            toolStripMenuItemSendMessage_DropDownItemClicked_Task("/example, 1.0f, \"Hello World!\", 1, 2, 3");

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
            toolStripStatusLabelTotalReceived.Text = "Total Received: " + receiveCounter.MessageTotal.ToString();
            toolStripStatusLabeReceiveRate.Text = "Receive Rate: " + receiveCounter.MessageRate.ToString();
            toolStripStatusLabelTotalSent.Text = "Total Sent: " + sendCounter.MessageTotal.ToString();
            toolStripStatusLabelSendRate.Text = "Send Rate: " + sendCounter.MessageRate.ToString();
        }

        private void textBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            SendCurrentMessage();
            e.Handled = true;   // don't print character
        }

        #endregion

        #region Menu strip

        /// <summary>
        /// toolStripMenuItemReceivePort DropDownItemClicked event to set the receive port.
        /// </summary>
        private void toolStripMenuItemReceivePort_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            ushort port;
            if (((ToolStripMenuItem)e.ClickedItem).Text == "...")
            {
                FormGetValue formGetValue = new FormGetValue();
                formGetValue.CheckString += (delegate(string currentValue)
                {
                    try
                    {
                        ushort.Parse(currentValue);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });
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
        /// toolStripMenuItemSendPortIP DropDownItemClicked event to set the send port/IP.
        /// </summary>
        private void toolStripMenuItemSendPortIP_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            bool itemIsPort = true;

            // Determine if port or IP item clicked
            foreach (object o in toolStripMenuItemSendPortIP.DropDownItems)
            {
                if (o is ToolStripMenuItem)
                {
                    if (((ToolStripMenuItem)e.ClickedItem) == (ToolStripMenuItem)o)
                    {
                        break;
                    }
                }
                else if (o is ToolStripSeparator)
                {
                    itemIsPort = false;
                    break;
                }
            }

            // Process selected port item
            if (itemIsPort)
            {
                ushort port;
                if (((ToolStripMenuItem)e.ClickedItem).Text == "...")
                {
                    FormGetValue formGetValue = new FormGetValue();
                    formGetValue.CheckString += (delegate(string currentValue)
                    {
                        try
                        {
                            ushort.Parse(currentValue);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    });
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
                OpenSender(port, oscSender.RemoteAddress);
            }

            // Process selected IP item
            if (!itemIsPort)
            {
                IPAddress ipAddress;
                if (((ToolStripMenuItem)e.ClickedItem).Text == "...")
                {
                    FormGetValue formGetValue = new FormGetValue();
                    formGetValue.CheckString += (delegate(string currentValue)
                    {
                        try
                        {
                            IPAddress.Parse(currentValue);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    });
                    formGetValue.ShowDialog();
                    try
                    {
                        if (formGetValue.value == "")
                        {
                            return;
                        }
                        ipAddress = IPAddress.Parse(formGetValue.value);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                        return;
                    }
                }
                else
                {
                    ipAddress = IPAddress.Parse(((ToolStripMenuItem)e.ClickedItem).Text);
                }
                OpenSender((ushort)oscSender.Port, ipAddress);
            }
        }

        /// <summary>
        /// toolStripMenuItemSendMessage DropDownItemClicked event to send selected message.
        /// </summary>
        private void toolStripMenuItemSendMessage_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            toolStripMenuItemSendMessage_DropDownItemClicked_Task(((ToolStripMenuItem)e.ClickedItem).Text);
            SendCurrentMessage();
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
                formGetValue.CheckString += (delegate(string currentValue)
                {
                    try
                    {
                        OscMessage.Parse(currentValue);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });
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
                receiveCounter.Increment();
            }
        }

        #endregion

        #region OSC sender

        private void OpenSender(ushort port, IPAddress ipAddress)
        {

            // Update port/IP list
            if (!sendPorts.Contains(port))
            {
                sendPorts.Add(port);
                sendPorts.Sort();
            }
            if (!ipAddressStrings.Contains(ipAddress.ToString()))
            {
                ipAddressStrings.Add(ipAddress.ToString());
                ipAddressStrings.Sort();
            }
            toolStripMenuItemSendPortIP.DropDownItems.Clear();
            foreach (ushort p in sendPorts)
            {
                toolStripMenuItemSendPortIP.DropDownItems.Add(p.ToString());
            }
            toolStripMenuItemSendPortIP.DropDownItems.Add("...");
            toolStripMenuItemSendPortIP.DropDownItems.Add("-");
            foreach (string s in ipAddressStrings)
            {
                toolStripMenuItemSendPortIP.DropDownItems.Add(s);
            }
            toolStripMenuItemSendPortIP.DropDownItems.Add("...");

            // Check selected port/IP
            foreach (object o in toolStripMenuItemSendPortIP.DropDownItems)
            {
                if (o is ToolStripMenuItem)
                {
                    ToolStripMenuItem toolStripMenuItem = (ToolStripMenuItem)o;
                    if (toolStripMenuItem.Text == port.ToString())
                    {
                        toolStripMenuItem.Checked = true;
                    }
                    if (toolStripMenuItem.Text == ipAddress.ToString())
                    {
                        toolStripMenuItem.Checked = true;
                    }
                }
            }

            // Open sender
            if (oscSender != null)
            {
                oscSender.Close();
            }
            try
            {
                oscSender = new OscSender(ipAddress, port);
                oscSender.Connect();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString());
            }
        }

        private void SendCurrentMessage()
        {
            oscSender.Send(selectedSendMessage);
            sendCounter.Increment();
        }

        #endregion
    }
}
