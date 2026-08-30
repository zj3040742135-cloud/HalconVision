using HToolBase.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HToolBase.Controls
{
    public partial class BlobToolForm : Form
    {
        BlobTool tool;
        public BlobToolForm(BlobTool blobTool)
        {
            InitializeComponent();
            tool = blobTool;
            this.Load += BlobToolForm_Load;
            this.FormClosed += BlobToolForm_FormClosed;
            tool.UpdataImage += this.run;


        }

        private void BlobToolForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            tool.UpdataImage -= this.run;
           
        }

        private void BlobToolForm_Load(object sender, EventArgs e)
        {
            this.hDisplayControl1.ShowImage(tool.InputImage);
        }

        public void run()
        {
            this.hDisplayControl1.ShowImage(tool.InputImage);
        }
        

    }
}
