using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeeplyLost
{
    public partial class RenameIslandNode : Form
    {
        public RenameIslandNode()
        {
            InitializeComponent();
        }

        public string NodeName
        {
            get { return this.textBoxNodeName.Text; }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void textBoxNodeName_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

    }
}
