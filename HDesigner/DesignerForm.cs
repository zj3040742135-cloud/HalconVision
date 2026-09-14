using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HToolBase;

namespace HDesigner
{
    public partial class DesignerForm : Form
    {
        /// <summary>缩放方向标志：N/S/W/E为单边，四角为两方向组合</summary>
        [Flags]
        private enum ResizeDir
        {
            None = 0,
            N = 1, S = 2, W = 4, E = 8,
            NW = N | W, NE = N | E, SW = S | W, SE = S | E
        }

        private const int HandleSize = 8;      // 缩放控制点方块边长（像素）
        private const int HandleTolerance = 3; // 控制点命中判定外扩像素，便于抓取
        private const int MinCtrlSize = 4;     // 控件最小宽高
        // 控制点中心相对控件边界的外移距离：方块完全位于控件边界外侧。
        // 不能骑跨边缘绘制——画在panel上的部分会被子控件窗口（独立HWND）裁掉一半
        private const int HandleOffset = HandleSize / 2;

        // 当前选中的控件集合（支持多选，可跨容器嵌套）
        private readonly List<Control> _selectedControls = new List<Control>();

        // 设计器创建的全部控件标记集：区分"设计控件"与容器（UserControl等）的内部子控件，
        // 也是嵌套拖放/选中解析的依据
        private readonly HashSet<Control> _designed = new HashSet<Control>();

        // 事件绑定表：控件 → 事件名(MouseClick/DoubleClick) → 绑定配置
        private readonly Dictionary<Control, Dictionary<string, EventBinding>> _eventBindings =
            new Dictionary<Control, Dictionary<string, EventBinding>>();

        // 组拖动状态
        private bool _isMove = false;
        private Point _moveStartMouse;                          // 按下时鼠标屏幕坐标
        private Dictionary<Control, Point> _moveStartLocations; // 各选中控件按下时位置

        // 缩放状态
        private bool _isResize = false;
        private ResizeDir _resizeDir = ResizeDir.None;
        private Control _resizeControl;
        private Rectangle _resizeStartBounds;
        private Point _resizeStartMouse;

        // 框选状态
        private bool _isMarquee = false;
        private Point _marqueeStart;
        private Rectangle _marquee; // panel1坐标系

        // 右键层级菜单
        private ContextMenuStrip _zOrderMenu;

        Type _type;

        public DesignerForm()
        {
            InitializeComponent();

            TreeNode node = new TreeNode();
            node.Text = "Button";
            node.Tag = typeof(System.Windows.Forms.Button);
            treeView1.Nodes.Add(node);

            node = new TreeNode();
            node.Text = "Panel";
            node.Tag = typeof(System.Windows.Forms.Panel);
            treeView1.Nodes.Add(node);

            node = new TreeNode();
            node.Text = "SplitContainer";
            node.Tag = typeof(System.Windows.Forms.SplitContainer);
            treeView1.Nodes.Add(node);

            // HDisplayControl自定义控件（来自HToolBase）
            node = new TreeNode();
            node.Text = "HDisplayControl";
            node.Tag = typeof(HToolBase.Controls.HDisplayControl);
            treeView1.Nodes.Add(node);

            // 右键菜单：控件层级（BringToFront/SendToBack）
            _zOrderMenu = new ContextMenuStrip();

            // 拖拽事件：panel1与所有设计控件（含嵌套容器）统一注册为拖放目标
            treeView1.ItemDrag += TreeView1_ItemDrag;
            panel1.DragEnter += Panel1_DragEnter;
            panel1.DragDrop += DesignedControl_DragDrop;

            // panel鼠标事件
            panel1.MouseDown += Panel1_MouseDown;
            panel1.MouseMove += Panel1_MouseMove;
            panel1.MouseUp += Panel1_MouseUp;
            // 选择框/控制点绘制在panel1上（控制点绘制于控件边界外侧，不被子控件裁剪）
            panel1.Paint += panel1_Paint;

            // 界面设计持久化到当前产品目录：打开时自动加载，关闭时自动保存
            Load += DesignerForm_Load;
        }

        /// <summary>设计文件路径：{当前产品目录}/DesignerLayout.xml</summary>
        private static string DesignFilePath
        {
            get { return Path.Combine(ProductManager.CurrentProductPath, "DesignerLayout.xml"); }
        }

