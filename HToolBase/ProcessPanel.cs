
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using HToolBase.Controls;
using HToolBase.Tools;
namespace HToolBase
{
    public enum TerminalType
    {
        Input,
        Output
    }
    public class TempConnection
    {
        public System.Drawing.Point StartPoint { get; set; }
        public System.Drawing.Point EndPoint { get; set; }
        public HToolBase.Module SourceModule { get; set; }
        public TerminalType SourceTerminal { get; set; }

    }
    public class Connection
    {
        public HToolBase.Module SourceModule { get; set; }
        public TerminalType SourceTerminal { get; set; }
        public HToolBase.Module TargetModule { get; set; }
        public TerminalType TargetTerminal { get; set; }

        public Connection(HToolBase.Module source, HToolBase.Module target)
        {
            SourceModule = source;
            TargetModule = target;
            SourceTerminal = TerminalType.Output;
            TargetTerminal = TerminalType.Input;
        }
    }
    /// <summary>流程终端连接</summary>
    public struct PortConnection
    {
        public ToolBase FromTool;
        public PortNode FromPort;
        public TypeName PortType;
        public ToolBase ToTool;
        public PortNode ToPort;
    }
    /// <summary>流程模块保存数据</summary>
    public class ModuleSaveData
    {
        public string Type { get; set; }
        public string Text { get; set; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    /// <summary>流程连线保存数据（按模块在列表中的索引引用）</summary>
    public class ProcessConnectionSaveData
    {
        public int SourceIndex { get; set; }
        public int TargetIndex { get; set; }
    }

    /// <summary>终端连线保存数据（跨ToolModule端口值传递，用工具名和端口名引用）</summary>
    public class TerConnectionSaveData
    {
        public string FromToolName { get; set; }
        public string FromPortName { get; set; }
        public string ToToolName { get; set; }
        public string ToPortName { get; set; }
        public string PortType { get; set; }
    }

    /// <summary>ProcessPanel整体保存数据</summary>
    public class ProcessPanelSaveData
    {
        public List<ModuleSaveData> Modules { get; set; } = new List<ModuleSaveData>();
        public List<ProcessConnectionSaveData> Connections { get; set; } = new List<ProcessConnectionSaveData>();
        public List<TerConnectionSaveData> TerConnections { get; set; } = new List<TerConnectionSaveData>();
    }

    public class ProcessPanel : Panel
    {
        private ContextMenuStrip contextMenu = new ContextMenuStrip();
        private ContextMenuStrip SelectModelMenu = new ContextMenuStrip();
        private Point point = new Point();
        public List<HToolBase.Module> modules = new List<HToolBase.Module>();
        public List<Connection> _connections = new List<Connection>();
        private HToolBase.Module SelectModule, MoveModule, InputModule, OutputModule, TermianlModule, CopyModule;
        private TempConnection _tempConnection = null;
        private (HToolBase.Module Module, TerminalType Type) _currentTerminal = (null, TerminalType.Input);
        public bool RunCompelete = false;
        public ManualResetEvent _resetEvent = new ManualResetEvent(false);
        public string PGS = "null";
        public Dictionary<ToolBase, List<PortConnection>> TerConnections=new Dictionary<ToolBase, List<PortConnection>>();
        /// <summary>ProcessPanel名称，用于构建保存目录 System/{产品}/{PanelName}/</summary>
        public string PanelName { get; set; } = "ProcessPanel";
        public ProcessPanel()
        {
            this.BackColor = Color.White;
            this.Dock = DockStyle.Fill;
            DoubleBuffer();
            AddStartModule();
            contextMenu.Items.Add("普通流程块", null, AddModule);
            SelectModelMenu.Items.Add("删除", null, DelModule);
            SelectModelMenu.Items.Add("删除连线");
            SelectModelMenu.Items.Add("重命名", null, ReName);
            SelectModelMenu.Items.Add("终端", null, ShowTerminal);
            this.Paint += Panel_Paint;
            this.MouseDown += Panel_MouseClick;
            this.MouseMove += Panel_MouseMove;
            this.MouseUp += Panel_MouseRelase;
            this.MouseDoubleClick += MousedoubleClick;
        }
        protected void DoubleBuffer()
        {
            Type type = this.GetType();
            PropertyInfo prop = type.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(this, true);
            
        }
        private void AddStartModule()
        {
            StartModule startModule = new StartModule();
            startModule.SetRect(new Point(this.Width / 2 - 40, 60), true);
            modules.Add(startModule);
            this.Invalidate();
        }
        private void AddModule(object sender, EventArgs e)
        {
            ToolModule module = new ToolModule();
            module.SetRect(point, false);
            module.Text = module.Name + ModuleStr(module.GetType());
            // 用模块唯一文本作为ToolBlock的ToolName，避免多个ToolBlock共用同名.vpp互相覆盖
            module.GetToolBlock().ToolName = module.Text;
            modules.Add(module);
            this.Invalidate();
        }
        private string ModuleStr(Type type)
        {
            int i = modules.Count(m => m.GetType() == type);
            return i.ToString();
        }
        private void DelModule(object sender, EventArgs e)
        {
            foreach (var module in modules.ToList())
            {
                if (module.Rects[0].Contains(point))
                {
                    RemoveConnectionsForModule(module);
                    if (module.Input != null)
                    {
                        module.Input.Output = null;
                        module.Input = null;
                    }
                    if (module.Output != null)
                    {
                        module.Output.Input = null;
                        module.Output = null;
                    }
                    modules.Remove(module);
                    if (module is ToolModule toolModule)
                    {
                        // 清理TerConnections中引用该ToolBlock的终端连线（作为FromTool或ToTool），
                        // 避免Dispose后保存时仍写入已失效的连线数据
                        var tb = toolModule.GetToolBlock();
                        if (tb != null)
                        {
                            // 移除以该ToolBlock为FromTool的条目
                            TerConnections.Remove(tb);
                            // 移除其他ToolBlock下以该ToolBlock为ToTool的条目
                            foreach (var kvp in TerConnections)
                                kvp.Value.RemoveAll(p => p.ToTool == tb);
                        }
                        // 删除ToolBlock对应的.vpp保存文件（须在Dispose前取ToolName，Dispose后toolBlock=null）
                        if (tb != null && !string.IsNullOrEmpty(tb.ToolName))
                        {
                            string vppPath = Path.Combine(JsonDynamicHelper.GetAppRootPath(),
                                ProductManager.GetProcessDir(), tb.ToolName + ".vpp");
                            try
                            {
                                if (File.Exists(vppPath))
                                    File.Delete(vppPath);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"删除模块文件失败：{ex.Message}");
                            }
                        }
                        toolModule.Dispose();
                    }
                    this.Invalidate();
                    return;
                }
            }
        }
        private void RemoveConnectionsForModule(HToolBase.Module module)
        {
            _connections.RemoveAll(c => c.SourceModule == module || c.TargetModule == module);
        }
        private void ReName(object sender, EventArgs e)
        {
            foreach (var module in modules)
            {
                if (module.Rects[0].Contains(point))
                {
                    string str = Microsoft.VisualBasic.Interaction.InputBox("请输入内容：", "输入弹窗", "");
                    if (str == null)
                        return;
                    string oldName = module.Text;
                    // ToolModule需同步更新ToolBlock.ToolName并重命名.vpp文件
                    if (module is ToolModule toolModule && oldName != str)
                    {
                        var tb = toolModule.GetToolBlock();
                        if (tb != null)
                        {
                            // 构建旧文件路径（直接拼接，不调用GetSafeFilePath以免创建目录）
                            string oldVppPath = Path.Combine(JsonDynamicHelper.GetAppRootPath(),
                                ProductManager.GetProcessDir(), oldName + ".vpp");
                            // 同步更新ToolBlock名称与根节点文本
                            tb.ToolName = str;
                            if (tb.RootNode != null)
                                tb.RootNode.Text = str;
                            // 构建新文件路径
                            string newVppPath = Path.Combine(JsonDynamicHelper.GetAppRootPath(),
                                ProductManager.GetProcessDir(), str + ".vpp");
                            // 旧.vpp文件存在则重命名为新名称
                            if (File.Exists(oldVppPath))
                            {
                                try
                                {
                                    if (File.Exists(newVppPath))
                                        File.Delete(newVppPath);
                                    File.Move(oldVppPath, newVppPath);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"重命名模块文件失败：{ex.Message}");
                                }
                            }
                        }
                    }
                    module.Text = str;
                    this.Invalidate();
                    return;
                }
            }
        }
        private void ShowTerminal(object sender, EventArgs e)
        {
            foreach (var module in modules.ToList())
            {
                if (module.Rects[0].Contains(point)&&module is ToolModule toolmodule)
                {
                    ToolBlockTerminalForm terminal = new ToolBlockTerminalForm(this, toolmodule.GetToolBlock());
                    terminal.ShowDialog();
                }
            }
            
        }
        private void Panel_Paint(object sender, PaintEventArgs e)
        {
            foreach (var module in modules) DrawModule(e.Graphics, module);
            foreach (var conn in _connections) DrawConnection(e.Graphics, conn);
            if (_tempConnection != null) DrawTemporaryConnection(e.Graphics, _tempConnection);
        }
        private void DrawModule(Graphics g, HToolBase.Module module)
        {
            Brush fillBrush = module.IsExecuted ? Brushes.MediumSpringGreen : Brushes.Red;
            Rectangle rect = module.Rects[0];
            g.FillRectangle(fillBrush, rect);
            g.DrawRectangle(Pens.Black, rect);
            g.DrawString(module.Text, Font, Brushes.Black, rect.X + 20, rect.Y + 25);

            if (module is StartModule)
                g.FillRectangle(Brushes.Red, module.Rects[1]);
            else
            {
                g.FillRectangle(Brushes.Blue, module.Rects[1]);
                g.FillRectangle(Brushes.Red, module.Rects[2]);
            }
        }
        private void DrawConnection(Graphics g, Connection conn)
        {
            Point start = GetTerminalCenter(conn.SourceModule, TerminalType.Output);
            Point end = GetTerminalCenter(conn.TargetModule, TerminalType.Input);
            int midY = (start.Y + end.Y) / 2;
            Point[] points = { start, new Point(start.X, midY), new Point(end.X, midY), end };

            using (Pen pen = new Pen(Color.Black, 2))
            {
                g.DrawLines(pen, points);
                DrawArrow(g, end, points[2], pen.Color);
            }
        }
        private Point GetTerminalCenter(HToolBase.Module module, TerminalType type)
        {
            Rectangle rect = type == TerminalType.Input ? module.Inputrectangle : module.Outputrectangle;
            return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        }

