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
        private PacketCounter packetCounter = new PacketCounter();

        /// <summary>
        /// TextBoxBuffer containing text printed to terminal.
        /// </summary>
        private TextBoxBuffer textBoxBuffer = new TextBoxBuffer(4096);

        /// <summary>
        /// Receive port history
        /// </summary>
        private List<ushort> receivePorts = new List<ushort>();

        private OscListenerManager m_Listener;
        private OscReceiver m_Receiver;
        private Thread m_Thread;

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
            m_Receiver.Close();
            m_Thread.Join();
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
            toolStripStatusLabelPacketsReceived.Text = "Packets Recieved: " + packetCounter.PacketsReceived.ToString();
            toolStripStatusLabelPacketRate.Text = "Packet Rate: " + packetCounter.PacketRate.ToString();
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
            if (m_Receiver != null)
            {
                m_Receiver.Close();
            }
            if (m_Thread != null)
            {
                m_Thread.Join();
            }
            m_Listener = new OscListenerManager();
            m_Receiver = new OscReceiver(port);
            m_Thread = new Thread(new ThreadStart(ListenLoop));
            m_Receiver.Connect();
            m_Thread.Start();
        }

        private void ListenLoop()
        {
            try
            {
                while (m_Receiver.State != OscSocketState.Closed)
                {
                    // if we are in a state to recieve
                    if (m_Receiver.State == OscSocketState.Connected)
                    {
                        // get the next message 
                        // this will block until one arrives or the socket is closed
                        OscPacket packet = m_Receiver.Receive();

                        switch (m_Listener.ShouldInvoke(packet))
                        {
                            case OscPacketInvokeAction.Invoke:
                                packetCounter.Increment();
                                //textBoxBuffer.WriteLine("Received packet");
                                textBoxBuffer.WriteLine(packet.ToString());
                                m_Listener.Invoke(packet);
                                break;
                            case OscPacketInvokeAction.DontInvoke:
                                textBoxBuffer.WriteLine("Cannot invoke");
                                textBoxBuffer.WriteLine(packet.ToString());
                                break;
                            case OscPacketInvokeAction.HasError:
                                textBoxBuffer.WriteLine("Error reading osc packet, " + packet.Error);
                                textBoxBuffer.WriteLine(packet.ErrorMessage);
                                break;
                            case OscPacketInvokeAction.Pospone:
                                textBoxBuffer.WriteLine("Posponed bundle");
                                textBoxBuffer.WriteLine(packet.ToString());
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // if the socket was connected when this happens
                // then tell the user
                if (m_Receiver.State == OscSocketState.Connected)
                {
                    textBoxBuffer.WriteLine("Exception in listen loop");
                    textBoxBuffer.WriteLine(ex.Message);
                }
            }
        }

        #endregion
    }
}
