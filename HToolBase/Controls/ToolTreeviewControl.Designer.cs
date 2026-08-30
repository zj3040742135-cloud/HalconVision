namespace HToolBase.Controls
{
    partial class ToolTreeviewControl
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
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode("输入");
            System.Windows.Forms.TreeNode treeNode2 = new System.Windows.Forms.TreeNode("工具");
            System.Windows.Forms.TreeNode treeNode3 = new System.Windows.Forms.TreeNode("输出");
            this.treeView1 = new ConnectionTreeView();
            this.SuspendLayout();
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            treeNode1.Name = "输入";
            treeNode1.Text = "输入";
            treeNode2.Name = "工具";
            treeNode2.Text = "工具";
            treeNode3.Name = "输出";
            treeNode3.Text = "输出";
            this.treeView1.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode1,
            treeNode2,
            treeNode3});
            this.treeView1.Size = new System.Drawing.Size(577, 624);
            this.treeView1.TabIndex = 0;
            // 
            // ToolTreeviewControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.treeView1);
            this.Name = "ToolTreeviewControl";
            this.Size = new System.Drawing.Size(577, 624);
            this.ResumeLayout(false);

        }

        #endregion

        private ConnectionTreeView treeView1;
    }
}