        private void DrawArrow(Graphics g, Point end, Point prev, Color color)
        {
            const int arrowSize = 8;
            Point dir = new Point(end.X - prev.X, end.Y - prev.Y);
            float len = (float)Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            if (len == 0) return;
            PointF unitDir = new PointF(dir.X / len, dir.Y / len);
            PointF perpendicular = new PointF(-unitDir.Y, unitDir.X);

            PointF arrow1 = new PointF(
                end.X - unitDir.X * arrowSize - perpendicular.X * arrowSize / 2,
                end.Y - unitDir.Y * arrowSize - perpendicular.Y * arrowSize / 2);
            PointF arrow2 = new PointF(
                end.X - unitDir.X * arrowSize + perpendicular.X * arrowSize / 2,
                end.Y - unitDir.Y * arrowSize + perpendicular.Y * arrowSize / 2);

            using (SolidBrush brush = new SolidBrush(color))
                g.FillPolygon(brush, new[] { end, arrow1, arrow2 });
        }
        private void DrawTemporaryConnection(Graphics g, TempConnection tempConn)
        {
            using (Pen pen = new Pen(Color.Gray, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                g.DrawLine(pen, tempConn.StartPoint, tempConn.EndPoint);
        }
        private (HToolBase.Module Module, TerminalType Type) GetTerminalAt(Point location)
        {
            foreach (var module in modules)
            {
                if (!(module is StartModule) && module.Inputrectangle.Contains(location))
                    return (module, TerminalType.Input);
                if (module.Outputrectangle.Contains(location))
                    return (module, TerminalType.Output);
            }
            return (null, TerminalType.Input);
        }
        private void Panel_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                point = e.Location;
                foreach (HToolBase.Module module in modules)
                {
                    if (module.Rects[0].Contains(e.Location) && !(module is StartModule))
                    {
                        TermianlModule = module;
                        SelectModelMenu.Show(this, e.Location);
                        return;
                    }
                }
                contextMenu.Show(this, e.Location);
                return;
            }
            else
            {
                var (module, terminalType) = GetTerminalAt(e.Location);
                _tempConnection = null;
                if (module != null)
                {
                    _currentTerminal = (module, terminalType);
                    _tempConnection = new TempConnection
                    {
                        SourceModule = module,
                        SourceTerminal = terminalType,
                        StartPoint = GetTerminalCenter(module, terminalType),
                        EndPoint = e.Location
                    };
                }
                else
                {
                    foreach (HToolBase.Module modelBase in modules)
                    {
                        if (modelBase.Rects[0].Contains(e.Location))
                        {
                            SelectModule = modelBase;
                            return;
                        }
                    }
                }
            }
        }
        private void Panel_MouseMove(object sender, MouseEventArgs e)
        {
            if (SelectModule != null && e.Button == MouseButtons.Left)
            {
                if (SelectModule is StartModule start)
                    start.SetRect(e.Location, true);
                else
                    SelectModule.SetRect(e.Location, false);
                this.Invalidate();
            }
            if (_tempConnection != null)
            {
                _tempConnection.EndPoint = e.Location;
                this.Invalidate();
            }
        }
        private void RemoveOldConnections(HToolBase.Module module, TerminalType type)
        {
            if (type == TerminalType.Input)
                _connections.RemoveAll(c => c.TargetModule == module);
            else
                _connections.RemoveAll(c => c.SourceModule == module);
        }
        private void Panel_MouseRelase(object sender, MouseEventArgs e)
        {
            if (SelectModule != null)
                SelectModule = null;

            if (_tempConnection != null)
            {
                var (targetModule, targetType) = GetTerminalAt(e.Location);
                if (IsValidConnection(_tempConnection.SourceModule, _tempConnection.SourceTerminal, targetModule, targetType))
                {
                    RemoveOldConnections(targetModule, TerminalType.Input);
                    _tempConnection.SourceModule.Output = targetModule;
                    targetModule.Input = _tempConnection.SourceModule;
                    RemoveOldConnections(_tempConnection.SourceModule, TerminalType.Output);
                    _connections.Add(new Connection(_tempConnection.SourceModule, targetModule));
                }
                _tempConnection = null;
            }
            this.Invalidate();
        }
        private bool IsValidConnection(HToolBase.Module source, TerminalType sourceType, HToolBase.Module target, TerminalType targetType)
        {
            if (source == null || target == null) return false;
            if (source == target) return false;
            if (sourceType != TerminalType.Output || targetType != TerminalType.Input) return false;
            if (source is StartModule && target is StartModule) return false;
            return true;
        }
        private void MousedoubleClick(object sender, MouseEventArgs e)
        {
            foreach (var module in modules)
            {
                if (module.Rects[0].Contains(e.Location) && module is ToolModule toolModule)
                    toolModule.Show();
            }
        }
        public void Run()
        {
            Task.Run(() =>
            {
                RunCompelete = false;
                Queue<HToolBase.Module> queue = new Queue<HToolBase.Module>();
                foreach (HToolBase.Module module in modules)
                {
                    module.IsExecuted=false;
                    if (module is StartModule)
                    {
                        queue.Enqueue(module);
                        //break;
                    }
                }
                this.Invalidate();
                while (queue.Count > 0)
                {
                    HToolBase.Module module = queue.Peek();
                    if (module == null) return;
                    module.Run();
                    if (module is ToolModule toolModule)
                        SpreadValue(toolModule.GetToolBlock());
                    this.Invalidate();
                    if (queue.First().Output != null)
                    {
                        queue.Enqueue(queue.First().Output);
                        queue.Dequeue();
                        continue;
                    }
                    break;
                }
                RunCompelete = true;
            });
        }

