using HToolBase.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
namespace HToolBase.Controls
{
    #region 连线数据类

    /// <summary>
    /// 连线数据类，表示两个端口之间的一条连线
    /// </summary>
    public class ConnectionLine
    {
        /// <summary>
        /// 连线起始端口（输出端口）
        /// </summary>
        public PortNode FromPort;

        /// <summary>
        /// 连线目标端口（输入端口）
        /// </summary>
        public PortNode ToPort;
    }

    /// <summary>
    /// 连线完成事件参数，在连线创建完成时传递源端口和目标端口
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// 连线起始端口
        /// </summary>
        public PortNode FromPort { get; }

        /// <summary>
        /// 连线目标端口
        /// </summary>
        public PortNode ToPort { get; }

        /// <summary>
        /// 构造函数，初始化连线事件参数
        /// </summary>
        /// <param name="fromPort">起始端口</param>
        /// <param name="toPort">目标端口</param>
        public ConnectionEventArgs(PortNode fromPort, PortNode toPort)
        {
            FromPort = fromPort;
            ToPort = toPort;
        }
    }
    #endregion

    #region 自定义TreeView，支持连线绘制

    /// <summary>
    /// 自定义TreeView控件，支持节点间连线绘制、拖拽连线、连线预览和选中高亮功能
    /// 通过缓存Bitmap实现防闪烁绘制，支持障碍物规避算法绘制安全路径
    /// </summary>
    public class ConnectionTreeView : TreeView
    {
        /// <summary>
        /// 当前所有连线集合
        /// </summary>
        public List<ConnectionLine> Connections { get; set; } = new List<ConnectionLine>();

        /// <summary>
        /// 默认连线画笔（浅蓝色）
        /// </summary>
        public Pen ConnectionPen { get; set; } = new Pen(Color.FromArgb(0, 120, 215), 2);

        /// <summary>
        /// 选中连线画笔（深蓝色），用于高亮显示与选中节点相关的连线
        /// </summary>
        public Pen SelectedPen { get; set; } = new Pen(Color.FromArgb(0, 50, 120), 2);

        /// <summary>
        /// 高亮填充画刷，用于悬停目标端口时显示圆点
        /// </summary>
        public Brush HighlightBrush { get; set; } = new SolidBrush(Color.FromArgb(50, 255, 140, 0));

        /// <summary>
        /// 当前是否处于拖拽连线状态
        /// </summary>
        public bool IsDragging { get; set; } = false;

        /// <summary>
        /// 拖拽连线时的起始端口（输出端口）
        /// </summary>
        public PortNode DragStartPort { get; set; }

        /// <summary>
        /// 当前鼠标位置，用于绘制拖拽中的预览连线
        /// </summary>
        public Point CurrentMousePoint { get; set; } = Point.Empty;

        /// <summary>
        /// 鼠标悬停的端口节点，用于判断是否可连接
        /// </summary>
        public PortNode HoverPort { get; set; }

        /// <summary>
        /// 悬停端口是否可连接，用于控制鼠标样式和预览连线显示
        /// </summary>
        public bool CanConnectHover { get; set; } = false;

        /// <summary>
        /// 鼠标是否悬停在"输出"根节点上，用于显示添加样式光标
        /// </summary>
        public bool IsHoverOutputRoot { get; set; } = false;

        /// <summary>
        /// 所属的ToolTreeviewControl控件引用，用于获取连接判断和绘制服务
        /// </summary>
        public ToolTreeviewControl OwnerControl { get; set; }

        /// <summary>
        /// 当前选中的节点，用于高亮相关连线
        /// </summary>
        public TreeNode SelectedNode { get; set; }

        /// <summary>
        /// Windows消息常量：绘制消息
        /// </summary>
        private const int WM_PAINT = 0x000F;

        /// <summary>
        /// Windows消息常量：擦除背景消息，拦截此消息可消除闪烁
        /// </summary>
        private const int WM_ERASEBKGND = 0x0014;

        /// <summary>
        /// 连线绘制缓存位图，所有连线先绘制到此位图，再一次性blit到屏幕
        /// </summary>
        private Bitmap _connectionBitmap;

        /// <summary>
        /// 缓存位图是否需要重绘的脏标记，true时RenderConnectionsToBitmap会重新绘制
        /// </summary>
        private bool _bitmapDirty = true;

        /// <summary>
        /// 构造函数，初始化控件样式以启用防闪烁绘制
        /// </summary>
        public ConnectionTreeView()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.ResizeRedraw, true);
        }

        /// <summary>
        /// 控件大小改变时标记缓存位图为脏，触发重绘
        /// </summary>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            _bitmapDirty = true;
        }

        /// <summary>
        /// 标记连线需要重绘并触发控件重绘
        /// </summary>
        public void InvalidateConnections()
        {
            _bitmapDirty = true;
            this.Invalidate();
        }

        /// <summary>
        /// Windows消息处理，拦截WM_ERASEBKGND消除背景擦除闪烁，
        /// 在WM_PAINT时调用DrawConnections绘制连线
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            // 控件已销毁时直接返回，避免base.WndProc访问已释放的TreeView内部状态抛NullReferenceException
            if (this.IsDisposed)
                return;

            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = IntPtr.Zero;
                return;
            }
            try
            {
                base.WndProc(ref m);
            }
            catch (Exception ex)
            {

            }


            if (m.Msg == WM_PAINT)
            {
                DrawConnections();
            }
        }

        /// <summary>
        /// 鼠标移动事件，拖拽连线时实时更新悬停端口状态、鼠标样式和预览连线
        /// 支持三种状态：可连接（手型）、悬停输出根节点（手型）、不可连接（禁止）
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (IsDragging && DragStartPort != null)
            {
                TreeNode node = this.GetNodeAt(e.Location);
                HoverPort = (node is PortNode portNode) ? portNode : null;

                if (HoverPort != null /*&& OwnerControl != null*/)
                {
                    CanConnectHover = OwnerControl.CanConnect(DragStartPort, HoverPort);
                    IsHoverOutputRoot = false;
                    this.Cursor = CanConnectHover ? Cursors.Hand : Cursors.No;
                }
                else if (OwnerControl != null && OwnerControl.IsOutputRootNode(node))
                {
                    CanConnectHover = false;
                    IsHoverOutputRoot = true;
                    this.Cursor = Cursors.Cross;
                }
                else if (OwnerControl != null && OwnerControl.IsToolBlockToolRootNode(node))
                {
                    // 悬停在ToolBlock工具根节点上，显示可放置样式
                    CanConnectHover = false;
                    IsHoverOutputRoot = false;
                    this.Cursor = Cursors.Cross;
                }
                else
                {
                    CanConnectHover = false;
                    IsHoverOutputRoot = false;
                    this.Cursor = Cursors.No;
                }

                CurrentMousePoint = e.Location;
                _bitmapDirty = true;
                this.Invalidate();
            }
        }

        /// <summary>
        /// 鼠标释放事件，结束拖拽连线状态，重置所有拖拽相关属性并恢复默认鼠标样式
        /// </summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool wasDragging = IsDragging;
            base.OnMouseUp(e);

            if (wasDragging)
            {
                IsDragging = false;
                DragStartPort = null;
                HoverPort = null;
                CanConnectHover = false;
                IsHoverOutputRoot = false;
                CurrentMousePoint = Point.Empty;
                this.Cursor = Cursors.Default;
                _bitmapDirty = true;
                this.Invalidate();
            }
        }

        /// <summary>
        /// 绘制连线入口，从缓存位图快速blit到屏幕，
        /// 仅在缓存脏或尺寸变化时重新渲染连线
        /// </summary>
        private void DrawConnections()
        {
            if (_bitmapDirty || _connectionBitmap == null ||
                _connectionBitmap.Width != this.ClientSize.Width ||
                _connectionBitmap.Height != this.ClientSize.Height)
            {
                RenderConnectionsToBitmap();
                _bitmapDirty = false;
            }

            if (_connectionBitmap != null)
            {
                using (Graphics g = Graphics.FromHwnd(this.Handle))
                {
                    g.DrawImageUnscaled(_connectionBitmap, 0, 0);
                }
            }
        }

        /// <summary>
        /// 将所有连线渲染到缓存位图，包括：已有连线、选中高亮连线、拖拽预览连线
        /// 支持连线偏移（多连线分组）和障碍物规避绘制
        /// </summary>
        private void RenderConnectionsToBitmap()
        {
            int w = Math.Max(1, this.ClientSize.Width);
            int h = Math.Max(1, this.ClientSize.Height);

            if (_connectionBitmap == null || _connectionBitmap.Width != w || _connectionBitmap.Height != h)
            {
                _connectionBitmap?.Dispose();
                _connectionBitmap = new Bitmap(w, h);
            }

            using (Graphics g = Graphics.FromImage(_connectionBitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                if (OwnerControl != null)
                {
                    var connectionOffsets = CalculateConnectionOffsets();

                    foreach (ConnectionLine line in Connections)
                    {
                        Point fromPoint = OwnerControl.GetPortPosition(line.FromPort);
                        Point toPoint = OwnerControl.GetPortPosition(line.ToPort);
                        int xOffset = connectionOffsets.ContainsKey(line) ? connectionOffsets[line] : 0;
                        var obstacles = OwnerControl.GetNodeObstacles(line.FromPort, line.ToPort);

                        Pen penToUse = IsConnectionSelected(line) ? SelectedPen : ConnectionPen;
                        OwnerControl.DrawConnectionLine(g, fromPoint, toPoint, penToUse, xOffset, obstacles);
                    }

                    if (IsDragging && DragStartPort != null)
                    {
                        Point startPoint = OwnerControl.GetPortPosition(DragStartPort);

                        if (HoverPort != null && CanConnectHover)
                        {
                            Point hoverPoint = OwnerControl.GetPortPosition(HoverPort);
                            var obstacles = OwnerControl.GetNodeObstacles(DragStartPort, HoverPort);
                            OwnerControl.DrawConnectionLine(g, startPoint, hoverPoint, ConnectionPen, 0, obstacles);
                            g.FillEllipse(HighlightBrush, hoverPoint.X - 5, hoverPoint.Y - 5, 10, 10);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 判断指定连线是否与当前选中节点相关，用于选中高亮
        /// 判断逻辑：直接端口匹配 → 可见祖先匹配（支持折叠节点）
        /// </summary>
        /// <param name="line">待判断的连线</param>
        /// <returns>是否需要高亮显示</returns>
        private bool IsConnectionSelected(ConnectionLine line)
        {
            if (SelectedNode == null)
                return false;

            if (line.FromPort == SelectedNode || line.ToPort == SelectedNode)
                return true;

            if (SelectedNode is PortNode selectedPort)
            {
                if (line.FromPort == selectedPort || line.ToPort == selectedPort)
                    return true;
            }

            TreeNode visibleFrom = OwnerControl?.GetVisibleAncestor(line.FromPort);
            TreeNode visibleTo = OwnerControl?.GetVisibleAncestor(line.ToPort);
            TreeNode visibleSelected = OwnerControl?.GetVisibleAncestor(SelectedNode);

            if (visibleFrom == visibleSelected || visibleTo == visibleSelected)
                return true;

            return false;
        }

        /// <summary>
        /// 计算多连线偏移量，从同一输出节点出发的多条连线按Y坐标排序后分配X方向偏移，
        /// 避免多条连线重叠
        /// </summary>
        /// <returns>连线到X偏移量的映射字典</returns>
        private Dictionary<ConnectionLine, int> CalculateConnectionOffsets()
        {
            var offsets = new Dictionary<ConnectionLine, int>();

            var groups = new Dictionary<PortNode, List<ConnectionLine>>();
            foreach (var conn in Connections)
            {
                PortNode groupKey = GetPortGroupKey(conn.FromPort);
                if (!groups.ContainsKey(groupKey))
                    groups[groupKey] = new List<ConnectionLine>();
                groups[groupKey].Add(conn);
            }

            int spacing = 5;
            foreach (var kvp in groups)
            {
                var connections = kvp.Value;
                connections.Sort((a, b) =>
                {
                    int yA = a.ToPort != null ? GetPortY(a.ToPort) : 0;
                    int yB = b.ToPort != null ? GetPortY(b.ToPort) : 0;
                    return yA.CompareTo(yB);
                });

                for (int i = 0; i < connections.Count; i++)
                {
                    offsets[connections[i]] = i * spacing;
                }
            }

            return offsets;
        }

        /// <summary>
        /// 获取端口的分组键，使用可见祖先节点作为分组依据，支持折叠节点
        /// </summary>
        /// <param name="port">端口节点</param>
        /// <returns>用于分组的端口节点</returns>
        private PortNode GetPortGroupKey(PortNode port)
        {
            TreeNode visible = OwnerControl != null ? OwnerControl.GetVisibleAncestor(port) : port;
            if (visible is PortNode visiblePort)
                return visiblePort;
            return port;
        }

        /// <summary>
        /// 获取端口的Y坐标，使用可见祖先节点的位置（支持折叠节点）
        /// </summary>
        /// <param name="port">端口节点</param>
        /// <returns>Y坐标值</returns>
        private int GetPortY(PortNode port)
        {
            TreeNode visible = OwnerControl != null ? OwnerControl.GetVisibleAncestor(port) : port;
            return visible.Bounds.Top;
        }
    }
    #endregion

    /// <summary>
    /// 工具箱TreeView控件，支持节点管理、连线绘制、拖拽连接、选中高亮和障碍物规避功能
    /// </summary>
    public partial class ToolTreeviewControl : UserControl
    {
        /// <summary>
        /// 当前绑定的工具块，用于事件回调（添加输入/输出）和连接操作
        /// </summary>
        ToolBlock ToolBlock;

        /// <summary>
        /// 图像源工具实例（调试用）
        /// </summary>
        ImageSourceTool imageSourceTool = new ImageSourceTool();

        /// <summary>
        /// 图标列表，存储节点图标
        /// </summary>
        ImageList imageList = new ImageList();

        /// <summary>
        /// 图标文件名列表，定义各节点类型对应的图标文件
        /// </summary>
        List<String> imageNames = new List<string> { "CogImageFileTool.ico", "Input.png", "Output.png", "CogToolBlock.ico" };

        #region //连线属性

        /// <summary>
        /// 当前所有连线的集合
        /// </summary>
        private List<ConnectionLine> _connections = new List<ConnectionLine>();

        /// <summary>
        /// canonical端口→编辑器显示克隆端口的映射（仅含本ToolBlock自身端口）。
        /// 为保证两个树视图（父级视图显示RootNode下的canonical端口、本编辑器视图显示克隆端口）
        /// 使用完全独立的TreeNode实例，编辑器"输入"/"输出"根节点下放的是克隆节点，而非canonical。
        /// 内部工具的端口无需克隆（它们只存在于本编辑器树中），故不在映射里。
        /// </summary>
        private Dictionary<PortNode, PortNode> _canonicalToDisplay = new Dictionary<PortNode, PortNode>();
        private Dictionary<PortNode, PortNode> _displayToCanonical = new Dictionary<PortNode, PortNode>();

        /// <summary>
        /// 当前是否处于拖拽连线状态
        /// </summary>
        private bool _isDragging = false;

        /// <summary>
        /// 拖拽连线时的起始端口（输出端口）
        /// </summary>
        private PortNode _dragStartPort = null;

        /// <summary>
        /// 默认连线画笔（浅蓝色）
        /// </summary>
        private Pen _connectionPen = new Pen(Color.FromArgb(0, 120, 215), 2);

        /// <summary>
        /// 选中连线画笔（深蓝色），用于高亮显示与选中节点相关的连线
        /// </summary>
        private Pen _selectedPen = new Pen(Color.FromArgb(0, 50, 120), 2);

        /// <summary>
        /// 高亮填充画刷，用于悬停目标端口时显示圆点
        /// </summary>
        private Brush _highlightBrush = new SolidBrush(Color.FromArgb(50, 255, 140, 0));

        /// <summary>
        /// 连线完成事件，在拖拽释放到有效目标后触发
        /// </summary>
        public event EventHandler<ConnectionEventArgs> ConnectionCompleted;

        #endregion


        ContextMenuStrip MenuStrip = new ContextMenuStrip();
        TreeNode selectnode;
        /// <summary>
        /// 构造函数，初始化控件、配置连线属性和事件处理
        /// </summary>
        /// <param name="toolBlock">绑定的工具块实例</param>
        public ToolTreeviewControl(ToolBlock toolBlock)
        {
            InitializeComponent();
            DoubleBuffer();
            var connTreeView = this.treeView1 as ConnectionTreeView;
            if (connTreeView != null)
            {
                connTreeView.OwnerControl = this;
                connTreeView.Connections = _connections;
                connTreeView.ConnectionPen = _connectionPen;
                connTreeView.SelectedPen = _selectedPen;
                connTreeView.HighlightBrush = _highlightBrush;
            }
            MenuStrip.Items.Add("添加终端", null, (s, e) => { new ToolTerminalForm(this.ToolBlock.Tools[selectnode.Text]).ShowDialog(); });
            #region //鼠标事件
            this.treeView1.DrawMode = TreeViewDrawMode.OwnerDrawText;
            this.treeView1.MouseDown += TreeView1_MouseDown;
            this.treeView1.MouseUp += TreeView1_MouseUp;
            this.treeView1.DrawNode += TreeView1_DrawNode;
            this.treeView1.AfterExpand += TreeView1_AfterExpand;
            this.treeView1.AfterCollapse += TreeView1_AfterCollapse;
            this.treeView1.AfterSelect += TreeView1_AfterSelect;
            this.ConnectionCompleted += OnConnectionCompleted;
            this.treeView1.NodeMouseDoubleClick += TreeView1_NodeMouseDoubleClick;

            #endregion
            SetImageList();
            ToolBlock = toolBlock;
            ToolBlock.Tools.ToolAdded += Tools_ToolAdded;
            ToolBlock.AddInputEvent += OnAddInputEvent;
            ToolBlock.AddOutputEvent += OnAddOutputEvent;

#if DEBUG
            //ToolBlock.Tools.Add(imageSourceTool.ToolName, imageSourceTool);
            //treeView1.MouseDoubleClick += TreeView1_MouseDoubleClick;
#endif

            // 延迟到控件创建句柄并加入消息循环后再加载，避免未父化时强制创建句柄导致布局递归
            if (this.IsHandleCreated)
            {
                BeginInvoke(new Action(InitializeLoadedData));
            }
            else
            {
                EventHandler onHandle = null;
                onHandle = (s, e) =>
                {
                    this.HandleCreated -= onHandle;
                    BeginInvoke(new Action(InitializeLoadedData));
                };
                this.HandleCreated += onHandle;
            }
        }

        private void TreeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Parent != null && e.Node.Parent.Text == "工具")
            {
                string fullClassName = this.ToolBlock.Tools[e.Node.Text].GetType() == typeof(ToolBlock) ? "HToolBase.Controls." + this.ToolBlock.Tools[e.Node.Text].Parent.RootNode.Text + "Control" : "HToolBase.Controls." + this.ToolBlock.Tools[e.Node.Text].RootNode.Tag + "Form";
                Assembly assembly =
                     Assembly.GetExecutingAssembly();
                Type type = assembly.GetType(fullClassName);
                if (type == null)
                    throw new TypeLoadException("$未找到类型 { fullClassName }");
                if (!typeof(HForm).IsAssignableFrom(type))
                    throw new ArgumentException("$类型 { fullClassName } 不是 ProcessTool 的子类");

                HForm f;
                if (type == typeof(ToolBlockControl))
                {
                    f = (ToolBlockControl)Activator.CreateInstance(type);
                    f.tool = this.ToolBlock.Tools[e.Node.Text];
                    ((ToolBlockControl)f).toolTreeview?.ReloadData();
                    f.Show();
                }
                else
                {
                    f = (HForm)Activator.CreateInstance(type);
                    f.tool = this.ToolBlock.Tools[e.Node.Text];
                    f.Show();
                }
                //this.ToolBlock.Tools[e.Node.Text].ShowWin();
                //tool.ShowWin();
            }
        }

        /// <summary>
        /// 延迟初始化：加载已有的工具、端口、连线到TreeView（控件父化和句柄创建之后）
        /// </summary>
        private void InitializeLoadedData()
        {
            ReloadData();
        }

        /// <summary>
        /// 清空TreeView所有子节点与连线，并从ToolBlock重新加载工具、端口、连线。
        /// 每次ShowWin→LoadVpp后调用，确保TreeView与最新ToolBlock数据同步，避免重复添加。
        /// </summary>
        public void ReloadData()
        {
            try
            {
                _isLoading = true;
                treeView1.BeginUpdate();
                try
                {
                    // 1. 清空连线、克隆映射和所有子节点（保留"输入"/"工具"/"输出"三个根节点）
                    _connections.Clear();
                    _canonicalToDisplay.Clear();
                    _displayToCanonical.Clear();
                    foreach (TreeNode rootNode in treeView1.Nodes)
                    {
                        rootNode.Nodes.Clear();
                    }

                    // 2. 从ToolBlock重新加载
                    FirstLoadTools();
                    FirstLoadPorts();
                    RestoreConnections();
                }
                finally
                {
                    treeView1.EndUpdate();
                    _isLoading = false;
                }
                // 全部加载完成后统一刷新一次连线
                RefreshConnections();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重新加载数据失败：{ex.Message}\n{ex.StackTrace}");
            }
        }

        private void Tools_ToolAdded(object sender, ToolAddedEventArgs e)
        {
            if (this.treeView1.InvokeRequired)
            {
                this.treeView1.BeginInvoke(new Action(() => AddNodes(e.Tool)));
            }
            else
            {
                AddNodes(e.Tool);
            }
        }

        /// <summary>
        /// 使用反射设置控件的DoubleBuffered属性为true，消除绘制闪烁
        /// </summary>
        protected void DoubleBuffer()
        {
            Type type2 = this.GetType();
            PropertyInfo prop2 = type2.GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);
            prop2.SetValue(this, true);
        }
        #region //绘图

        /// <summary>
        /// 自定义节点绘制事件，使用TextRenderer绘制节点文本和端口标识
        /// </summary>
        private void TreeView1_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            TreeNode node = e.Node;
            Rectangle bounds = node.Bounds;
            TextRenderer.DrawText(e.Graphics, node.Text, this.Font, bounds, this.ForeColor);
            if (node is PortNode portNode)
            {
                Point portPos = GetPortPosition(portNode);
                Brush brush = portNode.Direction == "Output" ? Brushes.LightGreen : Brushes.LightBlue;
            }
        }

        /// <summary>
        /// 节点展开事件处理，异步延迟刷新连线（等布局完成后再获取正确的Bounds）
        /// </summary>
        private void TreeView1_AfterExpand(object sender, TreeViewEventArgs e)
        {
            if (_isLoading) return;
            if (this.Created)
                this.BeginInvoke(new Action(() => RefreshConnections()));
        }

        /// <summary>
        /// 递归折叠标志，防止AfterCollapse事件在递归过程中重复触发
        /// </summary>
        private bool _isRecursiveCollapse = false;

        /// <summary>
        /// 批量加载标志，为true时抑制AfterExpand/AfterCollapse的异步刷新，
        /// 避免批量AddNodes/AddInput/AddOutput的ExpandAll引发大量RefreshConnections排队导致界面卡顿
        /// </summary>
        private bool _isLoading = false;

        /// <summary>
        /// 节点折叠事件处理，递归折叠所有子节点后异步刷新连线
        /// </summary>
        private void TreeView1_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            if (!_isRecursiveCollapse && !_isLoading && e.Node.Nodes.Count > 0)
            {
                _isRecursiveCollapse = true;
                try
                {
                    foreach (TreeNode child in e.Node.Nodes)
                    {
                        child.Collapse(false);
                    }
                }
                finally
                {
                    _isRecursiveCollapse = false;
                }
            }
            if (_isLoading) return;
            this.BeginInvoke(new Action(() => RefreshConnections()));
        }

        /// <summary>
        /// 节点选中事件处理，更新选中节点并触发连线重绘以高亮相关连线
        /// </summary>
        private void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var connTreeView = this.treeView1 as ConnectionTreeView;
            if (connTreeView != null)
            {
                connTreeView.SelectedNode = e.Node;
                connTreeView.InvalidateConnections();
            }
        }

        /// <summary>
        /// 鼠标释放事件，完成拖拽连线操作
        /// 判断目标是端口节点还是"输出"根节点，分别处理连接逻辑
        /// </summary>
        private void TreeView1_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.Capture = false;
                TreeNode node = this.treeView1.GetNodeAt(e.Location);
                var connTreeView = this.treeView1 as ConnectionTreeView;

                if (node is PortNode targetPort && _dragStartPort != null)
                {
                    if (CanConnect(_dragStartPort, targetPort))
                    {
                        ConnectionCompleted?.Invoke(this, new ConnectionEventArgs(_dragStartPort, targetPort));
                    }
                }
                else if (connTreeView != null && connTreeView.IsHoverOutputRoot && _dragStartPort != null)
                {
                    // 拖拽到"输出"根节点：自动添加ToolBlock输出端口并连线
                    string portName = _dragStartPort.PortName;
                    TypeName portType = _dragStartPort.PortType;
                    // 先计算去重后的实际名称（AddOutput内部也会去重），确保后续用正确名称取端口
                    string actualName = HToolBase.Tools.ToolBlock.GetUniquePortName(this.ToolBlock.Outputs, portName);
                    if (!this.ToolBlock.AddOutput(actualName, portType))
                        return;
                    // 连接层始终用canonical端口；_dragStartPort可能是克隆端口，需映射回canonical
                    var canonicalOut = this.ToolBlock.Outputs[actualName];
                    if (ToolBlock.ConnectPort(_dragStartPort.BelongTool, Canonical(_dragStartPort), this.ToolBlock, canonicalOut))
                    {
                        // 连线绘制需用编辑器树中的显示节点：_dragStartPort(显示) + 输出克隆(ToDisplay)
                        AddConnection(_dragStartPort, ToDisplay(canonicalOut));
                    }
                }
                else if (_dragStartPort != null && node != null && !(node is PortNode) && IsToolBlockToolRootNode(node))
                {
                    // 拖拽到ToolBlock工具的根节点：自动添加输入端口并连线
                    var targetTB = GetToolBlockByRootNode(node);
                    if (targetTB != null)
                    {
                        string portName = _dragStartPort.PortName;
                        TypeName portType = _dragStartPort.PortType;
                        string actualName = HToolBase.Tools.ToolBlock.GetUniquePortName(targetTB.Inputs, portName);
                        if (targetTB.AddInput(actualName, portType))
                        {
                            // newPort是targetTB的canonical端口，已在targetTB.RootNode下（本编辑器树中），无需克隆
                            var newPort = targetTB.Inputs[actualName];

                            if (ToolBlock.ConnectPort(_dragStartPort.BelongTool, Canonical(_dragStartPort), targetTB, newPort))
                            {
                                AddConnection(_dragStartPort, newPort);
                            }
                        }
                    }
                }
                _dragStartPort = null;
                if (connTreeView != null)
                {
                    connTreeView.IsDragging = false;
                    connTreeView.DragStartPort = null;
                    connTreeView.HoverPort = null;
                    connTreeView.CanConnectHover = false;
                    connTreeView.IsHoverOutputRoot = false;
                }
                this.treeView1.Cursor = Cursors.Default;
                if (connTreeView != null)
                    connTreeView.InvalidateConnections();
                else
                    this.treeView1.Invalidate();
            }
        }

        /// <summary>
        /// 连线完成事件处理，先断开目标端口的旧连接，再创建新的工具连接和连线
        /// </summary>
        private void OnConnectionCompleted(object sender, ConnectionEventArgs e)
        {
            PortNode fromPort = e.FromPort;
            PortNode toPort = e.ToPort;

            if (fromPort == null || toPort == null)
                return;

            ToolBase fromTool = fromPort.BelongTool;
            ToolBase toTool = toPort.BelongTool;

            // 连接层(ToolBlock.connections)始终用canonical端口；fromPort/toPort可能是克隆端口，需映射
            ToolBlock.DisconnectPortByTarget(toTool, Canonical(toPort));

            if (ToolBlock.ConnectPort(fromTool, Canonical(fromPort), toTool, Canonical(toPort)))
            {
                // 连线绘制用编辑器树中的显示节点(克隆或canonical本身)
                RemoveConnectionsTo(toPort);
                AddConnection(fromPort, toPort);
            }
        }

        /// <summary>
        /// 鼠标按下事件，启动拖拽连线或清除选中状态
        /// 左键点击输出端口时进入拖拽模式；点击空白区域时清除选中
        /// </summary>
        private void TreeView1_MouseDown(object sender, MouseEventArgs e)
        {
            base.OnMouseDown(e);

            TreeNode node = this.treeView1.GetNodeAt(e.Location);

            if (node == null)
            {
                var connTreeView = this.treeView1 as ConnectionTreeView;
                if (connTreeView != null && connTreeView.SelectedNode != null)
                {
                    connTreeView.SelectedNode = null;
                    connTreeView.InvalidateConnections();
                }
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                // 仅源端口(输出端)可发起拖拽连线。用IsSource判定而非直接判断direction=="Output"：
                // 嵌套ToolBlock的输出端口direction=="Input"(外部角色为源)，也允许发起拖拽；
                // 嵌套ToolBlock的输入端口direction=="Output"(外部角色为汇)，不允许发起拖拽。
                if (node is PortNode portNode && IsSource(portNode))
                {
                    _isDragging = true;
                    _dragStartPort = portNode;
                    this.Capture = true;
                    var connTreeView = this.treeView1 as ConnectionTreeView;
                    if (connTreeView != null)
                    {
                        connTreeView.IsDragging = true;
                        connTreeView.DragStartPort = portNode;
                        connTreeView.CurrentMousePoint = e.Location;
                        connTreeView.HoverPort = null;
                        connTreeView.CanConnectHover = false;
                        connTreeView.IsHoverOutputRoot = false;
                    }
                }
            }
            else
            {
                if (node.Parent.Text == "工具")
                {
                    selectnode = node;
                    MenuStrip.Show(this, e.Location);
                }
            }

        }

        /// <summary>
        /// 触发控件重绘
        /// </summary>
        protected void Paint()
        {
            this.treeView1.Invalidate();
        }

        /// <summary>
        /// 获取端口的屏幕坐标位置，基于可见祖先节点的Bounds计算
        /// 端口位置在节点文本右侧8像素处，垂直居中
        /// </summary>
        /// <param name="portNode">端口节点</param>
        /// <returns>端口坐标点</returns>
        public Point GetPortPosition(PortNode portNode)
        {
            TreeNode visibleNode = GetVisibleAncestor(portNode);
            Rectangle bounds = visibleNode.Bounds;
            Size textSize = TextRenderer.MeasureText(visibleNode.Text, this.Font);
            int textRight = bounds.Left + textSize.Width + 2;
            return new Point(textRight + 8, bounds.Top + bounds.Height / 2);
        }

        /// <summary>
        /// 获取节点的可见祖先，当节点的父节点处于折叠状态时向上遍历
        /// </summary>
        /// <param name="node">目标节点</param>
        /// <returns>第一个可见的祖先节点</returns>
        public TreeNode GetVisibleAncestor(TreeNode node)
        {
            TreeNode current = node;
            while (current != null && current.Parent != null && !current.Parent.IsExpanded)
            {
                current = current.Parent;
            }
            return current;
        }

        /// <summary>
        /// 判断节点是否为"输出"根节点（顶层文本为"输出"的节点）
        /// </summary>
        /// <param name="node">待判断的节点</param>
        /// <returns>是否为输出根节点</returns>
        public bool IsOutputRootNode(TreeNode node)
        {
            if (node == null)
                return false;
            return node.Text == "输出" && node.Parent == null;
        }

        /// <summary>
        /// 判断节点是否为ToolBlock类型工具的根节点（在"工具"根节点下，且对应工具是ToolBlock）
        /// </summary>
        public bool IsToolBlockToolRootNode(TreeNode node)
        {
            if (node == null || node is PortNode)
                return false;
            if (node.Parent == null || node.Parent.Text != "工具")
                return false;
            foreach (var tool in ToolBlock.Tools.Values)
            {
                if (tool.RootNode == node && tool is HToolBase.Tools.ToolBlock)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 根据根节点查找对应的ToolBlock工具
        /// </summary>
        public HToolBase.Tools.ToolBlock GetToolBlockByRootNode(TreeNode node)
        {
            if (node == null)
                return null;
            foreach (var tool in ToolBlock.Tools.Values)
            {
                if (tool.RootNode == node && tool is HToolBase.Tools.ToolBlock tb)
                    return tb;
            }
            return null;
        }

        /// <summary>
        /// 绘制单条连接线，使用四点折线路由：从端口→水平拐点→垂直拐点→目标端口
        /// 支持障碍物规避（自动计算安全路径）和箭头绘制
        /// </summary>
        /// <param name="g">图形对象</param>
        /// <param name="from">起点坐标</param>
        /// <param name="to">终点坐标</param>
        /// <param name="pen">连线画笔</param>
        /// <param name="xOffset">X方向偏移量（用于多连线分组）</param>
        /// <param name="obstacles">障碍物矩形列表</param>
        public void DrawConnectionLine(Graphics g, Point from, Point to, Pen pen, int xOffset, List<Rectangle> obstacles)
        {
            int baseMidX = Math.Max(from.X, to.X) + 30;
            baseMidX = FindSafeMidX(baseMidX, from, to, obstacles);

            int midX = baseMidX + 0;

            Point[] points = new Point[]
            {
                from,
                new Point(midX, from.Y),
                new Point(midX, to.Y),
                to
            };
            g.DrawLines(pen, points);

            Point endPoint = points[points.Length - 1];
            Point prevPoint = points[points.Length - 2];
            DrawArrowHead(g, pen, prevPoint, endPoint);
        }

        /// <summary>
        /// 绘制箭头头部，根据线段方向计算三角形箭头的两个侧边点
        /// </summary>
        /// <param name="g">图形对象</param>
        /// <param name="pen">画笔（使用其Brush填充箭头）</param>
        /// <param name="from">箭头起点（线段终点的前一点）</param>
        /// <param name="to">箭头终点（线段终点）</param>
        private void DrawArrowHead(Graphics g, Pen pen, Point from, Point to)
        {
            float angle = (float)Math.Atan2(to.Y - from.Y, to.X - from.X);
            float arrowSize = 8;

            Point p1 = new Point(
                (int)(to.X - arrowSize * Math.Cos(angle - Math.PI / 6)),
                (int)(to.Y - arrowSize * Math.Sin(angle - Math.PI / 6)));
            Point p2 = new Point(
                (int)(to.X - arrowSize * Math.Cos(angle + Math.PI / 6)),
                (int)(to.Y - arrowSize * Math.Sin(angle + Math.PI / 6)));

            Point[] arrowPoints = new Point[] { to, p1, p2 };
            g.FillPolygon(pen.Brush, arrowPoints);
        }

        /// <summary>
        /// 获取所有节点的障碍物矩形列表，用于连线规避
        /// 仅收集展开状态下的子节点，排除指定节点及其可见祖先
        /// </summary>
        /// <param name="excludeNodes">需要排除的节点数组</param>
        /// <returns>障碍物矩形列表</returns>
        public List<Rectangle> GetNodeObstacles(params TreeNode[] excludeNodes)
        {
            var obstacles = new List<Rectangle>();
            var excludeSet = new HashSet<TreeNode>(excludeNodes ?? new TreeNode[0]);

            if (excludeNodes != null)
            {
                foreach (TreeNode node in excludeNodes)
                {
                    TreeNode visible = GetVisibleAncestor(node);
                    if (visible != null)
                        excludeSet.Add(visible);
                }
            }

            foreach (TreeNode node in treeView1.Nodes)
            {
                CollectNodeBounds(node, obstacles, excludeSet.ToArray());
            }
            return obstacles;
        }

        /// <summary>
        /// 递归收集节点边界矩形作为障碍物，仅遍历展开节点的子节点
        /// </summary>
        /// <param name="node">当前节点</param>
        /// <param name="obstacles">障碍物列表（输出）</param>
        /// <param name="excludeNodes">需要排除的节点数组</param>
        private void CollectNodeBounds(TreeNode node, List<Rectangle> obstacles, TreeNode[] excludeNodes)
        {
            if (excludeNodes == null || !excludeNodes.Contains(node))
            {
                Rectangle bounds = node.Bounds;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    obstacles.Add(new Rectangle(
                        bounds.Left - 4,
                        bounds.Top - 2,
                        bounds.Width + 8,
                        bounds.Height + 4));
                }
            }

            if (node.IsExpanded)
            {
                foreach (TreeNode child in node.Nodes)
                {
                    CollectNodeBounds(child, obstacles, excludeNodes);
                }
            }
        }

        /// <summary>
        /// 寻找安全的中间X坐标，使连线绕开所有障碍物
        /// 迭代检测障碍物碰撞，逐步向右偏移直到找到安全路径
        /// </summary>
        /// <param name="desiredMidX">期望的中间X坐标</param>
        /// <param name="from">起点</param>
        /// <param name="to">终点</param>
        /// <param name="obstacles">障碍物列表</param>
        /// <returns>安全的中间X坐标</returns>
        private int FindSafeMidX(int desiredMidX, Point from, Point to, List<Rectangle> obstacles)
        {
            int midX = desiredMidX;
            int segTop = Math.Min(from.Y, to.Y);
            int segBottom = Math.Max(from.Y, to.Y);
            int iterations = 0;

            while (iterations++ < 30)
            {
                bool safe = true;

                foreach (var obstacle in obstacles)
                {
                    if (midX >= obstacle.Left && midX <= obstacle.Right)
                    {
                        if (segTop < obstacle.Bottom && segBottom > obstacle.Top)
                        {
                            midX = obstacle.Right + 10;
                            safe = false;
                            break;
                        }
                    }
                }

                if (!safe)
                    continue;

                if (SegmentIntersectsRect(from.X, from.Y, midX, from.Y, obstacles))
                {
                    midX += 15;
                    safe = false;
                    continue;
                }

                if (SegmentIntersectsRect(midX, to.Y, to.X, to.Y, obstacles))
                {
                    midX += 15;
                    safe = false;
                    continue;
                }

                break;
            }

            return midX;
        }

        /// <summary>
        /// 判断水平或垂直线段是否与任何障碍物矩形相交
        /// </summary>
        /// <param name="x1">线段起点X坐标</param>
        /// <param name="y1">线段起点Y坐标</param>
        /// <param name="x2">线段终点X坐标</param>
        /// <param name="y2">线段终点Y坐标</param>
        /// <param name="obstacles">障碍物列表</param>
        /// <returns>是否相交</returns>
        private bool SegmentIntersectsRect(int x1, int y1, int x2, int y2, List<Rectangle> obstacles)
        {
            foreach (var rect in obstacles)
            {
                if (y1 == y2)
                {
                    if (y1 >= rect.Top && y1 <= rect.Bottom)
                    {
                        int left = Math.Min(x1, x2);
                        int right = Math.Max(x1, x2);
                        if (left < rect.Right && right > rect.Left)
                            return true;
                    }
                }
                else if (x1 == x2)
                {
                    if (x1 >= rect.Left && x1 <= rect.Right)
                    {
                        int top = Math.Min(y1, y2);
                        int bottom = Math.Max(y1, y2);
                        if (top < rect.Bottom && bottom > rect.Top)
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 判断两个端口是否可以连接。
        /// 连接条件：from为源(输出端)、to为汇(输入端)、类型匹配、自上而下、非重复连接。
        /// Direction校验分两种情况(由IsSource/IsSink按端口角色判定)：
        ///  - 普通连线(含当前编辑器自身端口的内部连线)：from.Direction=="Output" 且 to.Direction=="Input"（方向相反）。
        ///  - 与嵌套ToolBlock端口的连线(外部父级树视图)：ToolBlock自身端口方向与语义相反
        ///    (输入端口direction="Output"、输出端口direction="Input")，故外部连线要求 from 与 to 的 Direction 相同。
        /// </summary>
        /// <param name="fromPort">源端口（输出）</param>
        /// <param name="toPort">目标端口（输入）</param>
        /// <returns>是否可连接</returns>
        public bool CanConnect(PortNode fromPort, PortNode toPort)
        {
            if (fromPort == null || toPort == null)
                return false;
            // from必须是源、to必须是汇；IsSource/IsSink按端口外部/内部角色判定Direction，
            // 自动同时满足"嵌套ToolBlock外部连线同方向"与"源→汇"语义。
            if (!IsSource(fromPort) || !IsSink(toPort))
                return false;
            if (fromPort.PortType != toPort.PortType)
                return false;
            if (!IsTopToBottom(fromPort, toPort))
                return false;
            foreach (ConnectionLine conn in _connections)
            {
                if (conn.FromPort == fromPort && conn.ToPort == toPort)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 判断端口是否为嵌套ToolBlock的自身端口(以canonical形式显示在当前编辑器中，处于"外部角色")。
        /// 当前编辑器自身ToolBlock的端口以克隆形式显示(在_displayToCanonical中)，属于"内部角色"，不算嵌套。
        /// </summary>
        private bool IsNestedToolBlockPort(PortNode port)
        {
            if (port == null) return false;
            if (!(port.BelongTool is HToolBase.Tools.ToolBlock)) return false;
            // 克隆端口(当前编辑器自身ToolBlock端口)属于内部角色，不算嵌套外部
            return !_displayToCanonical.ContainsKey(port);
        }

        /// <summary>
        /// 判断端口是否可作为连线源(输出端)。
        ///  - 普通工具：direction=="Output"为源。
        ///  - 当前编辑器自身ToolBlock端口(内部角色)：输入端口direction=="Output"为源(向内部工具供值)。
        ///  - 嵌套ToolBlock端口(外部角色)：输出端口direction=="Input"为源(向外部兄弟工具供值)。
        /// </summary>
        private bool IsSource(PortNode port)
        {
            if (port == null) return false;
            if (port.BelongTool is HToolBase.Tools.ToolBlock)
            {
                if (IsNestedToolBlockPort(port))
                    return port.Direction == "Input";   // 外部角色：ToolBlock输出端口是源
                return port.Direction == "Output";       // 内部角色：ToolBlock输入端口是源
            }
            return port.Direction == "Output";           // 普通工具输出端口是源
        }

        /// <summary>
        /// 判断端口是否可作为连线汇(输入端)。规则与IsSource对称。
        ///  - 普通工具：direction=="Input"为汇。
        ///  - 当前编辑器自身ToolBlock端口(内部角色)：输出端口direction=="Input"为汇(接收内部工具输出)。
        ///  - 嵌套ToolBlock端口(外部角色)：输入端口direction=="Output"为汇(接收外部兄弟工具输出)。
        /// </summary>
        private bool IsSink(PortNode port)
        {
            if (port == null) return false;
            if (port.BelongTool is HToolBase.Tools.ToolBlock)
            {
                if (IsNestedToolBlockPort(port))
                    return port.Direction == "Output";   // 外部角色：ToolBlock输入端口是汇
                return port.Direction == "Input";        // 内部角色：ToolBlock输出端口是汇
            }
            return port.Direction == "Input";            // 普通工具输入端口是汇
        }

        /// <summary>
        /// 删除所有指向指定端口的连线（用于连线替换场景）
        /// </summary>
        /// <param name="toPort">目标端口</param>
        public void RemoveConnectionsTo(PortNode toPort)
        {
            var toRemove = _connections.FindAll(c => c.ToPort == toPort);
            foreach (var conn in toRemove)
            {
                _connections.Remove(conn);
            }
            var connTreeView = this.treeView1 as ConnectionTreeView;
            if (connTreeView != null)
            {
                connTreeView.Connections = _connections;
                connTreeView.InvalidateConnections();
            }
        }

        /// <summary>
        /// 判断源端口是否在目标端口上方（自上而下连接）
        /// </summary>
        /// <param name="fromPort">源端口</param>
        /// <param name="toPort">目标端口</param>
        /// <returns>是否自上而下</returns>
        private bool IsTopToBottom(PortNode fromPort, PortNode toPort)
        {
            Rectangle fromBounds = fromPort.Bounds;
            Rectangle toBounds = toPort.Bounds;
            return fromBounds.Top < toBounds.Top;
        }

        /// <summary>
        /// 刷新所有连线，更新ConnectionTreeView的连线数据并重绘
        /// </summary>
        public void RefreshConnections()
        {
            var connTreeView = this.treeView1 as ConnectionTreeView;
            if (connTreeView != null)
            {
                connTreeView.Connections = _connections;
                connTreeView.InvalidateConnections();
            }
            else
            {
                this.Invalidate();
            }
        }

        /// <summary>
        /// 清除所有连线
        /// </summary>
        public void ClearConnections()
        {
            _connections.Clear();
            var connTreeView = this.treeView1 as ConnectionTreeView;
            if (connTreeView != null)
            {
                connTreeView.Connections = _connections;
                connTreeView.InvalidateConnections();
            }
            else
            {
                this.Invalidate();
            }
        }

        /// <summary>
        /// 删除指定的连线
        /// </summary>
        /// <param name="fromPort">源端口</param>
        /// <param name="toPort">目标端口</param>
        public void RemoveConnection(PortNode fromPort, PortNode toPort)
        {
            ConnectionLine conn = _connections.Find(c => c.FromPort == fromPort && c.ToPort == toPort);
            if (conn != null)
            {
                _connections.Remove(conn);
                var connTreeView = this.treeView1 as ConnectionTreeView;
                if (connTreeView != null)
                {
                    connTreeView.Connections = _connections;
                    connTreeView.InvalidateConnections();
                }
                else
                {
                    this.Invalidate();
                }
            }
        }

        /// <summary>
        /// 获取当前所有连线的副本
        /// </summary>
        /// <returns>连线列表</returns>
        public List<ConnectionLine> GetConnections()
        {
            return _connections;
        }

        /// <summary>
        /// 添加新连线
        /// </summary>
        /// <param name="fromPort">源端口</param>
        /// <param name="toPort">目标端口</param>
        public void AddConnection(PortNode fromPort, PortNode toPort)
        {
            ConnectionLine line = new ConnectionLine
            {
                FromPort = fromPort,
                ToPort = toPort
            };
            _connections.Add(line);
            var connTreeView = this.treeView1 as ConnectionTreeView;
            if (connTreeView != null)
            {
                connTreeView.Connections = _connections;
                connTreeView.InvalidateConnections();
            }
            else
            {
                this.Invalidate();
            }
        }
        #endregion

        /// <summary>
        /// 鼠标双击事件处理（调试用），添加调试端口
        /// </summary>
        private void TreeView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            imageSourceTool.AddInput("Debug", TypeName.SINGAL);
            imageSourceTool.AddOutput("Debug", TypeName.SINGAL);
            ToolBlock.AddInput("Debug", TypeName.SINGAL);
            this.Refresh();
        }

        /// <summary>
        /// 将工具的根节点添加到"工具"节点下
        /// </summary>
        /// <param name="tool">工具实例</param>
        /// <returns>是否添加成功</returns>
        public bool AddNodes(ToolBase tool)
        {
            try {
                if (tool?.RootNode == null) return false;
                string NodesName = "工具";
                TreeNode rootNode = treeView1.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == NodesName);
                if (rootNode == null) return false;
                // 同一工具实例跨编辑器窗口复用，RootNode若仍挂在当前树（如重复添加）需先摘除；
                // 跨窗口的残留由窗口关闭时DetachToolNodes在树存活状态下清理（Dispose后Remove无法
                // 清除节点内部残留的原生句柄与treeView引用，Add会抛"不能在多处添加或插入项"）
                tool.RootNode.Remove();
                rootNode.Nodes.Add(tool.RootNode);
                rootNode.ExpandAll();
                return true;
            }
            catch(Exception ex)
                {
                return false;
            }

        }

        /// <summary>
        /// 将所有内部工具的RootNode从本树视图摘除（供编辑器窗体关闭时调用）。
        /// 必须在TreeView句柄仍存活时调用：此时Remove才会同步销毁原生项并彻底清除节点内部的
        /// handle与treeView引用，工具节点之后才能被重新Add到新打开的编辑器树中；
        /// 若等窗体Dispose后节点仍挂着，该残留状态无法通过Remove清除。
        /// </summary>
        public void DetachToolNodes()
        {
            try {
                TreeNode rootNode = treeView1.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == "工具");
                if (rootNode == null) return;
                foreach (TreeNode node in rootNode.Nodes.Cast<TreeNode>().ToList())
                {
                    node.Remove();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("DetachToolNodes失败：" + ex.Message);
            }
        }

        /// <summary>
        /// AddInputEvent事件处理器：仅当本控件已挂载到可见的编辑器窗体（ToolBlockControl处于打开状态）时，
        /// 才为本ToolBlock端口创建克隆并同步到内部TreeView。编辑器未打开时（如外部父视图拖拽为嵌套
        /// ToolBlock添加端口）跳过——canonical端口保留在RootNode中，下次打开编辑器ReloadData会重建克隆。
        /// 注意：可见性用 FindForm().Visible 判断，而非 this.Visible，避免Tab页切换误判（见方法内注释）。
        /// </summary>
        private void OnAddInputEvent(PortNode portNode)
        {
            // 仅当本控件已挂载到可见的编辑器窗体（编辑器处于打开状态）时，才同步克隆端口到内部TreeView。
            // 注意：不能用 this.Visible 判断——toolTreeview位于"工具"Tab页(tabPage1)，
            // 而添加端口的"+"按钮位于"输入/输出"Tab页(tabPage2)。当用户在tabPage2点击"+"
            // 添加端口时，tabPage1非激活态会使 this.Visible 返回false，导致克隆节点无法添加，
            // 树视图不显示新增端口。改用 FindForm().Visible 判断宿主窗体可见性，与Tab激活状态无关。
            // 嵌套ToolBlock编辑器未打开时(其窗体隐藏)，FindForm().Visible为false，跳过同步——
            // 其端口canonical保留在RootNode中，下次打开编辑器ReloadData会重建克隆。
            Form form = this.FindForm();
            if (this.IsDisposed || form == null || !form.Visible) return;
            AddInput(portNode);
        }

        /// <summary>
        /// AddOutputEvent事件处理器：仅当本控件已挂载到可见编辑器窗体时才同步到内部TreeView。
        /// </summary>
        private void OnAddOutputEvent(PortNode portNode)
        {
            // 同OnAddInputEvent：用FindForm().Visible判断编辑器窗体是否可见，避免Tab页切换误判
            Form form = this.FindForm();
            if (this.IsDisposed || form == null || !form.Visible) return;
            AddOutput(portNode);
        }

        /// <summary>
        /// 为本ToolBlock的输入端口在编辑器"输入"根节点下创建独立的显示克隆节点。
        /// canonical端口保留在RootNode中（父级树视图），编辑器使用克隆节点显示，
        /// 两个树视图互不干扰。内部工具端口不走此方法。
        /// </summary>
        /// <param name="canonical">canonical输入端口</param>
        public void AddInput(PortNode canonical)
        {
            string NodesName = "输入";
            TreeNode rootNode = treeView1.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == NodesName);
            if (rootNode == null) return;
            PortNode clone = ClonePort(canonical);
            _canonicalToDisplay[canonical] = clone;
            _displayToCanonical[clone] = canonical;
            rootNode.Nodes.Add(clone);
            rootNode.ExpandAll();
        }
        /// <summary>
        /// 添加输出端口节点到"输出"根节点下（创建克隆，同AddInput）
        /// </summary>
        /// <param name="canonical">canonical输出端口</param>
        public void AddOutput(PortNode canonical)
        {
            try
            {
                string NodesName = "输出";
                TreeNode rootNode = treeView1.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == NodesName);
                if (rootNode == null) return;
                PortNode clone = ClonePort(canonical);
                _canonicalToDisplay[canonical] = clone;
                _displayToCanonical[clone] = canonical;
                rootNode.Nodes.Add(clone);
                rootNode.ExpandAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// 根据canonical端口创建独立的显示克隆端口（复制文本、类型、方向、归属工具、端口名、值）。
        /// 图标(ImageIndex/SelectedImageIndex)按direction独立设置，保持内部树视图原有约定，
        /// 不复制canonical的图标——外部父级树视图已对canonical图标做交换，直接复制会破坏内部显示。
        /// 克隆仅用于编辑器显示与连线绘制，不参与值存储与连接层（连接层始终用canonical）。
        /// </summary>
        private PortNode ClonePort(PortNode canonical)
        {
            if (canonical == null) return null;
            PortNode clone = new PortNode(
                text: canonical.Text,
                portType: canonical.PortType,
                direction: canonical.Direction,
                belongTool: canonical.BelongTool,
                portName: canonical.PortName,
                value: canonical.Value
            );
            // 内部编辑器树视图的克隆端口图标保持原有约定，不随外部父级树视图的图标交换而变化：
            //   ToolBlock输入端口(direction=="Output") → ImageIndex 1(Input.png)
            //   ToolBlock输出端口(direction=="Input")  → ImageIndex 2(Output.png)
            // 注意：不能复制canonical.ImageIndex——外部父级树视图已将其交换(输入2/输出1)，
            // 若直接复制会破坏内部树视图的图标显示。
            if (canonical.Direction == "Output")
            {
                clone.ImageIndex = 1;
                clone.SelectedImageIndex = 1;
            }
            else
            {
                clone.ImageIndex = 2;
                clone.SelectedImageIndex = 2;
            }
            return clone;
        }

        /// <summary>
        /// 输入端口子节点重命名
        /// </summary>
        /// <param name="oldName">输入端口原节点名</param>
        /// <param name="newName">输入端口新节点名</param>
        public void RenameInputPort(string oldName, string newName)
        {
            string NodesName = "输入";
            TreeNode rootNode = treeView1.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == NodesName);
            var clone = rootNode?.Nodes.Cast<PortNode>().FirstOrDefault(n => n.Text == oldName);
            if (clone != null)
            {
                clone.Text = newName;
                clone.PortName = newName; // 保持克隆端口名与文本一致
            }
            // 同步canonical端口的Text，使父级树视图（RootNode下）也显示新名称
            if (ToolBlock.Inputs.TryGetValue(newName, out var canonical))
                canonical.Text = newName;
        }

        /// <summary>
        /// 输出端口子节点重命名
        /// </summary>
        /// <param name="oldName">输出端口原节点名</param>
        /// <param name="newName">输出端口新节点名</param>
        public void RenameOutputPort(string oldName, string newName)
        {
            string NodesName = "输出";
            TreeNode rootNode = treeView1.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == NodesName);
            var clone = rootNode?.Nodes.Cast<PortNode>().FirstOrDefault(n => n.Text == oldName);
            if (clone != null)
            {
                clone.Text = newName;
                clone.PortName = newName;
            }
            if (ToolBlock.Outputs.TryGetValue(newName, out var canonical))
                canonical.Text = newName;
        }
        /// <summary>
        /// 初始化图标列表，从ICons目录加载图标并分配给各根节点
        /// </summary>
        private void SetImageList()
        {
            string exePath = Application.StartupPath;
            imageList.Images.Clear();
            imageList.ImageSize = new Size(16, 16);
            foreach (string fileName in imageNames)
            {
                string fullPath = Path.Combine(exePath, "ICons", fileName);
                if (!File.Exists(fullPath))
                    continue;
                string ext = Path.GetExtension(fileName).ToLower();
                if (ext == ".ico")
                {
                    Bitmap bmp;
                    using (Icon icon = new Icon(fullPath))
                    {
                        bmp = icon.ToBitmap();
                    }
                    imageList.Images.Add(bmp);
                }
                else if (ext == ".png")
                {
                    Bitmap bmp = new Bitmap(fullPath);
                    imageList.Images.Add(bmp);
                }
            }
            treeView1.ImageList = imageList;
            string InputNodeName = "输入";
            TreeNode InputrootNode = treeView1.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == InputNodeName);
            InputrootNode.ImageIndex = 2;
            InputrootNode.SelectedImageIndex = 2;

            string OutputNodesName = "输出";
            TreeNode OutputrootNode = treeView1.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == OutputNodesName);
            OutputrootNode.ImageIndex = 1;
            OutputrootNode.SelectedImageIndex = 1;

            string ToolNodesName = "工具";
            TreeNode ToolrootNode = treeView1.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == ToolNodesName);
            ToolrootNode.ImageIndex = 3;
            ToolrootNode.SelectedImageIndex = 3;
        }


        private void FirstLoadTools()
        {
            foreach (HToolBase.ToolBase tool in this.ToolBlock.Tools.Values)
            {
                AddNodes(tool);
                Console.WriteLine(tool.ToolName);
            }
        }

        /// <summary>
        /// 加载ToolBlock自身的输入/输出端口到TreeView
        /// </summary>
        private void FirstLoadPorts()
        {
            foreach (PortNode port in ToolBlock.Inputs.Values)
            {
                AddInput(port);
            }
            foreach (PortNode port in ToolBlock.Outputs.Values)
            {
                AddOutput(port);
            }
        }

        /// <summary>
        /// 从ToolBlock恢复所有连线到TreeView（批量添加后只刷新一次，避免多次Invalidate导致卡顿）
        /// 连线绘制依赖端口在本树视图中的Bounds，故需将canonical端口映射为编辑器显示克隆端口；
        /// 内部工具端口未克隆，ToDisplay直接返回其自身。
        /// </summary>
        private void RestoreConnections()
        {
            var allConns = ToolBlock.GetAllConnections();
            foreach (var conn in allConns)
            {
                PortNode FromPort = ToDisplay(conn.FromPort);
                PortNode ToPort = ToDisplay(conn.ToPort);

                if (FromPort != null && ToPort != null)
                {
                    ConnectionLine line = new ConnectionLine();
                    line.FromPort = FromPort;
                    line.ToPort = ToPort;
                    _connections.Add(line);

                }
            }
            // 全部添加完成后统一刷新连线（调用一次即可）
            RefreshConnections();
        }

        /// <summary>
        /// 将canonical端口映射为编辑器显示克隆端口（仅本ToolBlock自身端口有克隆）。
        /// 内部工具端口或未注册的端口原样返回（它们本身就在编辑器树中）。
        /// </summary>
        private PortNode ToDisplay(PortNode canonical)
        {
            if (canonical != null && _canonicalToDisplay.TryGetValue(canonical, out var clone))
                return clone;//clone
            return canonical;
        }

        /// <summary>
        /// 将编辑器显示克隆端口映射回canonical端口（用于与ToolBlock连接层交互）。
        /// 内部工具端口未克隆，Canonical直接返回其自身。
        /// </summary>
        private PortNode Canonical(PortNode display)
        {
            if (display != null && _displayToCanonical.TryGetValue(display, out var canonical))
                return canonical;
            return display;
        }
    }
}
