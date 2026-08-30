namespace HToolBase.Controls
{
    partial class AddToolForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode("ImageSourceTool");
            System.Windows.Forms.TreeNode treeNode2 = new System.Windows.Forms.TreeNode("图像源", new System.Windows.Forms.TreeNode[] {
            treeNode1});
            System.Windows.Forms.TreeNode treeNode3 = new System.Windows.Forms.TreeNode("BlobTool");
            System.Windows.Forms.TreeNode treeNode4 = new System.Windows.Forms.TreeNode("查找", new System.Windows.Forms.TreeNode[] {
            treeNode3});
            System.Windows.Forms.TreeNode treeNode5 = new System.Windows.Forms.TreeNode("创建");
            System.Windows.Forms.TreeNode treeNode6 = new System.Windows.Forms.TreeNode("模板匹配");
            System.Windows.Forms.TreeNode treeNode7 = new System.Windows.Forms.TreeNode("距离测量");
            System.Windows.Forms.TreeNode treeNode8 = new System.Windows.Forms.TreeNode("ToolBlock");
            System.Windows.Forms.TreeNode treeNodeScript = new System.Windows.Forms.TreeNode("ScriptTool");
            System.Windows.Forms.TreeNode treeNode9 = new System.Windows.Forms.TreeNode("通用工具", new System.Windows.Forms.TreeNode[] {
            treeNode8,
            treeNodeScript});
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.SuspendLayout();
            //
            // treeView1
            //
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            treeNode1.Name = "节点5";
            treeNode1.Text = "ImageSourceTool";
            treeNode2.Name = "节点4";
            treeNode2.Text = "图像源";
            treeNode3.Name = "BlobTool";
            treeNode3.Text = "BlobTool";
            treeNode4.Name = "节点0";
            treeNode4.Text = "查找";
            treeNode5.Name = "节点1";
            treeNode5.Text = "创建";
            treeNode6.Name = "节点2";
            treeNode6.Text = "模板匹配";
            treeNode7.Name = "节点3";
            treeNode7.Text = "距离测量";
            treeNode8.Name = "节点1";
            treeNode8.Text = "ToolBlock";
            treeNodeScript.Name = "ScriptTool";
            treeNodeScript.Text = "ScriptTool";
            treeNode9.Name = "节点0";
            treeNode9.Text = "通用工具";
            this.treeView1.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode2,
            treeNode4,
            treeNode5,
            treeNode6,
            treeNode7,
            treeNode9});
            this.treeView1.Size = new System.Drawing.Size(410, 592);
            this.treeView1.TabIndex = 0;
            // 
            // AddToolForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(410, 592);
            this.Controls.Add(this.treeView1);
            this.Name = "AddToolForm";
            this.Text = "AddToolForm";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TreeView treeView1;
    }
}