        /// <summary>
        /// 清空并释放所有模块、连线及内部状态资源（供切换产品/重新加载前调用）。
        /// </summary>
        public void ClearAll()
        {
            // 1. 释放所有模块（ToolModule 递归释放 ToolBlock）
            foreach (var module in modules.ToList())
            {
                if (module is IDisposable disp) disp.Dispose();
            }
            modules.Clear();

            // 2. 清空连线
            _connections.Clear();

            // 3. 清空交互状态
            SelectModule = null;
            MoveModule = null;
            InputModule = null;
            OutputModule = null;
            TermianlModule = null;
            CopyModule = null;
            _tempConnection = null;
            _currentTerminal = (null, TerminalType.Input);
            TerConnections.Clear();
            this.Invalidate();
        }
        public bool TerMinalConnectionAdd(ToolBlock FromTool,PortNode FromPort, ToolBlock ToTool, PortNode ToPort)
        {
            ToolBlock fromtool = ((ToolModule)this.modules.Cast<HToolBase.Module>().FirstOrDefault(n => n is ToolModule tool && tool.GetToolBlock() == FromTool)).GetToolBlock();
            ToolBlock totool = ((ToolModule)this.modules.Cast<HToolBase.Module>().FirstOrDefault(n => n is ToolModule tool && tool.GetToolBlock() == ToTool)).GetToolBlock();
            PortNode fromport = fromtool.Outputs.Values.Cast<PortNode>().FirstOrDefault(n=>n== FromPort);
            PortNode toport = fromtool.Outputs.Values.Cast<PortNode>().FirstOrDefault(n => n == FromPort);
            if (fromtool == null && totool == null && fromport == null && toport == null)
            {
                return false;
            }
            DisconnectPort(fromtool, fromport, totool, toport);
            if (TerConnections.ContainsKey(fromtool))
            {
                foreach (PortConnection port in TerConnections[FromTool])
                {
                    if (port.FromPort == fromport && port.ToTool == totool && port.ToPort == toport)
                        return false;
                }
                PortConnection p = new PortConnection();
                p.FromTool = FromTool;
                p.FromPort = FromPort;
                p.PortType = FromPort.PortType;
                p.ToTool = ToTool;
                p.ToPort = ToPort;
                TerConnections[FromTool].Add(p);
                return true;
            }
            else
            {
                PortConnection p = new PortConnection();
                p.FromTool = FromTool;
                p.FromPort = FromPort;
                p.PortType = FromPort.PortType;
                p.ToTool = ToTool;
                p.ToPort = ToPort;
                TerConnections.Add(FromTool, new List<PortConnection>());
                TerConnections[FromTool].Add(p);
                return true;
            }
        }
        public void DisconnectPort(ToolBase fromTool, PortNode fromPort, ToolBase toTool, PortNode toPort)
        {
            if (TerConnections.ContainsKey(fromTool))
            {
                TerConnections[fromTool].RemoveAll(p =>
                    p.FromPort == fromPort && p.ToTool == toTool && p.ToPort == toPort);

                if (TerConnections[fromTool].Count == 0)
                    TerConnections.Remove(fromTool);
            }
        }
        private void SpreadValue(ToolBlock toolBlock)
        {
            if (!TerConnections.ContainsKey(toolBlock))
                return;
            foreach (PortConnection portConnection in TerConnections[toolBlock])
            {
                portConnection.ToTool.Inputs[portConnection.ToPort.Text].Value = portConnection.FromTool.Outputs[portConnection.FromPort.Text].Value;
            }
        }
        /// <summary>
        /// 保存ProcessPanel布局与连线到指定目录，并保存每个ToolModule的ToolBlock配置。
        /// 目录结构：{folder}/{PanelName}.json + {folder}/{ToolName}.vpp
        /// </summary>
        public void SaveToFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            string panelName = Path.GetFileName(folder);
            this.PanelName = panelName;
            // 设置当前ProcessPanel上下文，供ToolBlock构建路径使用（System/{产品}/{PanelName}）
            ProductManager.CurrentProcessPanel = panelName;

