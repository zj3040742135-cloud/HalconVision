﻿﻿﻿﻿﻿﻿﻿﻿namespace HToolBase.Controls
{
    partial class ToolBlockControl
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
            if (disposing)
            {
                // 取消订阅ToolBlock事件，避免控件销毁后旧的事件处理器干扰下次打开
                if (ToolBlock != null)
                {
                    ToolBlock.AddInputEvent -= ToolBlock_AddInputEvent;
                    ToolBlock.AddOutputEvent -= ToolBlock_AddOutputEvent;
                    // 取消订阅 RunCompleted（外部 ToolBlock.Run 触发 UI 刷新的事件）
                    ToolBlock.RunCompleted -= ToolBlock_RunCompleted;
                    // 取消订阅端口值变化事件
                    foreach (var input in ToolBlock.Inputs.Values)
                    {
                        input.ValueChanged -= InputPort_ValueChanged;
                    }
                    foreach (var output in ToolBlock.Outputs.Values)
                    {
                        output.ValueChanged -= OutputPort_ValueChanged;
                    }

                    // 从父控件中移除toolTreeview，防止base.Dispose递归销毁它。
                    // toolTreeview由ToolBlock管理生命周期，不应随窗体一起销毁，
                    // 否则下次打开窗体访问toolTreeview时会抛ObjectDisposedException。
                    if (toolTreeview != null && toolTreeview.Parent != null)
                    {
                       
                        toolTreeview.Parent.Controls.Remove(toolTreeview); toolTreeview?.Dispose();
                    }
                }
                if (components != null)
                {
                    components.Dispose();
                }
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.panel2 = new System.Windows.Forms.Panel();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.panel4 = new System.Windows.Forms.Panel();
            this.OutputGridView2 = new System.Windows.Forms.DataGridView();
            this.OutputName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.OutputType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.OutputValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel6 = new System.Windows.Forms.Panel();
            this.button7 = new System.Windows.Forms.Button();
            this.button5 = new System.Windows.Forms.Button();
            this.panel3 = new System.Windows.Forms.Panel();
            this.InputGridView1 = new System.Windows.Forms.DataGridView();
            this.InputName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.InputType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.InputValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel5 = new System.Windows.Forms.Panel();
            this.button6 = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.overlayGridView = new System.Windows.Forms.DataGridView();
            this.colOverlayVisible = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colOverlayTool = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colOverlayName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colOverlayType = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colOverlayColor = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colOverlayDraw = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colOverlayLineWidth = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel9 = new System.Windows.Forms.Panel();
            this.button9 = new System.Windows.Forms.Button();
            this.cbOnlyCurrentTool = new System.Windows.Forms.CheckBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.button8 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.panel8 = new System.Windows.Forms.Panel();
            this.panel7 = new System.Windows.Forms.Panel();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.hDisplayControl1 = new HToolBase.Controls.HDisplayControl();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.panel4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.OutputGridView2)).BeginInit();
            this.panel6.SuspendLayout();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.InputGridView1)).BeginInit();
            this.panel5.SuspendLayout();
            this.tabPage3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.overlayGridView)).BeginInit();
            this.panel9.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel8.SuspendLayout();
            this.panel7.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.panel2);
            this.splitContainer1.Panel1.Controls.Add(this.panel1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.panel8);
            this.splitContainer1.Panel2.Controls.Add(this.panel7);
            this.splitContainer1.Size = new System.Drawing.Size(1123, 899);
            this.splitContainer1.SplitterDistance = 430;
            this.splitContainer1.TabIndex = 5;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.tabControl1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(0, 29);
            this.panel2.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(426, 866);
            this.panel2.TabIndex = 5;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(426, 866);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Location = new System.Drawing.Point(4, 25);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPage1.Size = new System.Drawing.Size(418, 837);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "工具";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.panel4);
            this.tabPage2.Controls.Add(this.panel3);
            this.tabPage2.Location = new System.Drawing.Point(4, 25);
            this.tabPage2.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPage2.Size = new System.Drawing.Size(418, 837);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "输入/输出";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // panel4
            // 
            this.panel4.Controls.Add(this.OutputGridView2);
            this.panel4.Controls.Add(this.panel6);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel4.Location = new System.Drawing.Point(4, 413);
            this.panel4.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(410, 420);
            this.panel4.TabIndex = 1;
            // 
            // OutputGridView2
            // 
            this.OutputGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.OutputGridView2.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.OutputName,
            this.OutputType,
            this.OutputValue});
            this.OutputGridView2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.OutputGridView2.Location = new System.Drawing.Point(0, 29);
            this.OutputGridView2.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.OutputGridView2.Name = "OutputGridView2";
            this.OutputGridView2.RowHeadersWidth = 51;
            this.OutputGridView2.RowTemplate.Height = 23;
            this.OutputGridView2.Size = new System.Drawing.Size(410, 391);
            this.OutputGridView2.TabIndex = 1;
            // 
            // OutputName
            // 
            this.OutputName.HeaderText = "名称";
            this.OutputName.MinimumWidth = 6;
            this.OutputName.Name = "OutputName";
            this.OutputName.Width = 125;
            // 
            // OutputType
            // 
            this.OutputType.HeaderText = "类型";
            this.OutputType.MinimumWidth = 6;
            this.OutputType.Name = "OutputType";
            this.OutputType.Width = 125;
            // 
            // OutputValue
            // 
            this.OutputValue.HeaderText = "值";
            this.OutputValue.MinimumWidth = 6;
            this.OutputValue.Name = "OutputValue";
            this.OutputValue.Width = 125;
            // 
            // panel6
            // 
            this.panel6.Controls.Add(this.button7);
            this.panel6.Controls.Add(this.button5);
            this.panel6.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel6.Location = new System.Drawing.Point(0, 0);
            this.panel6.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.panel6.Name = "panel6";
            this.panel6.Size = new System.Drawing.Size(410, 29);
            this.panel6.TabIndex = 0;
            // 
            // button7
            // 
            this.button7.Dock = System.Windows.Forms.DockStyle.Left;
            this.button7.Location = new System.Drawing.Point(36, 0);
            this.button7.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button7.Name = "button7";
            this.button7.Size = new System.Drawing.Size(29, 29);
            this.button7.TabIndex = 1;
            this.button7.Text = "-";
            this.button7.UseVisualStyleBackColor = true;
            this.button7.Click += new System.EventHandler(this.button7_Click);
            // 
            // button5
            // 
            this.button5.Dock = System.Windows.Forms.DockStyle.Left;
            this.button5.Location = new System.Drawing.Point(0, 0);
            this.button5.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button5.Name = "button5";
            this.button5.Size = new System.Drawing.Size(36, 29);
            this.button5.TabIndex = 0;
            this.button5.Text = "+";
            this.button5.UseVisualStyleBackColor = true;
            this.button5.Click += new System.EventHandler(this.button5_Click);
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.InputGridView1);
            this.panel3.Controls.Add(this.panel5);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel3.Location = new System.Drawing.Point(4, 4);
            this.panel3.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(410, 409);
            this.panel3.TabIndex = 0;
            // 
            // InputGridView1
            // 
            this.InputGridView1.AllowUserToDeleteRows = false;
            this.InputGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.InputGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.InputName,
            this.InputType,
            this.InputValue});
            this.InputGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.InputGridView1.Location = new System.Drawing.Point(0, 29);
            this.InputGridView1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.InputGridView1.Name = "InputGridView1";
            this.InputGridView1.RowHeadersWidth = 51;
            this.InputGridView1.RowTemplate.Height = 23;
            this.InputGridView1.Size = new System.Drawing.Size(410, 380);
            this.InputGridView1.TabIndex = 1;
            // 
            // InputName
            // 
            this.InputName.HeaderText = "名称";
            this.InputName.MinimumWidth = 6;
            this.InputName.Name = "InputName";
            this.InputName.Width = 125;
            // 
            // InputType
            // 
            this.InputType.HeaderText = "类型";
            this.InputType.MinimumWidth = 6;
            this.InputType.Name = "InputType";
            this.InputType.Width = 125;
            // 
            // InputValue
            // 
            this.InputValue.HeaderText = "值";
            this.InputValue.MinimumWidth = 6;
            this.InputValue.Name = "InputValue";
            this.InputValue.Width = 125;
            // 
            // panel5
            // 
            this.panel5.Controls.Add(this.button6);
            this.panel5.Controls.Add(this.button4);
            this.panel5.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel5.Location = new System.Drawing.Point(0, 0);
            this.panel5.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.panel5.Name = "panel5";
            this.panel5.Size = new System.Drawing.Size(410, 29);
            this.panel5.TabIndex = 0;
            // 
            // button6
            // 
            this.button6.Dock = System.Windows.Forms.DockStyle.Left;
            this.button6.Location = new System.Drawing.Point(36, 0);
            this.button6.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button6.Name = "button6";
            this.button6.Size = new System.Drawing.Size(29, 29);
            this.button6.TabIndex = 1;
            this.button6.Text = "-";
            this.button6.UseVisualStyleBackColor = true;
            this.button6.Click += new System.EventHandler(this.button6_Click);
            // 
            // button4
            // 
            this.button4.Dock = System.Windows.Forms.DockStyle.Left;
            this.button4.Location = new System.Drawing.Point(0, 0);
            this.button4.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(36, 29);
            this.button4.TabIndex = 0;
            this.button4.Text = "+";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.overlayGridView);
            this.tabPage3.Controls.Add(this.panel9);
            this.tabPage3.Location = new System.Drawing.Point(4, 25);
            this.tabPage3.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPage3.Size = new System.Drawing.Size(418, 837);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "显示叠加层";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // overlayGridView
            // 
            this.overlayGridView.AllowUserToAddRows = false;
            this.overlayGridView.AllowUserToDeleteRows = false;
            this.overlayGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.overlayGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colOverlayVisible,
            this.colOverlayTool,
            this.colOverlayName,
            this.colOverlayType,
            this.colOverlayColor,
            this.colOverlayDraw,
            this.colOverlayLineWidth});
            this.overlayGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.overlayGridView.Location = new System.Drawing.Point(4, 39);
            this.overlayGridView.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.overlayGridView.Name = "overlayGridView";
            this.overlayGridView.RowHeadersWidth = 51;
            this.overlayGridView.RowTemplate.Height = 23;
            this.overlayGridView.Size = new System.Drawing.Size(410, 794);
            this.overlayGridView.TabIndex = 0;
            this.overlayGridView.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.overlayGridView_CellEndEdit);
            // 
            // colOverlayVisible
            // 
            this.colOverlayVisible.HeaderText = "显示";
            this.colOverlayVisible.MinimumWidth = 6;
            this.colOverlayVisible.Name = "colOverlayVisible";
            this.colOverlayVisible.Width = 40;
            // 
            // colOverlayTool
            // 
            this.colOverlayTool.HeaderText = "工具";
            this.colOverlayTool.MinimumWidth = 6;
            this.colOverlayTool.Name = "colOverlayTool";
            this.colOverlayTool.ReadOnly = true;
            this.colOverlayTool.Width = 80;
            // 
            // colOverlayName
            // 
            this.colOverlayName.HeaderText = "名称";
            this.colOverlayName.MinimumWidth = 6;
            this.colOverlayName.Name = "colOverlayName";
            this.colOverlayName.ReadOnly = true;
            this.colOverlayName.Width = 80;
            // 
            // colOverlayType
            // 
            this.colOverlayType.HeaderText = "类型";
            this.colOverlayType.Items.AddRange(new object[] {
            "REGION",
            "LINE"});
            this.colOverlayType.MinimumWidth = 6;
            this.colOverlayType.Name = "colOverlayType";
            this.colOverlayType.ReadOnly = true;
            this.colOverlayType.Width = 60;
            // 
            // colOverlayColor
            // 
            this.colOverlayColor.HeaderText = "颜色";
            this.colOverlayColor.Items.AddRange(new object[] {
            "red",
            "green",
            "blue",
            "yellow",
            "cyan",
            "magenta",
            "white",
            "black",
            "orange"});
            this.colOverlayColor.MinimumWidth = 6;
            this.colOverlayColor.Name = "colOverlayColor";
            this.colOverlayColor.Width = 70;
            // 
            // colOverlayDraw
            // 
            this.colOverlayDraw.HeaderText = "Draw";
            this.colOverlayDraw.Items.AddRange(new object[] {
            "margin",
            "fill"});
            this.colOverlayDraw.MinimumWidth = 6;
            this.colOverlayDraw.Name = "colOverlayDraw";
            this.colOverlayDraw.Width = 60;
            // 
            // colOverlayLineWidth
            // 
            this.colOverlayLineWidth.HeaderText = "线宽";
            this.colOverlayLineWidth.MinimumWidth = 6;
            this.colOverlayLineWidth.Name = "colOverlayLineWidth";
            this.colOverlayLineWidth.Width = 50;
            // 
            // panel9
            // 
            this.panel9.Controls.Add(this.button9);
            this.panel9.Controls.Add(this.cbOnlyCurrentTool);
            this.panel9.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel9.Location = new System.Drawing.Point(4, 4);
            this.panel9.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.panel9.Name = "panel9";
            this.panel9.Size = new System.Drawing.Size(410, 35);
            this.panel9.TabIndex = 1;
            // 
            // button9
            // 
            this.button9.Dock = System.Windows.Forms.DockStyle.Right;
            this.button9.Location = new System.Drawing.Point(309, 0);
            this.button9.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button9.Name = "button9";
            this.button9.Size = new System.Drawing.Size(101, 35);
            this.button9.TabIndex = 1;
            this.button9.Text = "刷新显示";
            this.button9.UseVisualStyleBackColor = true;
            this.button9.Click += new System.EventHandler(this.button9_Click);
            // 
            // cbOnlyCurrentTool
            // 
            this.cbOnlyCurrentTool.AutoSize = true;
            this.cbOnlyCurrentTool.Dock = System.Windows.Forms.DockStyle.Left;
            this.cbOnlyCurrentTool.Location = new System.Drawing.Point(0, 0);
            this.cbOnlyCurrentTool.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cbOnlyCurrentTool.Name = "cbOnlyCurrentTool";
            this.cbOnlyCurrentTool.Size = new System.Drawing.Size(179, 35);
            this.cbOnlyCurrentTool.TabIndex = 0;
            this.cbOnlyCurrentTool.Text = "仅显示当前工具叠加层";
            this.cbOnlyCurrentTool.UseVisualStyleBackColor = true;
            this.cbOnlyCurrentTool.CheckedChanged += new System.EventHandler(this.cbOnlyCurrentTool_CheckedChanged);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.button8);
            this.panel1.Controls.Add(this.button2);
            this.panel1.Controls.Add(this.button3);
            this.panel1.Controls.Add(this.button1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(426, 29);
            this.panel1.TabIndex = 4;
            // 
            // button8
            // 
            this.button8.Dock = System.Windows.Forms.DockStyle.Left;
            this.button8.Location = new System.Drawing.Point(225, 0);
            this.button8.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button8.Name = "button8";
            this.button8.Size = new System.Drawing.Size(75, 29);
            this.button8.TabIndex = 4;
            this.button8.Text = "加载";
            this.button8.UseVisualStyleBackColor = true;
            this.button8.Click += new System.EventHandler(this.button8_Click);
            // 
            // button2
            // 
            this.button2.Dock = System.Windows.Forms.DockStyle.Left;
            this.button2.Location = new System.Drawing.Point(150, 0);
            this.button2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 29);
            this.button2.TabIndex = 2;
            this.button2.Text = "运行";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button3
            // 
            this.button3.Dock = System.Windows.Forms.DockStyle.Left;
            this.button3.Location = new System.Drawing.Point(75, 0);
            this.button3.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(75, 29);
            this.button3.TabIndex = 3;
            this.button3.Text = "脚本";
            this.button3.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Dock = System.Windows.Forms.DockStyle.Left;
            this.button1.Location = new System.Drawing.Point(0, 0);
            this.button1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 29);
            this.button1.TabIndex = 1;
            this.button1.Text = "工具箱";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // panel8
            // 
            this.panel8.Controls.Add(this.hDisplayControl1);
            this.panel8.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel8.Location = new System.Drawing.Point(0, 29);
            this.panel8.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.panel8.Name = "panel8";
            this.panel8.Size = new System.Drawing.Size(685, 866);
            this.panel8.TabIndex = 1;
            // 
            // panel7
            // 
            this.panel7.Controls.Add(this.comboBox1);
            this.panel7.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel7.Location = new System.Drawing.Point(0, 0);
            this.panel7.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.panel7.Name = "panel7";
            this.panel7.Size = new System.Drawing.Size(685, 29);
            this.panel7.TabIndex = 0;
            // 
            // comboBox1
            // 
            this.comboBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(0, 0);
            this.comboBox1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(685, 23);
            this.comboBox1.TabIndex = 0;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // hDisplayControl1
            // 
            this.hDisplayControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hDisplayControl1.Location = new System.Drawing.Point(0, 0);
            this.hDisplayControl1.Margin = new System.Windows.Forms.Padding(5);
            this.hDisplayControl1.Name = "hDisplayControl1";
            this.hDisplayControl1.Size = new System.Drawing.Size(685, 866);
            this.hDisplayControl1.TabIndex = 0;
            // 
            // ToolBlockControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1123, 899);
            this.Controls.Add(this.splitContainer1);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "ToolBlockControl";
            this.Text = "ToolBlockControl";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.panel4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.OutputGridView2)).EndInit();
            this.panel6.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.InputGridView1)).EndInit();
            this.panel5.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.overlayGridView)).EndInit();
            this.panel9.ResumeLayout(false);
            this.panel9.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel8.ResumeLayout(false);
            this.panel7.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.DataGridView OutputGridView2;
        private System.Windows.Forms.Panel panel6;
        private System.Windows.Forms.Button button5;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.DataGridView InputGridView1;
        private System.Windows.Forms.Panel panel5;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.DataGridViewTextBoxColumn OutputName;
        private System.Windows.Forms.DataGridViewTextBoxColumn OutputType;
        private System.Windows.Forms.DataGridViewTextBoxColumn OutputValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn InputName;
        private System.Windows.Forms.DataGridViewTextBoxColumn InputType;
        private System.Windows.Forms.DataGridViewTextBoxColumn InputValue;
        private System.Windows.Forms.Button button7;
        private System.Windows.Forms.Button button6;
        private System.Windows.Forms.Button button8;
        private System.Windows.Forms.Panel panel8;
        private HDisplayControl hDisplayControl1;
        private System.Windows.Forms.Panel panel7;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.Panel panel9;
        private System.Windows.Forms.CheckBox cbOnlyCurrentTool;
        private System.Windows.Forms.Button button9;
        private System.Windows.Forms.DataGridView overlayGridView;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colOverlayVisible;
        private System.Windows.Forms.DataGridViewTextBoxColumn colOverlayTool;
        private System.Windows.Forms.DataGridViewTextBoxColumn colOverlayName;
        private System.Windows.Forms.DataGridViewComboBoxColumn colOverlayType;
        private System.Windows.Forms.DataGridViewComboBoxColumn colOverlayColor;
        private System.Windows.Forms.DataGridViewComboBoxColumn colOverlayDraw;
        private System.Windows.Forms.DataGridViewTextBoxColumn colOverlayLineWidth;
    }
}