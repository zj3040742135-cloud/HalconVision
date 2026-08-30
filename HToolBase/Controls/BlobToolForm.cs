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
    public partial class BlobToolForm : HForm
    {
        BlobTool blobtool;
        public override ToolBase tool
        {
            get { return blobtool; }
            set
            {
                blobtool = (BlobTool)value;
                this.Text = blobtool.RootNode.Text;
                UnBindEvent();
                BindingEvent();
            }
        }
        public BlobToolForm()
        {
            InitializeComponent();
            blobtool=new BlobTool();
            this.Load += BlobToolForm_Load;
            this.FormClosed += BlobToolForm_FormClosed;
            blobtool.UpdataImage += this.run;


        }
        public override void BindingEvent() { blobtool.UpdataImage += this.run; }
        public override void UnBindEvent() { blobtool.UpdataImage -= this.run; }
        private void BlobToolForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            blobtool.UpdataImage -= this.run;
           
        }

        private void BlobToolForm_Load(object sender, EventArgs e)
        {
            this.hDisplayControl1.ShowImage(blobtool.InputImage);
        }

        public void run()
        {
            this.hDisplayControl1.ShowImage(blobtool.InputImage);
        }
        

    }
}
