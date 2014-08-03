using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OSC_Terminal
{
    /// <summary>
    /// Message counter. Tracks number of messages received and message rate.
    /// </summary>
    class MessageCounter
    {
        /// <summary>
        /// Timer to calculate message rate.
        /// </summary>
        private System.Windows.Forms.Timer timer;

        /// <summary>
        /// Number of messages received.
        /// </summary>
        public int MessagesReceived { get; private set; }

        /// <summary>
        /// Message receive rate as messages per second.
        /// </summary>
        public int MessageRate { get; private set; }

        /// <summary>
        /// Used to calculate message rate.
        /// </summary>
        private DateTime prevTime;

        /// <summary>
        /// Used to calculate message rate.
        /// </summary>
        private int prevMessagesReceived;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MessageCounter()
        {
            // Initialise variables
            prevMessagesReceived = 0;
            MessagesReceived = 0;

            // Setup timer
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += new EventHandler(timer_Tick);
            timer.Start();
        }

        /// <summary>
        /// Increments message counter.
        /// </summary>
        public void Increment()
        {
            MessagesReceived++;
        }

        // Zeros message counter.
        public void Reset()
        {
            prevMessagesReceived = 0;
            MessagesReceived = 0;
            MessageRate = 0;
        }

        /// <summary>
        /// timer Tick event to calculate message rate.
        /// </summary>
        void timer_Tick(object sender, EventArgs e)
        {
            DateTime nowTime = DateTime.Now;
            TimeSpan t = nowTime - prevTime;
            prevTime = nowTime;
            MessageRate = (int)((float)(MessagesReceived - prevMessagesReceived) / ((float)t.Seconds + (float)t.Milliseconds * 0.001f));
            prevMessagesReceived = MessagesReceived;
        }
    }
}