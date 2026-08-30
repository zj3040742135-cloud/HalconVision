using HalconDotNet;
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
using System.Xml.Linq;

namespace HToolBase.Controls
{
    public partial class ToolBlockControl : HForm
    {
        ToolBlock ToolBlock;
        public override ToolBase tool
        {
            get { return ToolBlock; }
            set { 
                ToolBlock =(ToolBlock) value;
                this.Text = ToolBlock.RootNode.Text;
                UnBindEvent();
                BindingEvent();
            }
        }
        /// <summary>
        /// 节点重命名委托
        /// </summary>
        public delegate void RenamePortHandler(string oldName, string newName);
        /// <summary>
        /// 输入节点重命名
        /// </summary>
        public event RenamePortHandler RenameInputPortEvent;
        /// <summary>
        /// 输入节点重命名
        /// </summary>
        public event RenamePortHandler RenameOutputPortEvent;

        TypeName SelectType;
        ContextMenuStrip ContextMenu =new ContextMenuStrip();

        bool IsInput = false;
        string DefultName;
        public ToolTreeviewControl toolTreeview;
        public ToolBlockControl()
        {
            InitializeComponent();
            ToolBlock = new ToolBlock();
            toolTreeview=new ToolTreeviewControl(ToolBlock);
            this.tabControl1.TabPages[0].Controls.Add(toolTreeview);

            SetDoubleBuffered(InputGridView1);
            SetDoubleBuffered(OutputGridView2);
            InputGridView1.Columns[1].ReadOnly = true;
            OutputGridView2.Columns[1].ReadOnly = true;
            InputGridView1.AllowUserToAddRows = false;
            OutputGridView2.AllowUserToAddRows = false;
            OutputGridView2.Rows.Clear();
            InputGridView1.Rows.Clear();

            
            InputGridView1.CellEndEdit += InputGridView1_CellEndEdit;
            InputGridView1.CellBeginEdit += InputGridView1_CellBeginEdit;
            OutputGridView2.CellBeginEdit += OutputGridView2_CellBeginEdit;
            OutputGridView2.CellEndEdit += OutputGridView2_CellEndEdit;

            ContextMenu.Items.Add("Single", null, (s,e) => { SelectType = TypeName.SINGAL; AddPort(); });
            ContextMenu.Items.Add("String", null, (s, e) => { SelectType = TypeName.STRING; AddPort(); });
            ContextMenu.Items.Add("Bool", null,  (s, e) => { SelectType = TypeName.BOOL; AddPort(); });
            ContextMenu.Items.Add("Image", null, (s, e) => { SelectType = TypeName.IMAGE; AddPort(); });

            // 形态2：button3"脚本"打开 ToolBlock 块级脚本编辑器
            this.button3.Click += (s, e) => { this.ToolBlock.ShowScriptEditor(); };

            // 延迟到窗体显示/加载后再填充端口，避免与ToolTreeview初始化交叉导致消息循环卡顿
            this.Load += (s, e) => LoadExistingPortsToGridView();
        }
        public override void BindingEvent()
        {
            toolTreeview = new ToolTreeviewControl(ToolBlock);
            this.tabControl1.TabPages[0].Controls.Add(toolTreeview);
            RenameInputPortEvent += toolTreeview.RenameInputPort;
            RenameOutputPortEvent += toolTreeview.RenameOutputPort;
            ToolBlock.AddInputEvent += ToolBlock_AddInputEvent;
            ToolBlock.AddOutputEvent += ToolBlock_AddOutputEvent;
            // ToolBlock.Run 完成后通知 UI 刷新（外部直接调用 ToolBlock.Run 时也能自动更新图像窗口）
            ToolBlock.RunCompleted += ToolBlock_RunCompleted;

        }
        public override void UnBindEvent()
        {

            RenameInputPortEvent -= toolTreeview.RenameInputPort;
            RenameOutputPortEvent -= toolTreeview.RenameOutputPort;
            ToolBlock.AddInputEvent -= ToolBlock_AddInputEvent;
            ToolBlock.AddOutputEvent -= ToolBlock_AddOutputEvent;
            // ToolBlock.Run 完成后通知 UI 刷新（外部直接调用 ToolBlock.Run 时也能自动更新图像窗口）
            ToolBlock.RunCompleted -= ToolBlock_RunCompleted;
            this.tabControl1.TabPages[0].Controls.Clear();
            toolTreeview?.Dispose();
            toolTreeview = null;

        }

