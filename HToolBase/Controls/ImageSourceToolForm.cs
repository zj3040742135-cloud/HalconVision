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
    public partial class ImageSourceToolForm : HForm
    {
        ImageSourceTool _tool;
        public override ToolBase tool
        {
            get { return _tool; }
            set
            {
                _tool = (ImageSourceTool)value;
                this.Text = _tool.RootNode.Text;
                UnBindEvent();
                BindingEvent();
            }
        }
        public ImageSourceToolForm()
        {
            InitializeComponent();
            _tool = new ImageSourceTool();
        }
        public override void BindingEvent() { _tool.UpdataImage += this.Run; }
        public override void UnBindEvent() { _tool.UpdataImage -= this.Run; }
        public void Run()
        {
            if(_tool.OutputImage.IsInitialized())
            {
                this.hDisplayControl1.ShowImage(_tool.OutputImage);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _tool.Run();
        }

        private void OpenFold_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "选择文件";
            dialog.Filter = "图片|*.jpg|所有文件(*.*)|*.*";
            string Text = this.Name;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _tool.ImageStr = dialog.FileName;
            }
        }
    }
}
