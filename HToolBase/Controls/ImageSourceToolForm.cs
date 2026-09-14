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
            dialog.Title = "选择文件（可多选，运行时按顺序循环读取）";
            dialog.Filter = "图片|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|所有文件(*.*)|*.*";
            dialog.Multiselect = true;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (dialog.FileNames.Length > 1)
                {
                    // 多选：保存序列，每运行一次自动读取下一张（循环）
                    _tool.ImageList = dialog.FileNames.ToList();
                    _tool.ImageStr = dialog.FileNames[0];
                }
                else
                {
                    // 单选：保持原单张行为（每次运行读同一张）
                    _tool.ImageList = new List<string>();
                    _tool.ImageStr = dialog.FileName;
                }
            }
        }
    }
}