            // 1. 保存每个ToolModule的ToolBlock配置（单个失败不影响整体）
            foreach (var module in modules)
            {
                if (module is ToolModule tm)
                {
                    try { tm.GetToolBlock()?.SaveTools(false); }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"模块[{module.Text}]的ToolBlock保存失败：{ex.Message}");
                    }
                }
            }

            // 2. 保存模块布局
            var saveData = new ProcessPanelSaveData();
            foreach (var module in modules)
            {
                saveData.Modules.Add(new ModuleSaveData
                {
                    Type = module.GetType().Name,
                    Text = module.Text,
                    Name = module.Name,
                    X = module.Position.X,
                    Y = module.Position.Y
                });
            }

            // 3. 保存连线（按模块索引引用）
            foreach (var conn in _connections)
            {
                saveData.Connections.Add(new ProcessConnectionSaveData
                {
                    SourceIndex = modules.IndexOf(conn.SourceModule),
                    TargetIndex = modules.IndexOf(conn.TargetModule)
                });
            }

            // 4. 保存终端连线（跨ToolModule端口值传递，按工具名+端口名引用）
            foreach (var kvp in TerConnections)
            {
                foreach (var conn in kvp.Value)
                {
                    saveData.TerConnections.Add(new TerConnectionSaveData
                    {
                        FromToolName = conn.FromTool?.ToolName,
                        FromPortName = conn.FromPort?.PortName,
                        ToToolName = conn.ToTool?.ToolName,
                        ToPortName = conn.ToPort?.PortName,
                        PortType = conn.PortType.ToString()
                    });
                }
            }

            string filePath = Path.Combine(folder, panelName + ".json");
            string json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 从指定目录加载ProcessPanel布局与连线。
        /// </summary>
        public void LoadFromFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            string panelName = Path.GetFileName(folder);
            this.PanelName = panelName;
            ProductManager.CurrentProcessPanel = panelName;

            string filePath = Path.Combine(folder, panelName + ".json");
            if (!File.Exists(filePath)) return;

            ProcessPanelSaveData saveData;
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                saveData = JsonConvert.DeserializeObject<ProcessPanelSaveData>(json);
            }
            catch
            {
                MessageBox.Show("流程文件解析失败：" + filePath);
                return;
            }
            if (saveData == null) return;

            // 1. 清空现有模块与连线
            ClearAll();

            // 2. 重建模块
            for (int i = 0; i < saveData.Modules.Count; i++)
            {
                var m = saveData.Modules[i];
                HToolBase.Module module = null;
                if (m.Type == "StartModule")
                {
                    module = new StartModule();
                    module.SetRect(new Point(m.X, m.Y), true);
                }
                else if (m.Type == "ToolModule")
                {
                    module = new ToolModule();
                    module.SetRect(new Point(m.X, m.Y), false);
                }
                if (module != null)
                {
                    module.Name = m.Name;
                    module.Text = m.Text;
                    // 恢复ToolBlock的唯一ToolName，保证加载/保存路径与保存时一致
                    if (module is ToolModule tm)
                    {
                        tm.GetToolBlock().ToolName = m.Text;
                        tm.GetToolBlock().LoadVpp();
                    }
                    modules.Add(module);
                }
            }

            // 3. 重建连线
            foreach (var c in saveData.Connections)
            {
                if (c.SourceIndex >= 0 && c.SourceIndex < modules.Count &&
                    c.TargetIndex >= 0 && c.TargetIndex < modules.Count)
                {
                    var src = modules[c.SourceIndex];
                    var tgt = modules[c.TargetIndex];
                    src.Output = tgt;
                    tgt.Input = src;
                    _connections.Add(new Connection(src, tgt));
                }
            }

            // 4. 重建终端连线（跨ToolModule端口值传递）
            //    须在模块加载完成后进行，因为需要通过ToolName查找ToolBlock、通过PortName查找端口
            foreach (var tc in saveData.TerConnections)
            {
                var fromTool = LookupToolBlock(tc.FromToolName);
                var toTool = LookupToolBlock(tc.ToToolName);
                if (fromTool == null || toTool == null) continue;
                PortNode fromPort = LookupTerPort(fromTool, tc.FromPortName);
                PortNode toPort = LookupTerPort(toTool, tc.ToPortName);
                if (fromPort == null || toPort == null) continue;
                // 直接构建PortConnection并添加，避免TerMinalConnectionAdd中的重复查找
                PortConnection p = new PortConnection
                {
                    FromTool = fromTool,
                    FromPort = fromPort,
                    PortType = fromPort.PortType,
                    ToTool = toTool,
                    ToPort = toPort
                };
                if (!TerConnections.ContainsKey(fromTool))
                    TerConnections[fromTool] = new List<PortConnection>();
                TerConnections[fromTool].Add(p);
            }

            this.Invalidate();
        }

        /// <summary>按ToolName查找ToolModule对应的ToolBlock</summary>
        private ToolBlock LookupToolBlock(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return null;
            foreach (var module in modules)
            {
                if (module is ToolModule tm && tm.GetToolBlock()?.ToolName == toolName)
                    return tm.GetToolBlock();
            }
            return null;
        }

        /// <summary>按PortName在ToolBlock的Inputs/Outputs中查找端口</summary>
        private PortNode LookupTerPort(ToolBlock tool, string portName)
        {
            if (tool == null || string.IsNullOrEmpty(portName)) return null;
            if (tool.Outputs.TryGetValue(portName, out var outPort))
                return outPort;
            if (tool.Inputs.TryGetValue(portName, out var inPort))
                return inPort;
            return null;
        }
    }
}