        /// <summary>
        /// 窗口关闭、窗体Dispose之前，内部工具的RootNode还挂在本窗体的树视图上且句柄存活。
        /// 此时必须先摘除（DetachToolNodes）：工具实例被ToolBlock长期持有并跨编辑器窗口复用，
        /// 若等窗体Dispose后节点仍挂着，节点内部会残留已销毁的原生句柄与treeView引用，
        /// 再次打开编辑器AddNodes时抛"不能在多处添加或插入项...Parameter name: node"异常
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            toolTreeview?.DetachToolNodes();
            base.OnFormClosed(e);
        }
        /// <summary>
        /// 窗体加载完成后，将已存在的ToolBlock输入/输出端口填充到DataGridView
        /// </summary>
        private void LoadExistingPortsToGridView()
        {
            try
            {
                InputGridView1.Rows.Clear();
                OutputGridView2.Rows.Clear();

                // 加载已存在的ToolBlock输入端口到DataGridView
                foreach (var port in ToolBlock.Inputs.Values)
                {
                    int rowIdx = InputGridView1.Rows.Add();
                    DataGridViewRow row = InputGridView1.Rows[rowIdx];
                    DataGridViewCell cell = row.Cells[2];
                    Type type = TypeNameHelper.ToSystemType(port.PortType);
                    row.SetValues(port.Text, port.PortType.ToString(), port.Value);
                    port.ValueChanged += InputPort_ValueChanged;
                }
                // 加载已存在的ToolBlock输出端口到DataGridView
                foreach (var port in ToolBlock.Outputs.Values)
                {
                    int rowIdx = OutputGridView2.Rows.Add();
                    DataGridViewRow row = OutputGridView2.Rows[rowIdx];
                    DataGridViewCell cell = row.Cells[2];
                    Type type = TypeNameHelper.ToSystemType(port.PortType);
                    cell.ValueType = type;
                    row.SetValues(port.Text, port.PortType.ToString(), port.Value);
                    port.ValueChanged += OutputPort_ValueChanged;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"端口加载失败：{ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OutputGridView2_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView dgv = sender as DataGridView;
            DataGridViewRow row = dgv.Rows[e.RowIndex];
            DataGridViewCell cell = row.Cells[e.ColumnIndex];
            string oldVal = cell.Tag?.ToString();
            string newVal = cell.Value?.ToString();
            string name = dgv.Rows[e.RowIndex].Cells["OutputName"].Value?.ToString();
            // 空值判断（按需保留/删除）
            if (string.IsNullOrEmpty(newVal))
            {
#if DEBUG
                Console.WriteLine("内容不能为空！");
#endif
                RestoreOldValue(dgv, e.RowIndex, e.ColumnIndex);
                return;
            }
            if (e.ColumnIndex == 0)
            {
                // 遍历该列所有行查重
                bool isDuplicate = CheckColumnDuplicate(dgv, e.ColumnIndex, newVal, e.RowIndex);
                if (isDuplicate)
                {
#if DEBUG
                    Console.WriteLine($"该值【{newVal}】已存在，不允许重复！");
#endif 
                    RestoreOldValue(dgv, e.RowIndex, e.ColumnIndex);
                    return;
                }
                if (ToolBlock.RenameKey(ToolBlock.Outputs, oldVal, newVal))
                {


                    ToolBlock.Outputs[newVal].PortName = newVal;
                    RenameOutputPortEvent?.Invoke(oldVal, newVal);
                }
                    
            }
            else if (e.ColumnIndex == 2)
            {
                try
                {
                    Type type = TypeNameHelper.ToSystemType(ToolBlock.Outputs[name].PortType);
                    cell.Value = Convert.ChangeType(cell.Value, type);
                    ToolBlock.Outputs[name].Value = cell.Value;
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    RestoreOldValue(dgv, e.RowIndex, e.ColumnIndex);
                }
                


            }
        }
        private void OutputGridView2_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            DataGridView dgv = sender as DataGridView;
            DataGridViewCell cell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
            cell.Tag = cell.Value; 
        }

        private void InputGridView1_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            DataGridView dgv = sender as DataGridView;
            DataGridViewCell cell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
            cell.Tag = cell.Value; 
        }

