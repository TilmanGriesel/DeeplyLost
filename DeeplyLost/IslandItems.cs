using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DeeplyLost
{
    public partial class IslandItems : Form
    {
        public IslandItems()
        {
            InitializeComponent();
        }

        public void SetTitle(string title)
        {
            this.Text = title;
        }

        public void AddGridData(IDictionary<string, int> items)
        {
            foreach (KeyValuePair<string, int> kvp in items)
            {
                this.dataGridView.Rows.Add(kvp.Key, kvp.Value);
            }
        }
    }
}
