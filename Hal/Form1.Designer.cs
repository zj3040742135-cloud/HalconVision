namespace Hal
{
    partial class Form1
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

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.User = new System.Windows.Forms.Button();
            this.Logout = new System.Windows.Forms.Button();
            this.Login = new System.Windows.Forms.Button();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.SetSystem = new System.Windows.Forms.Button();
            this.DisplayManger = new System.Windows.Forms.Button();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.button3 = new System.Windows.Forms.Button();
            this.ProjectManger = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabControl3 = new System.Windows.Forms.TabControl();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.tabControl2 = new System.Windows.Forms.TabControl();
            this.panel3 = new System.Windows.Forms.Panel();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.Add = new System.Windows.Forms.Button();
            this.Run = new System.Windows.Forms.Button();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1255, 71);
            this.tabControl1.TabIndex = 1;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.User);
            this.tabPage1.Controls.Add(this.Logout);
            this.tabPage1.Controls.Add(this.Login);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(1247, 45);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "开始";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // User
            // 
            this.User.Image = ((System.Drawing.Image)(resources.GetObject("User.Image")));
            this.User.Location = new System.Drawing.Point(99, 3);
            this.User.Name = "User";
            this.User.Size = new System.Drawing.Size(42, 40);
            this.User.TabIndex = 0;
            this.User.UseVisualStyleBackColor = true;
            // 
            // Logout
            // 
            this.Logout.Image = ((System.Drawing.Image)(resources.GetObject("Logout.Image")));
            this.Logout.Location = new System.Drawing.Point(51, 3);
            this.Logout.Name = "Logout";
            this.Logout.Size = new System.Drawing.Size(42, 40);
            this.Logout.TabIndex = 0;
            this.Logout.UseVisualStyleBackColor = true;
            // 
            // Login
            // 
            this.Login.BackColor = System.Drawing.Color.Gray;
            this.Login.Font = new System.Drawing.Font("宋体", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Login.ForeColor = System.Drawing.SystemColors.Control;
            this.Login.Image = ((System.Drawing.Image)(resources.GetObject("Login.Image")));
            this.Login.Location = new System.Drawing.Point(3, 3);
            this.Login.Name = "Login";
            this.Login.Size = new System.Drawing.Size(42, 40);
            this.Login.TabIndex = 0;
            this.Login.Text = "用户登录";
            this.Login.UseVisualStyleBackColor = false;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.SetSystem);
            this.tabPage2.Controls.Add(this.DisplayManger);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(1247, 45);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "设置";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // SetSystem
            // 
            this.SetSystem.Dock = System.Windows.Forms.DockStyle.Left;
            this.SetSystem.Location = new System.Drawing.Point(76, 3);
            this.SetSystem.Name = "SetSystem";
            this.SetSystem.Size = new System.Drawing.Size(73, 39);
            this.SetSystem.TabIndex = 1;
            this.SetSystem.Text = "系统设置";
            this.SetSystem.UseVisualStyleBackColor = true;
            // 
            // DisplayManger
            // 
            this.DisplayManger.Dock = System.Windows.Forms.DockStyle.Left;
            this.DisplayManger.Location = new System.Drawing.Point(3, 3);
            this.DisplayManger.Name = "DisplayManger";
            this.DisplayManger.Size = new System.Drawing.Size(73, 39);
            this.DisplayManger.TabIndex = 0;
            this.DisplayManger.Text = "显示管理";
            this.DisplayManger.UseVisualStyleBackColor = true;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.button3);
            this.tabPage3.Controls.Add(this.ProjectManger);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(1247, 45);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "产品";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // button3
            // 
            this.button3.BackColor = System.Drawing.Color.LightGray;
            this.button3.Dock = System.Windows.Forms.DockStyle.Left;
            this.button3.Location = new System.Drawing.Point(61, 0);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(61, 45);
            this.button3.TabIndex = 1;
            this.button3.Text = "保存";
            this.button3.UseVisualStyleBackColor = false;
            this.button3.Click += new System.EventHandler(this.Save_Click);
            // 
            // ProjectManger
            // 
            this.ProjectManger.BackColor = System.Drawing.Color.LightGray;
            this.ProjectManger.Dock = System.Windows.Forms.DockStyle.Left;
            this.ProjectManger.Location = new System.Drawing.Point(0, 0);
            this.ProjectManger.Name = "ProjectManger";
            this.ProjectManger.Size = new System.Drawing.Size(61, 45);
            this.ProjectManger.TabIndex = 0;
            this.ProjectManger.Text = "产品管理";
            this.ProjectManger.UseVisualStyleBackColor = false;
            this.ProjectManger.Click += new System.EventHandler(this.ProjectManger_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 71);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tabControl3);
            this.splitContainer1.Panel1.Controls.Add(this.textBox1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.tabControl2);
            this.splitContainer1.Panel2.Controls.Add(this.panel3);
            this.splitContainer1.Size = new System.Drawing.Size(1255, 794);
            this.splitContainer1.SplitterDistance = 524;
            this.splitContainer1.TabIndex = 2;
            // 
            // tabControl3
            // 
            this.tabControl3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl3.Location = new System.Drawing.Point(0, 0);
            this.tabControl3.Name = "tabControl3";
            this.tabControl3.SelectedIndex = 0;
            this.tabControl3.Size = new System.Drawing.Size(524, 667);
            this.tabControl3.TabIndex = 3;
            // 
            // textBox1
            // 
            this.textBox1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.textBox1.Location = new System.Drawing.Point(0, 667);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(524, 127);
            this.textBox1.TabIndex = 2;
            // 
            // tabControl2
            // 
            this.tabControl2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl2.Location = new System.Drawing.Point(0, 30);
            this.tabControl2.Name = "tabControl2";
            this.tabControl2.SelectedIndex = 0;
            this.tabControl2.Size = new System.Drawing.Size(727, 764);
            this.tabControl2.TabIndex = 0;
            // 
            // panel3
            // 
            this.panel3.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.panel3.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel3.Controls.Add(this.button2);
            this.panel3.Controls.Add(this.button1);
            this.panel3.Controls.Add(this.Add);
            this.panel3.Controls.Add(this.Run);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel3.Location = new System.Drawing.Point(0, 0);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(727, 30);
            this.panel3.TabIndex = 1;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(414, 2);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(27, 23);
            this.button2.TabIndex = 2;
            this.button2.Text = "属性";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(63, 4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(27, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "属性";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // Add
            // 
            this.Add.Image = ((System.Drawing.Image)(resources.GetObject("Add.Image")));
            this.Add.Location = new System.Drawing.Point(35, 3);
            this.Add.Name = "Add";
            this.Add.Size = new System.Drawing.Size(22, 23);
            this.Add.TabIndex = 0;
            this.Add.UseVisualStyleBackColor = true;
            // 
            // Run
            // 
            this.Run.Image = ((System.Drawing.Image)(resources.GetObject("Run.Image")));
            this.Run.Location = new System.Drawing.Point(7, 3);
            this.Run.Name = "Run";
            this.Run.Size = new System.Drawing.Size(22, 23);
            this.Run.TabIndex = 0;
            this.Run.UseVisualStyleBackColor = true;
            this.Run.Click += new System.EventHandler(this.Run_Click_1);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1255, 865);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.tabControl1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.Button User;
        private System.Windows.Forms.Button Logout;
        private System.Windows.Forms.Button Login;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Button SetSystem;
        private System.Windows.Forms.Button DisplayManger;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button ProjectManger;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TabControl tabControl3;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TabControl tabControl2;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button Add;
        private System.Windows.Forms.Button Run;
    }
}

