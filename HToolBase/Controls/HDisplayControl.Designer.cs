namespace HToolBase.Controls
{
    partial class HDisplayControl
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            HalconWindow.Dispose();
            this.ClearEvent();
            image.Dispose();
            // 释放多叠加层 Data HObject（SetOverlays 深拷贝独占，可安全释放）
            if (_overlays != null)
            {
                foreach (var o in _overlays)
                {
                    if (o.Data is HalconDotNet.HObject h && h.IsInitialized()) { try { h.Dispose(); } catch { } }
                }
                _overlays.Clear();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.hWindow = new HalconDotNet.HWindowControl();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.hWindow);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(597, 707);
            this.panel1.TabIndex = 0;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.textBox1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 682);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(597, 25);
            this.panel2.TabIndex = 1;
            // 
            // textBox1
            // 
            this.textBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox1.Location = new System.Drawing.Point(0, 0);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(597, 21);
            this.textBox1.TabIndex = 0;
            // 
            // hWindow
            // 
            this.hWindow.BackColor = System.Drawing.Color.Gray;
            this.hWindow.BorderColor = System.Drawing.Color.Gray;
            this.hWindow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hWindow.ImagePart = new System.Drawing.Rectangle(0, 0, 640, 480);
            this.hWindow.Location = new System.Drawing.Point(0, 0);
            this.hWindow.Name = "hWindow";
            this.hWindow.Size = new System.Drawing.Size(597, 707);
            this.hWindow.TabIndex = 0;
            this.hWindow.WindowSize = new System.Drawing.Size(597, 707);
            // 
            // HDisplayControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Name = "HDisplayControl";
            this.Size = new System.Drawing.Size(597, 707);
            this.panel1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private HalconDotNet.HWindowControl hWindow;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.TextBox textBox1;
    }
}