        /// <summary>打开时自动加载当前产品目录下的界面设计</summary>
        private void DesignerForm_Load(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(DesignFilePath)) return;
                DesignDocument doc = DesignDocument.Load(DesignFilePath);
                foreach (ControlData data in doc.Controls)
                    DesignDocument.Populate(data, panel1, OnDesignControlCreated);
            }
            catch (Exception ex)
            {
                MessageBox.Show("自动加载界面设计失败：" + ex.Message, "界面设计",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>关闭时自动保存界面设计到当前产品目录</summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            try
            {
                if (!Directory.Exists(ProductManager.CurrentProductPath))
                    Directory.CreateDirectory(ProductManager.CurrentProductPath);
                DesignDocument doc = DesignDocument.Capture(panel1, _designed, _eventBindings);
                DesignDocument.Save(doc, DesignFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("自动保存界面设计失败：" + ex.Message, "界面设计",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TreeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            TreeNode item = (TreeNode)e.Item;
            _type = item.Tag as Type;
            if (_type == null) return;
            // 将Type对象放入拖拽数据包
            treeView1.DoDragDrop(_type, DragDropEffects.Copy);
        }

        private void Panel1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        // 悬停在设计控件（含嵌套容器）上时允许放置
        private void DesignedControl_DragEnter(object sender, DragEventArgs e)
        {
            if (_type != null) e.Effect = DragDropEffects.Copy;
        }

        /// <summary>拖放落下：把落点解析到最内层的容器（panel1或嵌套的Panel/SplitContainer）
        /// 并在其中创建控件。WinForms的OLE拖放按HWND投递，只有AllowDrop=true的窗口能收到
        /// 拖放事件，故所有设计控件均注册为拖放目标，实际容器由GetDropContainer统一解析</summary>
        private void DesignedControl_DragDrop(object sender, DragEventArgs e)
        {
            if (_type == null) return;
            Point screenPt = new Point(e.X, e.Y);
            Control container = GetDropContainer((Control)sender, screenPt);
            Control newCtrl = CreateDesignedControl(_type, container.PointToClient(screenPt), container);
            // 新建控件自动选中，属性面板刷新
            SetSelection(newCtrl);
        }

        /// <summary>在parent容器中创建设计控件并初始化</summary>
        private Control CreateDesignedControl(Type type, Point clientPos, Control parent)
        {
            Control newCtrl = (Control)Activator.CreateInstance(type);
            newCtrl.BackColor = Color.White;
            newCtrl.Location = clientPos;
            newCtrl.Size = new Size(100, 30);
            // 显示控件需要较大默认尺寸，太小无法正常展示
            if (newCtrl is HToolBase.Controls.HDisplayControl) newCtrl.Size = new Size(320, 240);

            // 绑定控件鼠标事件（含子控件递归绑定：UserControl/SplitContainer的内部
            // 子控件会拦截鼠标事件，仅绑定容器本身时点击内部区域无法拖动）
            BindDesignerEvents(newCtrl);

            // 自动命名，便于层级菜单显示与设计序列化
            newCtrl.Name = type.Name + (++_ctrlSeq);

            parent.Controls.Add(newCtrl);
            newCtrl.BringToFront();
            _designed.Add(newCtrl);
            return newCtrl;
        }

        /// <summary>解析拖放落点所属容器：从落点窗口向下进入包含落点的最内层设计容器
        /// （按z序从前到后匹配），起点非容器（如Button）时向上退到最近的设计容器；
        /// SplitContainer不是有效直接父容器，按落点位置映射到其Panel1/Panel2</summary>
        private Control GetDropContainer(Control start, Point screenPt)
        {
            Control c = start;
            while (true)
            {
                Control next = null;
                foreach (Control child in c.Controls.Cast<Control>().OrderBy(ch => c.Controls.GetChildIndex(ch)))
                {
                    if (!_designed.Contains(child)) continue;
                    if (!(child is Panel || child is SplitContainer)) continue;
                    if (child.Bounds.Contains(child.Parent.PointToClient(screenPt))) { next = child; break; }
                }
                if (next == null) break;
                c = next;
            }
            while (c != panel1 && !(c is Panel || c is SplitContainer))
                c = c.Parent ?? panel1;

            if (c is SplitContainer)
            {
                SplitContainer sc = (SplitContainer)c;
                Point p = sc.PointToClient(screenPt);
                bool first = sc.Orientation == Orientation.Vertical
                    ? p.X < sc.SplitterDistance
                    : p.Y < sc.SplitterDistance;
                c = first ? (Control)sc.Panel1 : sc.Panel2;
            }
            return c;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Delete && _selectedControls.Count > 0)
            {
                // ToList避免枚举中修改集合
                foreach (Control c in _selectedControls.ToList())
                {
                    StripDesigned(c);       // 清理标记集（容器删除时其内部设计控件一并移除）
                    (c.Parent ?? panel1).Controls.Remove(c);
                    c.Dispose();
                }
                _selectedControls.Clear();
                SyncSelectionUI();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        #region 选择管理

        /// <summary>单选：清空并设置唯一选中控件</summary>
        private void SetSelection(Control ctrl)
        {
            _selectedControls.Clear();
            if (ctrl != null) _selectedControls.Add(ctrl);
            SyncSelectionUI();
        }

        /// <summary>Ctrl+点击：切换控件的选中状态（多选）</summary>
        private void ToggleSelection(Control ctrl)
        {
            if (_selectedControls.Contains(ctrl)) _selectedControls.Remove(ctrl);
            else _selectedControls.Add(ctrl);
            SyncSelectionUI();
        }

        /// <summary>同步属性面板与绘制层</summary>
        private void SyncSelectionUI()
        {
            // 多选时PropertyGrid无法显示混合属性，置空
            propertyGrid1.SelectedObject = _selectedControls.Count == 1 ? _selectedControls[0] : null;
            RefreshOverlay();
        }

        private void RefreshOverlay()
        {
            panel1.Invalidate();
            // 嵌套控件的选区绘制在其父容器表面，需一并刷新
            foreach (Control c in _selectedControls)
            {
                if (c.Parent != null && c.Parent != panel1) c.Parent.Invalidate();
            }
        }

        #endregion

        #region 几何与Dock方向识别

        /// <summary>根据Dock布局自动识别可调整的缩放方向：
        /// None→四边全可调；Left→仅右缘E（调宽度）；Right→仅左缘W；Top→仅下缘S（调高度）；
        /// Bottom→仅上缘N；Fill→完全由布局管理，不可缩放</summary>
        private static ResizeDir GetResizableDirs(Control ctrl)
        {
            switch (ctrl.Dock)
            {
                case DockStyle.None: return ResizeDir.N | ResizeDir.S | ResizeDir.W | ResizeDir.E;
                case DockStyle.Left: return ResizeDir.E;
                case DockStyle.Right: return ResizeDir.W;
                case DockStyle.Top: return ResizeDir.S;
                case DockStyle.Bottom: return ResizeDir.N;
                default: return ResizeDir.None;
            }
        }

        /// <summary>枚举控件的缩放控制点（父容器客户区坐标），方向由Dock布局自动增减。
        /// 控制点位于控件边界外侧（HandleOffset外移），完整显示在父容器上不被子控件裁剪。
        /// 嵌套控件的选区由其父容器绘制（panel1画布会被中间容器窗口裁剪）</summary>
        private IEnumerable<KeyValuePair<Rectangle, ResizeDir>> GetHandles(Control ctrl)
        {
            ResizeDir dirs = GetResizableDirs(ctrl);
            if (dirs == ResizeDir.None || ctrl.Parent == null) yield break;
            Rectangle b = ctrl.Bounds;
            ResizeDir[] all = { ResizeDir.NW, ResizeDir.N, ResizeDir.NE, ResizeDir.E, ResizeDir.SE, ResizeDir.S, ResizeDir.SW, ResizeDir.W };
            foreach (ResizeDir dir in all)
            {
                // 控制点方向必须完全落在可调整方向内（如Dock.Left时四角/上下边均不可用，仅E可用）
                if ((dir & dirs) != dir) continue;
                bool n = (dir & ResizeDir.N) != 0, s = (dir & ResizeDir.S) != 0;
                bool w = (dir & ResizeDir.W) != 0, e = (dir & ResizeDir.E) != 0;
                int x = w ? b.Left - HandleOffset : e ? b.Right + HandleOffset : b.X + b.Width / 2;
                int y = n ? b.Top - HandleOffset : s ? b.Bottom + HandleOffset : b.Y + b.Height / 2;
                yield return new KeyValuePair<Rectangle, ResizeDir>(
                    new Rectangle(x - HandleSize / 2, y - HandleSize / 2, HandleSize, HandleSize), dir);
            }
        }

        /// <summary>命中测试：屏幕坐标处是否为选中控件（单选）的缩放控制点，命中返回方向。
        /// 控制点矩形位于父容器坐标，统一转换到屏幕坐标比较，嵌套控件同样适用</summary>
        private ResizeDir GetHandleAt(Point screenPt)
        {
            if (_selectedControls.Count != 1) return ResizeDir.None;
            Control sel = _selectedControls[0];
            if (sel.Parent == null) return ResizeDir.None;
            foreach (KeyValuePair<Rectangle, ResizeDir> h in GetHandles(sel))
            {
                Rectangle r = h.Key;
                r.Inflate(HandleTolerance, HandleTolerance);
                if (sel.Parent.RectangleToScreen(r).Contains(screenPt)) return h.Value;
            }
            return ResizeDir.None;
        }

        #endregion

        #region 拖动与缩放

        private void StartResize(Control ctrl, ResizeDir dir)
        {
            _isResize = true;
            _isMove = false;
            _isMarquee = false;
            _resizeControl = ctrl;
            _resizeDir = dir;
            _resizeStartBounds = ctrl.Bounds;
            _resizeStartMouse = Cursor.Position;
        }

        private void StartMove()
        {
            _moveStartMouse = Cursor.Position;
            _moveStartLocations = new Dictionary<Control, Point>();
            foreach (Control c in _selectedControls)
            {
                // 停靠控件位置由布局引擎管理，不允许拖动
                if (c.Dock != DockStyle.None) continue;
                _moveStartLocations[c] = c.Location;
            }
            _isMove = _moveStartLocations.Count > 0;
            _isResize = false;
            _isMarquee = false;
        }

        /// <summary>组拖动：所有选中控件按相同位移移动</summary>
        private void ApplyMove()
        {
            if (_moveStartLocations == null) return;
            int dx = Cursor.Position.X - _moveStartMouse.X;
            int dy = Cursor.Position.Y - _moveStartMouse.Y;
            foreach (KeyValuePair<Control, Point> kv in _moveStartLocations)
                kv.Key.Location = new Point(kv.Value.X + dx, kv.Value.Y + dy);
            RefreshOverlay();
        }

        /// <summary>按起始边界+方向调整尺寸；停靠控件仅调整布局允许的尺寸方向，位置交给布局引擎</summary>
        private void ApplyResize()
        {
            int dx = Cursor.Position.X - _resizeStartMouse.X;
            int dy = Cursor.Position.Y - _resizeStartMouse.Y;
            Rectangle b = _resizeStartBounds;
            int left = b.Left, top = b.Top, width = b.Width, height = b.Height;
            bool n = (_resizeDir & ResizeDir.N) != 0, s = (_resizeDir & ResizeDir.S) != 0;
            bool w = (_resizeDir & ResizeDir.W) != 0, e = (_resizeDir & ResizeDir.E) != 0;
            if (w) { width = Math.Max(MinCtrlSize, b.Width - dx); left = b.Right - width; }
            if (e) { width = Math.Max(MinCtrlSize, b.Width + dx); }
            if (n) { height = Math.Max(MinCtrlSize, b.Height - dy); top = b.Bottom - height; }
            if (s) { height = Math.Max(MinCtrlSize, b.Height + dy); }

            if (_resizeControl.Dock == DockStyle.None)
            {
                _resizeControl.Bounds = new Rectangle(left, top, width, height);
            }
            else
            {
                // 停靠控件：GetResizableDirs保证只允许单方向，仅改对应尺寸，位置由布局管理
                if (w || e) _resizeControl.Width = width;
                if (n || s) _resizeControl.Height = height;
            }
            RefreshOverlay();
        }

        /// <summary>结束所有拖动/缩放状态</summary>
        private void EndDrag()
        {
            _isMove = false;
            _isResize = false;
            _resizeControl = null;
            _moveStartLocations = null;
            // 缩放后刷新属性面板显示值
            if (propertyGrid1.SelectedObject != null) propertyGrid1.Refresh();
        }

        /// <summary>悬停时若命中控制点则显示对应方向的调整光标</summary>
        private void UpdateHandleCursor()
        {
            ResizeDir dir = GetHandleAt(Cursor.Position);
            Cursor.Current =
                dir == ResizeDir.N || dir == ResizeDir.S ? Cursors.SizeNS :
                dir == ResizeDir.W || dir == ResizeDir.E ? Cursors.SizeWE :
                dir == ResizeDir.NW || dir == ResizeDir.SE ? Cursors.SizeNWSE :
                dir == ResizeDir.NE || dir == ResizeDir.SW ? Cursors.SizeNESW :
                Cursors.Default;
        }

        #endregion

        #region 鼠标事件

        /// <summary>递归绑定设计器鼠标事件到控件及其全部子控件；
        /// 同时把每个设计控件注册为拖放目标（OLE拖放按HWND投递，
        /// 悬停在未注册的控件上时无法放置），实际容器由GetDropContainer解析</summary>
        private void BindDesignerEvents(Control ctrl)
        {
            ctrl.MouseDown += Control_MouseDown;
            ctrl.MouseMove += Control_MouseMove;
            ctrl.MouseUp += Control_MouseUp;
            ctrl.AllowDrop = true;
            ctrl.DragEnter += DesignedControl_DragEnter;
            ctrl.DragDrop += DesignedControl_DragDrop;

            // 容器控件承担其内部嵌套设计控件的选区/控制点绘制
            if (ctrl is Panel || ctrl is SplitContainer)
                ctrl.Paint += DesignedContainer_Paint;

            foreach (Control child in ctrl.Controls)
                BindDesignerEvents(child);
        }

        /// <summary>向上解析鼠标事件来源所属的设计控件（_designed标记集内的最近祖先）。
        /// UserControl的内部子控件归到该控件本身；嵌套拖入容器内的控件解析到自身</summary>
        private Control GetDesignedControl(Control source)
        {
            Control c = source;
            while (c != null && !_designed.Contains(c))
                c = c.Parent;
            return c;
        }

        // 设计控件鼠标按下：优先命中缩放控制点（控制点可能落在父容器表面，事件来源
        // 不一定是选中控件本身）；其次右键层级菜单、Ctrl多选切换、普通点击选中并开始组拖动
        private void Control_MouseDown(object sender, MouseEventArgs e)
        {
            ResizeDir hitDir = GetHandleAt(Cursor.Position);
            if (hitDir != ResizeDir.None && _selectedControls.Count == 1)
            {
                StartResize(_selectedControls[0], hitDir);
                return;
            }

            Control ctrl = GetDesignedControl(sender as Control);
            if (ctrl == null) return;
            if (e.Button == MouseButtons.Right)
            {
                // 右键未选中控件时先选中；已参与多选则保持整组
                if (!_selectedControls.Contains(ctrl)) SetSelection(ctrl);
                ShowZOrderMenu(ctrl);
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            // 点击分隔条时交给SplitContainer原生调整逻辑，避免拖动分隔条移动整个容器
            if (ctrl is SplitContainer && ((SplitContainer)ctrl).SplitterRectangle
                .Contains(((Control)sender).PointToClient(Cursor.Position)))
                return;

            if ((ModifierKeys & Keys.Control) != 0)
            {
                ToggleSelection(ctrl);
                return;
            }
            if (!_selectedControls.Contains(ctrl)) SetSelection(ctrl);
            StartMove();
        }

        private void Control_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isResize) { ApplyResize(); return; }
            if (_isMove) { ApplyMove(); return; }
            UpdateHandleCursor();
        }

        private void Control_MouseUp(object sender, MouseEventArgs e)
        {
            EndDrag();
        }

        // 面板空白处按下：右键显示层级菜单；左键命中控制点则缩放；否则Ctrl保留原选区（框选累加），普通点击清空后框选
        private void Panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowZOrderMenu(null);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            ResizeDir dir = GetHandleAt(Cursor.Position);
            if (dir != ResizeDir.None && _selectedControls.Count == 1)
            {
                StartResize(_selectedControls[0], dir);
                return;
            }
            if ((ModifierKeys & Keys.Control) == 0) SetSelection(null);
            _isMarquee = true;
            _isMove = false;
            _isResize = false;
            _marqueeStart = e.Location;
            _marquee = Rectangle.Empty;
        }

        private void Panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isResize) { ApplyResize(); return; }
            if (_isMove) { ApplyMove(); return; }
            if (_isMarquee)
            {
                int x = Math.Min(_marqueeStart.X, e.X);
                int y = Math.Min(_marqueeStart.Y, e.Y);
                _marquee = new Rectangle(x, y, Math.Abs(e.X - _marqueeStart.X), Math.Abs(e.Y - _marqueeStart.Y));
                RefreshOverlay();
                return;
            }
            UpdateHandleCursor();
        }

        private void Panel1_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isMarquee)
            {
                if (!_marquee.IsEmpty)
                {
                    List<Control> hits = panel1.Controls.Cast<Control>()
                        .Where(c => c.Visible && _marquee.IntersectsWith(c.Bounds))
                        .ToList();
                    if ((ModifierKeys & Keys.Control) == 0)
                    {
                        _selectedControls.Clear();
                        _selectedControls.AddRange(hits);
                    }
                    else
                    {
                        foreach (Control c in hits)
                            if (!_selectedControls.Contains(c)) _selectedControls.Add(c);
                    }
                    SyncSelectionUI();
                }
                _isMarquee = false;
                _marquee = Rectangle.Empty;
                RefreshOverlay();
            }
            EndDrag();
        }

        #endregion

        #region 事件绑定

        // 设计态：绑定仅保存配置，不订阅事件——事件绑定在界面设计中不生效，
        // 随设计序列化保存后由运行窗口（RuntimeForm）恢复并生效

        private int _ctrlSeq; // 控件自动命名序号

        /// <summary>打开事件绑定编辑器。Button支持鼠标点击/双击事件（绑定流程/打开窗口）；
        /// HDisplayControl/NumericUpDown/TextBox编辑器内为占位，暂未实现</summary>
        private void ShowBindingEditor(Control ctrl)
        {
            Dictionary<string, EventBinding> bindings;
            if (!_eventBindings.TryGetValue(ctrl, out bindings))
            {
                bindings = new Dictionary<string, EventBinding>();
                _eventBindings[ctrl] = bindings;
            }
            using (EventBindingEditorForm form = new EventBindingEditorForm(ctrl, bindings))
            {
                form.ShowDialog(this);
            }
        }

        #endregion

        #region 设计加载（自动）

        /// <summary>设计器重建回调：绑定设计器事件、登记设计控件、恢复绑定表配置</summary>
        private void OnDesignControlCreated(Control c, ControlData data)
        {
            BindDesignerEvents(c);
            _designed.Add(c);
            if (data.Bindings.Count > 0)
            {
                Dictionary<string, EventBinding> map = new Dictionary<string, EventBinding>();
                foreach (BindingData bd in data.Bindings)
                    map[bd.EventName] = new EventBinding { FunctionType = bd.FunctionType, Target = bd.Target };
                _eventBindings[c] = map;
            }
        }

        #endregion

        #region 右键层级菜单

        /// <summary>构建并显示层级菜单：列出clicked控件父容器的全部子控件（顶层→底层，
        /// WinForms中GetChildIndex(0)=最顶层），点击项选中对应控件；底部提供置于顶层/
        /// 发送到底层操作，作用于当前选中控件（支持多选，保持选区内相对层序）。
        /// 右键空白处（clicked=null）显示panel1顶层层级，右键嵌套控件显示其所在容器的层级</summary>
        private void ShowZOrderMenu(Control clicked)
        {
            _zOrderMenu.Items.Clear();

            // 四类控件支持事件绑定编辑（Button完整功能；HDisplayControl/NumericUpDown/TextBox占位）
            if (clicked is Button || clicked is HToolBase.Controls.HDisplayControl
                || clicked is NumericUpDown || clicked is TextBox)
            {
                ToolStripMenuItem bindItem = new ToolStripMenuItem("事件绑定...");
                bindItem.Click += (s, ev) => ShowBindingEditor(clicked);
                _zOrderMenu.Items.Add(bindItem);
                _zOrderMenu.Items.Add(new ToolStripSeparator());
            }

            Control container = clicked != null && clicked.Parent != null ? clicked.Parent : panel1;
            _zOrderMenu.Items.Add(new ToolStripLabel("控件层级（顶层 → 底层）"));

            // Controls集合顺序≠层序，按GetChildIndex升序枚举即 顶层→底层
            List<Control> ordered = container.Controls.Cast<Control>()
                .OrderBy(c => container.Controls.GetChildIndex(c))
                .ToList();
            if (ordered.Count == 0)
            {
                _zOrderMenu.Items.Add(new ToolStripLabel("（无控件）"));
            }
            else
            {
                foreach (Control c in ordered)
                {
                    string label = string.IsNullOrEmpty(c.Name) ? c.GetType().Name : c.Name;
                    ToolStripMenuItem item = new ToolStripMenuItem(label) { Tag = c, Checked = _selectedControls.Contains(c) };
                    item.Click += (s, ev) => SetSelection((Control)((ToolStripMenuItem)s).Tag);
                    _zOrderMenu.Items.Add(item);
                }
            }

            _zOrderMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem frontItem = new ToolStripMenuItem("置于顶层");
            frontItem.Click += (s, ev) => ApplyZOrder(true);
            ToolStripMenuItem backItem = new ToolStripMenuItem("发送到底层");
            backItem.Click += (s, ev) => ApplyZOrder(false);
            bool hasSelection = _selectedControls.Count > 0;
            frontItem.Enabled = hasSelection;
            backItem.Enabled = hasSelection;
            _zOrderMenu.Items.Add(frontItem);
            _zOrderMenu.Items.Add(backItem);

            _zOrderMenu.Show(Cursor.Position);
        }

        /// <summary>对选中控件执行置顶/置底（在其各自父容器内），并保持多选时选区内的
        /// 相对层序：BringToFront最后一次调用者位于最前，故按原层序"底层→顶层"依次调用；
        /// SendToBack最后一次调用者位于最后，故按原层序"顶层→底层"依次调用</summary>
        private void ApplyZOrder(bool toFront)
        {
            if (_selectedControls.Count == 0) return;
            if (toFront)
            {
                foreach (Control c in _selectedControls.Where(c => c.Parent != null)
                             .OrderByDescending(c => c.Parent.Controls.GetChildIndex(c)))
                    c.BringToFront();
            }
            else
            {
                foreach (Control c in _selectedControls.Where(c => c.Parent != null)
                             .OrderBy(c => c.Parent.Controls.GetChildIndex(c)))
                    c.SendToBack();
            }
            RefreshOverlay();
        }

        #endregion

        #region 绘制

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            DrawSelectionUI(e.Graphics, panel1);
        }

        /// <summary>嵌套设计控件的选区/控制点绘制在其父容器表面
        /// （panel1画布会被中间容器窗口裁剪，画不到嵌套控件周围）</summary>
        private void DesignedContainer_Paint(object sender, PaintEventArgs e)
        {
            DrawSelectionUI(e.Graphics, (Control)sender);
        }

        /// <summary>在容器表面绘制选区UI：框选矩形（仅panel1）、选中控件的蓝色虚线框、
        /// 单选时的缩放控制点。控件边界与控制点均使用父容器客户区坐标</summary>
        private void DrawSelectionUI(Graphics g, Control container)
        {
            // 框选矩形：半透明填充+虚线边框（仅panel1画布支持框选）
            if (container == panel1 && _isMarquee && !_marquee.IsEmpty)
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(30, 0, 120, 215)))
                    g.FillRectangle(brush, _marquee);
                using (Pen pen = new Pen(Color.FromArgb(0, 120, 215)))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawRectangle(pen, _marquee);
                }
            }

            // 该容器内的每个选中控件绘制蓝色虚线框
            foreach (Control c in _selectedControls)
            {
                if (c.Parent != container) continue;
                Rectangle rect = c.Bounds;
                rect.Inflate(1, 1);
                using (Pen pen = new Pen(Color.Blue))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawRectangle(pen, rect);
                }
            }

            // 单选时绘制缩放控制点（8个小方块，方向随Dock布局自动增减）
            if (_selectedControls.Count == 1 && _selectedControls[0].Parent == container)
            {
                foreach (KeyValuePair<Rectangle, ResizeDir> h in GetHandles(_selectedControls[0]))
                {
                    g.FillRectangle(Brushes.White, h.Key);
                    g.DrawRectangle(Pens.DodgerBlue, h.Key);
                }
            }
        }

        /// <summary>递归清理设计控件标记集与事件绑定表（删除容器时其内部嵌套的设计控件一并移除）</summary>
        private void StripDesigned(Control root)
        {
            _designed.Remove(root);
            _eventBindings.Remove(root);
            foreach (Control child in root.Controls.Cast<Control>())
                StripDesigned(child);
        }

        #endregion
    }
}
