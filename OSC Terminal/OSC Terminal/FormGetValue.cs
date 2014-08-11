using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OSC_Terminal
{
    /// <summary>
    /// Callback function to validate current string value.
    /// </summary>
    /// <param name="currentValue">
    /// Currenst string value.
    /// </param>
    /// <returns>
    /// True if current string is valid.
    /// </returns>
    /// <remarks>
    /// http://msdn.microsoft.com/en-us/library/843s5s5x.aspx
    /// </remarks>
    public delegate bool CallBack(string currentValue);

    /// <summary>
    /// Dialog form to get text value from user.
    /// </summary>
    public partial class FormGetValue : Form
    {
        /// <summary>
        /// Value entered by user.
        /// </summary>
        public string value { get; private set; }

        /// <summary>
        /// Callback function to validate current string value.
        /// </summary>
        public CallBack CheckString { get; set; }

        /// <summary>
        /// Flag indicating if entered value is valid.
        /// </summary>
        private bool valid;

        /// <summary>
        /// Constructor.
        /// </summary>
        public FormGetValue()
        {
            value = "";
            CheckString = null;
            valid = true;
            InitializeComponent();
        }

        /// <summary>
        /// textBoxValue TextChanged event to validate string through callback function.
        /// </summary>
        private void textBoxValue_TextChanged(object sender, EventArgs e)
        {
            if (CheckString != null)
            {
                if (CheckString(textBoxValue.Text))
                {
                    valid = true;
                    buttonOK.Enabled = true;
                    textBoxValue.ForeColor = Color.Black;
                }
                else
                {
                    valid = false;
                    buttonOK.Enabled = false;
                    textBoxValue.ForeColor = Color.Red;
                }
            }
        }

        /// <summary>
        /// textBoxValue KeyPress event to close form when Enter key pressed.
        /// </summary>
        private void textBoxValue_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r' && valid)
            {
                value = textBoxValue.Text;
                Close();
            }
        }

        /// <summary>
        /// buttonOK Click event to close form.
        /// </summary>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            value = textBoxValue.Text;
            Close();
        }
    }
}