        private void InputGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView dgv = sender as DataGridView;
            DataGridViewRow row = dgv.Rows[e.RowIndex];
            DataGridViewCell cell = row.Cells[e.ColumnIndex];
            string oldVal = cell.Tag?.ToString();
            string newVal = cell.Value?.ToString();
            string name = dgv.Rows[e.RowIndex].Cells["InputName"].Value?.ToString();
            // 空值判断（按需保留/删除）
            if (string.IsNullOrEmpty(newVal))
            {
#if DEBUG
                Console.WriteLine("内容不能为空！");
#endif
                RestoreOldValue(dgv, e.RowIndex, e.ColumnIndex);
                return;
            }
            if (e.ColumnIndex == 0)
            {
                // 遍历该列所有行查重
                bool isDuplicate = CheckColumnDuplicate(dgv, e.ColumnIndex, newVal, e.RowIndex);
                if (isDuplicate)
                {
#if DEBUG
                    Console.WriteLine($"该值【{newVal}】已存在，不允许重复！");
#endif
                    RestoreOldValue(dgv, e.RowIndex, e.ColumnIndex);
                    return;
                }
                if (ToolBlock.RenameKey(ToolBlock.Inputs, oldVal, newVal))
                {
                    ToolBlock.Inputs[newVal].PortName = newVal;
                    RenameInputPortEvent?.Invoke(oldVal, newVal);
                }
                    
            }
            else if (e.ColumnIndex == 2)
            {
                try
                {
                    Type type = TypeNameHelper.ToSystemType(ToolBlock.Inputs[name].PortType);
                    cell.Value = Convert.ChangeType(cell.Value, type);
                    ToolBlock.Inputs[name].Value = cell.Value;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    RestoreOldValue(dgv, e.RowIndex, e.ColumnIndex);
                }
                
            }
        }
        /// <summary>
        /// 检查指定列是否存在重复值（排除当前编辑行自己）
        /// </summary>
        private bool CheckColumnDuplicate(DataGridView dgv, int colIndex, string targetVal, int skipRow)
        {
            foreach (DataGridViewRow row in dgv.Rows)
            {
                // 跳过空白行、当前编辑行
                if (row.IsNewRow || row.Index == skipRow)
                    continue;

                string cellVal = row.Cells[colIndex].Value?.ToString().Trim();
                if (cellVal == targetVal)
                {
                    return true; // 找到重复
                }
            }
            return false;
        }
        /// <summary>
        /// 恢复单元格修改前原值，并进入编辑状态
        /// </summary>
        private void RestoreOldValue(DataGridView dgv, int rowIdx, int colIdx)
        {
            DataGridViewCell cell = dgv.Rows[rowIdx].Cells[colIdx];
            cell.Value = cell.Tag; // 还原旧值
            dgv.BeginEdit(true); // 重新激活编辑框，让用户重新输入
        }
        public static void SetDoubleBuffered(Control ctl)
        {
            var prop = ctl.GetType().GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            prop?.SetValue(ctl, true);
        }
        private void ToolBlock_AddOutputEvent(PortNode portNode)
        {
            int rowIdx = OutputGridView2.Rows.Add();
            DataGridViewRow row = OutputGridView2.Rows[rowIdx];
            DataGridViewCell cell = row.Cells[2];
            Type type = TypeNameHelper.ToSystemType(portNode.PortType);
            cell.ValueType = type;
            row.SetValues(portNode.Text, portNode.PortType.ToString(), portNode.Value);
            portNode.ValueChanged += OutputPort_ValueChanged;
        }

        private void ToolBlock_AddInputEvent(PortNode portNode)
        {
            int rowIdx = InputGridView1.Rows.Add();
            DataGridViewRow row = InputGridView1.Rows[rowIdx];
            DataGridViewCell cell = row.Cells[2];
            Type type = TypeNameHelper.ToSystemType(portNode.PortType);
            row.SetValues(portNode.Text, portNode.PortType.ToString(), portNode.Value);
            portNode.ValueChanged += InputPort_ValueChanged;
        }

        private void InputPort_ValueChanged(object sender, EventArgs e)
        {
            if (sender is PortNode portNode)
            {
                if (InputGridView1.InvokeRequired)
                {
                    InputGridView1.BeginInvoke(new Action(() => UpdateInputCellValue(portNode)));
                }
                else
                {
                    UpdateInputCellValue(portNode);
                }
            }
        }

        private void OutputPort_ValueChanged(object sender, EventArgs e)
        {
            if (sender is PortNode portNode)
            {
                if (OutputGridView2.InvokeRequired)
                {
                    OutputGridView2.BeginInvoke(new Action(() => UpdateOutputCellValue(portNode)));
                }
                else
                {
                    UpdateOutputCellValue(portNode);
                }
            }
        }

        private void UpdateInputCellValue(PortNode portNode)
        {
            foreach (DataGridViewRow row in InputGridView1.Rows)
            {
                if (row.Cells["InputName"].Value?.ToString() == portNode.PortName)
                {
                    row.Cells[2].Value = portNode.Value;
                    break;
                }
            }
        }

        private void UpdateOutputCellValue(PortNode portNode)
        {
            foreach (DataGridViewRow row in OutputGridView2.Rows)
            {
                if (row.Cells["OutputName"].Value?.ToString() == portNode.PortName)
                {
                    row.Cells[2].Value = portNode.Value;
                    break;
                }
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            new AddToolForm(this.ToolBlock).ShowDialog();
            
        }
        [Obsolete]
        private new void Refresh()
        {
            int rowIdx = InputGridView1.Rows.Count;
            int rowIdx2 = OutputGridView2.Rows.Count;
            for (int i = 0; i < rowIdx; i++)
            {
                DataGridViewCell cell = InputGridView1.Rows[i].Cells[2];
                string name = InputGridView1.Rows[i].Cells["InputName"].Value?.ToString();
                var value=  this.ToolBlock.RefreshInput(name);
                cell.Value = value;
            }
            for (int i = 0; i < rowIdx2; i++)
            {
                DataGridViewCell cell = OutputGridView2.Rows[i].Cells[2];
                string name = OutputGridView2.Rows[i].Cells["OutputName"].Value?.ToString();
                var value = this.ToolBlock.RefreshOutput(name);
                cell.Value = value;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ToolBlock.Run();
            // Note: ToolBlock.Run() 内部已触发 RunCompleted → ToolBlock_RunCompleted → RefreshDisplayAfterRun()
            // 此处仍手动调用一次，避免用户点击运行时与事件异步导致的视觉延迟；重复两次刷新无功能问题，仅轻微性能开销。
            RefreshDisplayAfterRun();
        }

        /// <summary>Run 之后刷新 UI：图像下拉、HDisplayControl 图像、叠加层网格、叠加层显示。
        /// 手动点击"运行"和 ToolBlock.RunCompleted 事件都调用此方法。</summary>
        private void RefreshDisplayAfterRun()
        {
            this.comboBox1.Items.Clear();
            foreach (string s in ToolBlock.ToolImage.Keys)
            {
                this.comboBox1.Items.Add(s);
            }
            if (this.comboBox1.Items.Count > 0)
            {
                this.comboBox1.SelectedIndex = 0;
            }
            // Run 后刷新叠加层网格并应用显示（工具可能在 Run 中发布/更新了 Region/XLD）
            RefreshOverlayGrid();
            ApplyOverlays();
        }

        /// <summary>外部/内部 ToolBlock.Run 完成后事件处理器。跨线程安全（BeginInvoke 封送回 UI 线程）。</summary>
        private void ToolBlock_RunCompleted(object sender, EventArgs e)
        {
            if (this.IsDisposed) return;
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
                this.BeginInvoke(new Action(RefreshDisplayAfterRun));
            else
                RefreshDisplayAfterRun();
        }

        /// <summary>获取当前应显示的叠加层集合。
        /// cbOnlyCurrentTool 勾选且 ComboBox 有选中项时，仅返回选中图像所属工具的项；否则返回全部。</summary>
        private List<(ToolBase Tool, DisplayItem Item)> GetDisplayItemsToApply()
        {
            var all = ToolBlock.CollectDisplayItems();
            if (cbOnlyCurrentTool.Checked && comboBox1.SelectedItem != null)
            {
                string key = comboBox1.SelectedItem.ToString();
                if (ToolBlock.ToolImageOwner.TryGetValue(key, out ToolBase owner) && owner != null)
                    return all.Where(t => t.Tool == owner).ToList();
            }
            return all;
        }

        /// <summary>填充叠加层 DataGridView（按 GetDisplayItemsToApply 的过滤结果）。
        /// 每行 Tag 存 (ToolBase, DisplayItem) 元组，供 CellEndEdit 回写配置。</summary>
        private void RefreshOverlayGrid()
        {
            overlayGridView.Rows.Clear();
            overlayGridView.SuspendLayout();
            try
            {
                foreach (var (tool, item) in GetDisplayItemsToApply())
                {
                    int idx = overlayGridView.Rows.Add();
                    DataGridViewRow row = overlayGridView.Rows[idx];
                    row.Tag = (tool, item);   // 回写配置用
                    row.Cells[0].Value = item.Visible;
                    row.Cells[1].Value = tool.ToolName;
                    row.Cells[2].Value = item.Name;
                    row.Cells[3].Value = item.Type.ToString();
                    row.Cells[4].Value = item.Color;
                    row.Cells[5].Value = item.Draw;
                    row.Cells[6].Value = item.LineWidth;
                }
            }
            finally
            {
                overlayGridView.ResumeLayout();
            }
        }

        /// <summary>将当前叠加层集合应用到 HDisplayControl（深拷贝渲染）。
        /// 不重跑 Run，仅按现有 DisplayItem 配置与数据重绘。</summary>
        private void ApplyOverlays()
        {
            var items = GetDisplayItemsToApply().Select(t => t.Item).ToList();
            hDisplayControl1.SetOverlays(items);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            // 手动刷新显示（无需重跑 Run）
            ApplyOverlays();
        }

        private void cbOnlyCurrentTool_CheckedChanged(object sender, EventArgs e)
        {
            // 切换过滤模式：重刷网格 + 重绘叠加层
            RefreshOverlayGrid();
            ApplyOverlays();
        }

        private void overlayGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= overlayGridView.Rows.Count) return;
            DataGridViewRow row = overlayGridView.Rows[e.RowIndex];
            // Tag 存的是 (ToolBase, DisplayItem) ValueTuple（box 为 object），用显式类型检查拆箱
            if (!(row.Tag is ValueTuple<ToolBase, DisplayItem> tag)) return;
            DisplayItem item = tag.Item2;
            try
            {
                switch (e.ColumnIndex)
                {
                    case 0: // 显示
                        item.Visible = row.Cells[0].Value is bool b ? b : Convert.ToBoolean(row.Cells[0].Value);
                        break;
                    case 4: // 颜色
                        item.Color = row.Cells[4].Value?.ToString() ?? "red";
                        break;
                    case 5: // Draw
                        item.Draw = row.Cells[5].Value?.ToString() ?? "margin";
                        break;
                    case 6: // 线宽
                        if (double.TryParse(row.Cells[6].Value?.ToString(), out double lw))
                            item.LineWidth = lw;
                        else
                            row.Cells[6].Value = item.LineWidth; // 还原
                        break;
                }
            }
            catch { }
            ApplyOverlays();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            DefultName = "Input";
            IsInput = true;
            ContextMenu.Show(button4, button4.Location);
        }
        private void AddPort()
        {
            if (IsInput)
            {
                this.ToolBlock.AddInput(DefultName, SelectType);
            }
            else
            {
                this.ToolBlock.AddOutput(DefultName, SelectType);
            }
            
        }
        private void button5_Click(object sender, EventArgs e)
        {
            DefultName = "Output";
            IsInput = false;
            ContextMenu.Show(button5, button5.Location);
        }
        private void button6_Click(object sender, EventArgs e)
        {
            if(InputGridView1.CurrentRow!=null)
            {
                string name = InputGridView1.CurrentRow.Cells["InputName"].Value?.ToString();
                int index = InputGridView1.CurrentRow.Index;
                // 先移除与该端口相关的所有连线，再删除端口，避免连线残留
                if (this.ToolBlock.Inputs.TryGetValue(name, out var port))
                {
                    this.ToolBlock.DisconnectPortByPort(port);
                    // 释放该端口持有的 HObject（删除即不再使用；Fix1后为独立副本，释放安全）
                    if (port.Value is HObject hObj)
                    {
                        try { if (hObj.IsInitialized()) hObj.Dispose(); } catch { }
                    }
                    // canonical端口保留在RootNode中（父级树视图显示），删除时需从RootNode移除，
                    // 否则父级树视图会残留已删除的端口节点（编辑器树视图的克隆由ReloadData清空）
                    port.Parent?.Nodes.Remove(port);
                }
                this.ToolBlock.Inputs.Remove(name);
                toolTreeview?.ReloadData();
                InputGridView1.CurrentRow.Dispose();
                InputGridView1.Rows.RemoveAt(index);

            }

        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (OutputGridView2.CurrentRow != null)
            {
                string name = OutputGridView2.CurrentRow.Cells["OutputName"].Value?.ToString();
                int index = OutputGridView2.CurrentRow.Index;
                // 先移除与该端口相关的所有连线，再删除端口，避免连线残留
                if (this.ToolBlock.Outputs.TryGetValue(name, out var port))
                {
                    this.ToolBlock.DisconnectPortByPort(port);
                    // 释放该端口持有的 HObject（删除即不再使用；Fix1后为独立副本，释放安全）
                    if (port.Value is HObject hObj)
                    {
                        try { if (hObj.IsInitialized()) hObj.Dispose(); } catch { }
                    }
                }
                this.ToolBlock.Outputs.Remove(name);
                toolTreeview?.ReloadData();
                OutputGridView2.CurrentRow.Dispose();
                OutputGridView2.Rows.RemoveAt(index);

            }
        }
        private void button8_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "选择文件";
            dialog.Filter = "工具|*.vpp|所有文件(*.*)|*.*";
            string Text= this.ToolBlock.RootNode.Text;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string path= dialog.FileName;
                this.ToolBlock.LoadVpp(path);
                this.ToolBlock.RootNode.Text = Text;
                LoadExistingPortsToGridView();
                this.toolTreeview?.ReloadData();
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex != -1)
            {
                string key = comboBox1.SelectedItem.ToString();
                if (!ToolBlock.ToolImage.ContainsKey(key)) return;
                this.hDisplayControl1.ShowImage(ToolBlock.ToolImage[key]);
                // 切换图像后重应用叠加层：若「仅显示当前工具」勾选，按新选中工具过滤
                if (cbOnlyCurrentTool.Checked)
                    RefreshOverlayGrid();
                ApplyOverlays();
            }
        }
    }
}
