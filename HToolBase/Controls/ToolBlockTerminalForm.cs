using HToolBase.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HToolBase.Controls
{
    
    public partial class ToolBlockTerminalForm : Form
    {
        int ControlCount = 0;
        ToolBlock ToolBlock=new ToolBlock();
        ProcessPanel ProcessPanel = new ProcessPanel();
        private Dictionary<TextBox, BindableString> _textBoxBindMap = new Dictionary<TextBox, BindableString>();
        string s;
        public ToolBlockTerminalForm(ProcessPanel processPanel,ToolBlock toolBlock)
        {
            InitializeComponent();
            ToolBlock=toolBlock;
            ProcessPanel=processPanel;
            // 构造完成后加载已存在的终端连线并创建对应控件
            LoadExistingConnections();
        }

        /// <summary>
        /// 加载当前ToolBlock作为ToTool的所有终端连线，为每条连线创建一行UI控件。
        /// 重新打开窗口时，ToolBlock.Inputs已从VPP加载、TerConnections已存在，
        /// 通过CreateConnectionRow传入已连接状态（hasConnection=true），避免重复AddInput。
        /// </summary>
        private void LoadExistingConnections()
        {
            if (ToolBlock == null || ProcessPanel == null) return;

            // 1. 临时存储需要移除的工具Key（避免遍历中修改字典）
            List<ToolBlock> removeToolKeys = new List<ToolBlock>();

            foreach (var kvp in ProcessPanel.TerConnections)
            {
                
                ToolBlock fromTool =(ToolBlock) kvp.Key;
                List<PortConnection> connList =(List<PortConnection>) kvp.Value;
                // 临时保存当前分组里要删掉的无效连线
                List<PortConnection> invalidConns = new List<PortConnection>();

                foreach (var conn in connList)
                {
                    // 空值防御，防止空引用崩溃
                    if (conn.ToTool == null || conn.ToPort == null || conn.ToTool.Inputs == null)
                    {
                        invalidConns.Add(conn);
                        continue;
                    }

                    if (conn.ToTool == ToolBlock)
                    {
                        // 目标端口存在：保留连线，生成表格行
                        if (conn.ToTool.Inputs.Values.Contains(conn.ToPort))
                        {
                            CreateConnectionRow(conn.FromTool, conn.FromPort, conn.ToPort);
                        }
                        else
                        {
                            // 当前这条连线无效，标记待删除，不再删除整组工具
                            invalidConns.Add(conn);
                        }
                    }
                }

                // 移除当前分组内所有无效连线
                foreach (var badConn in invalidConns)
                {
                    connList.Remove(badConn);
                }

                // 如果当前分组连线全部清空，标记Key待后续统一删除
                if (connList.Count == 0)
                {
                    removeToolKeys.Add(fromTool);
                }
            }

            // 遍历完成后，批量移除空连线分组（不会破坏迭代器）
            foreach (var delKey in removeToolKeys)
            {
                ProcessPanel.TerConnections.Remove(delKey);
            }
        }

        /// <summary>
        /// 创建一行终端连线UI控件（Label+TextBox+Button），并绑定按钮点击事件。
        /// 首次点击创建输入端口+连线，后续点击仅断开旧连线再重连（不重复AddInput）。
        /// 若传入fromTool/fromPort/toPort，则初始化为已连接状态（用于加载已存在的连线）。
        /// </summary>
        private void CreateConnectionRow(ToolBase fromTool = null, PortNode fromPort = null, PortNode toPort = null)
        {
            Label label = new Label();
            label.Enabled = false;
            label.BackColor = Color.White;
            if (toPort != null)
            {
                label.Text = toPort.Text;
            }
            else
                label.Text = s;
            label.Size = new Size(100, 20);
            label.Location = new Point(0, 25 * ControlCount);

            TextBox textBox = new TextBox();
            textBox.BackColor = SystemColors.ControlLight;
            textBox.Enabled = false;
            textBox.Size = new Size(this.panel1.Width - 150, 50);
            textBox.Location = new Point(100, 25 * ControlCount);

            Button button = new Button();
            button.Enabled = true;
            button.Text = "...";
            button.Size = new Size(50, 20);
            button.Location = new Point(textBox.Width + label.Width, 25 * ControlCount);
            ControlCount++;

            // 每次新增独立绑定对象
            BindableString bindStr = new BindableString();
            bindStr.BindTextBox(textBox);
            _textBoxBindMap.Add(textBox, bindStr);

            // 连线状态跟踪：传入fromTool/fromPort/toPort时为已连接状态（重新打开窗口加载已存在连线）
            bool hasConnection = (fromTool != null && fromPort != null && toPort != null);
            ToolBase currentFromTool = fromTool;
            PortNode currentFromPort = fromPort;
            PortNode currentToPort = toPort;

            // 已存在连线时，TextBox显示源端口名
            if (hasConnection)
                bindStr.Value = fromPort.Parent.Text+"."+fromPort.PortName;

            // 按钮点击：首次创建输入端口+连线，后续仅断开旧连线再重连（不重复AddInput）
            button.Click += (s, ev) =>
            {
                TerminalForm terminal = new TerminalForm(ProcessPanel);
                terminal.ShowDialog();
                if (terminal.Node == null || terminal.RootNode == null) return;
                if (_textBoxBindMap.TryGetValue(textBox, out var bindObj))
                {
                    bindObj.Value = terminal.Node.Parent.Text+"."+ terminal.Node.Text;
                    ToolBlock tool = ((ToolModule)ProcessPanel.modules
                        .Cast<HToolBase.Module>()
                        .FirstOrDefault(n => n is ToolModule t && t.GetToolBlock().ToolName == terminal.RootNode.Text))
                        ?.GetToolBlock();
                    if (tool == null) return;
                    PortNode newFromPort = tool.Outputs.Values
                        .Cast<PortNode>()
                        .FirstOrDefault(p => p.PortName == terminal.Node.Text);
                    if (newFromPort == null) return;

                    if (!hasConnection)
                    {
                        // 首次连接：创建输入端口并建立终端连线
                        ToolBlock.AddInput(label.Text, newFromPort.PortType);
                        currentToPort = ToolBlock.Inputs[label.Text];
                        ProcessPanel.TerMinalConnectionAdd(tool, newFromPort, ToolBlock, currentToPort);
                    }
                    else
                    {
                        // 已存在连接：先断开旧连线，再重新连接（不重复AddInput，输入端口已存在）
                        ProcessPanel.DisconnectPort(currentFromTool, currentFromPort, ToolBlock, currentToPort);
                        ProcessPanel.TerMinalConnectionAdd(tool, newFromPort, ToolBlock, currentToPort);
                    }
                    // 更新捕获变量，供下次重连时断开使用
                    currentFromTool = tool;
                    currentFromPort = newFromPort;
                    hasConnection = true;
                }
            };

            panel1.Controls.Add(label);
            panel1.Controls.Add(textBox);
            panel1.Controls.Add(button);
        }

        private void Add()
        {
            CreateConnectionRow();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            s = Microsoft.VisualBasic.Interaction.InputBox("端口名", "输入内容");
            if (this.ToolBlock.Inputs.Keys.Contains(s))
            {
                return;
            }
            Add();
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // 遍历所有绑定对象，解绑TextBox事件
            foreach (var pair in _textBoxBindMap)
            {
                pair.Value.UnBindTextBox();
            }
            _textBoxBindMap.Clear();
            base.OnFormClosed(e);
        }
    }
    public class BindableString
    {
        private string _value;
        // 文本值
        public string Value
        {
            get => _value;
            set
            {
                // 值无变化不触发事件
                if (_value == value) return;
                _value = value;
                OnValueChanged();
            }
        }
        // 值变更事件
        public event EventHandler ValueChanged;
        protected virtual void OnValueChanged()
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
        #region 绑定TextBox 单向/双向绑定
        private TextBox _bindTextBox;
        private bool _isUpdating = false; // 防递归标记

        /// <summary>
        /// 双向绑定TextBox
        /// </summary>
        public void BindTextBox(TextBox textBox)
        {
            UnBindTextBox(); // 先解绑旧控件
            _bindTextBox = textBox;
            if (textBox == null) return;
            // 1. 对象Value改变 → 更新TextBox
            this.ValueChanged += BindableString_ValueChanged;
            // 初始化同步一次
            textBox.Text = this.Value;
        }

        // 对象值变化，同步到文本框
        private void BindableString_ValueChanged(object sender, EventArgs e)
        {
            if (_isUpdating || _bindTextBox == null) return;
            _isUpdating = true;
            try
            {
                // 跨线程安全（UI线程）
                if (_bindTextBox.InvokeRequired)
                {
                    _bindTextBox.Invoke(new Action(() => _bindTextBox.Text = Value));
                }
                else
                {
                    _bindTextBox.Text = Value;
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }
        /// <summary>
        /// 解绑控件，释放事件
        /// </summary>
        public void UnBindTextBox()
        {
            if (_bindTextBox != null)
            {
                this.ValueChanged -= BindableString_ValueChanged;
            }
            _bindTextBox = null;
        }
        #endregion
    }
}
