using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HToolBase;

namespace HDesigner
{
    /// <summary>事件绑定配置模型</summary>
    public class EventBinding
    {
        public const string RunProcess = "RunProcess"; // 绑定流程：事件发生时运行绑定的流程
        public const string OpenWindow = "OpenWindow"; // 打开窗口：事件发生时打开所选窗口
        public const string BindImage = "BindImage";   // 绑定图像：图像变化时更新HDisplayControl显示

        public string FunctionType = RunProcess; // 功能类型
        public string Target;                    // 流程名称 / 窗口类型全名 / "流程名|模块Text|端口名"
    }

    /// <summary>事件绑定编辑器：
    /// Button——鼠标点击/双击事件，功能类型：绑定流程（运行ProcessManager中的流程）/打开窗口（实例化并显示Form）；
    /// HDisplayControl——输入图像绑定：选择流程→该流程ToolModule的HObject(IMAGE)图像输出，图像变化时更新显示；
    /// NumericUpDown/TextBox（绑定程序内部变量）——编辑界面占位，暂未实现</summary>
    internal class EventBindingEditorForm : Form
    {
        private const string EventMouseClick = "MouseClick";
        private const string EventDoubleClick = "DoubleClick";

        private readonly Control _target;
        // 事件名 → 绑定配置（编辑副本，确定后由DesignerForm取回）
        private readonly Dictionary<string, EventBinding> _bindings;

        // Button编辑UI
        private ComboBox _eventCombo;
        private GroupBox _configGroup;
        private RadioButton _rbProcess, _rbWindow;
        private ComboBox _processCombo, _windowCombo;
        private Button _clearBtn, _okBtn, _cancelBtn;
        private string _currentEvent = EventMouseClick;

        // HDisplayControl图像绑定UI：流程 → 模块(ToolModule) → HObject图像输出端口
        private ComboBox _imgProcessCombo, _imgModuleCombo, _imgPortCombo;
        private Label _imgStatusLbl;

        /// <summary>编辑后的绑定表（事件名 → 配置）</summary>
        public Dictionary<string, EventBinding> Bindings { get { return _bindings; } }

        public EventBindingEditorForm(Control target, Dictionary<string, EventBinding> bindings)
        {
            _target = target;
            _bindings = bindings;

            Text = "事件绑定 - " + target.GetType().Name;
            ClientSize = new Size(470, 320);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            if (target is Button) InitButtonUI();
            else if (target is HToolBase.Controls.HDisplayControl) InitImageBindUI();
            else InitPlaceholderUI();
        }

        #region Button事件编辑

        private void InitButtonUI()
        {
            Label lblEvent = new Label { Text = "事件：", Location = new Point(16, 21), AutoSize = true };
            _eventCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(66, 17),
                Size = new Size(200, 23)
            };
            _eventCombo.Items.Add(new KeyValuePair<string, string>(EventMouseClick, "鼠标点击 (MouseClick)"));
            _eventCombo.Items.Add(new KeyValuePair<string, string>(EventDoubleClick, "双击 (DoubleClick)"));
            _eventCombo.DisplayMember = "Value";
            _eventCombo.SelectedIndexChanged += EventCombo_SelectedIndexChanged;
            Controls.Add(lblEvent);
            Controls.Add(_eventCombo);

            _configGroup = new GroupBox { Text = "功能类型", Location = new Point(16, 52), Size = new Size(438, 168) };

            _rbProcess = new RadioButton
            {
                Text = "绑定流程（事件发生时运行绑定的流程）",
                Location = new Point(14, 26),
                AutoSize = true
            };
            _rbProcess.CheckedChanged += Radio_CheckedChanged;
            _processCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(36, 56), Size = new Size(380, 23) };

            _rbWindow = new RadioButton
            {
                Text = "打开窗口（事件发生时打开所选窗口）",
                Location = new Point(14, 96),
                AutoSize = true
            };
            _rbWindow.CheckedChanged += Radio_CheckedChanged;
            _windowCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(36, 126), Size = new Size(380, 23) };

            _configGroup.Controls.Add(_rbProcess);
            _configGroup.Controls.Add(_processCombo);
            _configGroup.Controls.Add(_rbWindow);
            _configGroup.Controls.Add(_windowCombo);
            Controls.Add(_configGroup);

            _clearBtn = new Button { Text = "清除该事件绑定", Location = new Point(16, 238), Size = new Size(120, 26) };
            _clearBtn.Click += ClearBtn_Click;
            Controls.Add(_clearBtn);

            _okBtn = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(256, 278), Size = new Size(90, 28) };
            _okBtn.Click += OkBtn_Click;
            _cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(356, 278), Size = new Size(90, 28) };
            Controls.Add(_okBtn);
            Controls.Add(_cancelBtn);
            AcceptButton = _okBtn;
            CancelButton = _cancelBtn;

            List<string> processNames = GetProcessNames();
            foreach (string name in processNames) _processCombo.Items.Add(name);
            if (processNames.Count == 0)
            {
                _processCombo.Items.Add("（当前无可用流程）");
                _processCombo.Enabled = false;
            }
            foreach (string window in GetWindowTypeNames()) _windowCombo.Items.Add(window);

            _eventCombo.SelectedIndex = 0;
        }

        private void EventCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            SaveCurrentEventUI();
            _currentEvent = ((KeyValuePair<string, string>)_eventCombo.SelectedItem).Key;
            LoadCurrentEventUI();
        }

        private void Radio_CheckedChanged(object sender, EventArgs e)
        {
            bool process = _rbProcess.Checked;
            if (_processCombo.Enabled || process) _processCombo.Enabled = process;
            _windowCombo.Enabled = !process;
        }

        private void LoadCurrentEventUI()
        {
            EventBinding b;
            bool has = _bindings.TryGetValue(_currentEvent, out b);
            if (has && b.FunctionType == EventBinding.OpenWindow)
            {
                _rbWindow.Checked = true;
                SelectCombo(_windowCombo, b.Target);
            }
            else
            {
                _rbProcess.Checked = true;
                if (has) SelectCombo(_processCombo, b.Target);
                else if (_processCombo.Items.Count > 0) _processCombo.SelectedIndex = 0;
            }
        }

        private static void SelectCombo(ComboBox combo, string value)
        {
            int idx = combo.Items.IndexOf(value);
            combo.SelectedIndex = idx >= 0 ? idx : (combo.Items.Count > 0 ? 0 : -1);
        }

        private void SaveCurrentEventUI()
        {
            if (_eventCombo == null || _eventCombo.SelectedIndex < 0) return;
            if (_rbProcess == null) return;
            bool window = _rbWindow.Checked;
            string target = window
                ? (string)_windowCombo.SelectedItem
                : (string)_processCombo.SelectedItem;

            if (string.IsNullOrEmpty(target))
            {
                _bindings.Remove(_currentEvent);
                return;
            }
            EventBinding b;
            if (!_bindings.TryGetValue(_currentEvent, out b))
            {
                b = new EventBinding();
                _bindings[_currentEvent] = b;
            }
            b.FunctionType = window ? EventBinding.OpenWindow : EventBinding.RunProcess;
            b.Target = target;
        }

        private void ClearBtn_Click(object sender, EventArgs e)
        {
            _bindings.Remove(_currentEvent);
            LoadCurrentEventUI();
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {
            SaveCurrentEventUI();
        }

        /// <summary>流程列表：ProcessManager单例中的全部流程名称（HDesigner与宿主共用同一单例）</summary>
        private static List<string> GetProcessNames()
        {
            try
            {
                return ProcessManager.instance().Processes.Keys.OrderBy(k => k).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>可打开的窗口列表：已加载程序集中所有可实例化的Form类型（排除设计器自身）</summary>
        private static List<string> GetWindowTypeNames()
        {
            List<string> names = new List<string>();
            string selfAsm = Assembly.GetExecutingAssembly().GetName().Name;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                if (asm.GetName().Name == selfAsm) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                catch { continue; }
                foreach (Type t in types)
                {
                    if (!t.IsSubclassOf(typeof(Form)) || t.IsAbstract) continue;
                    if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                    names.Add(t.FullName);
                }
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        #endregion

        #region HDisplayControl图像绑定

        /// <summary>HDisplayControl输入图像绑定：流程 → 流程内ToolModule → 其ToolBlock
        /// 输出中类型为IMAGE(HObject)的端口。绑定保存为"流程名|模块Text|端口名"，
        /// 运行窗口轮询该端口Value，图像变化时更新控件显示（设计态不生效）</summary>
        private void InitImageBindUI()
        {
            Label lblTitle = new Label
            {
                Text = "输入图像绑定（图像变化时更新控件显示）",
                Location = new Point(16, 18),
                AutoSize = true
            };
            Controls.Add(lblTitle);

            Func<string, Label, ComboBox> makeRow = (labelText, lbl) =>
            {
                lbl.Text = labelText;
                lbl.Location = new Point(16, 0);
                lbl.AutoSize = true;
                ComboBox combo = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Size = new Size(360, 23)
                };
                return combo;
            };

            Label lblP = new Label(), lblM = new Label(), lblPort = new Label();
            _imgProcessCombo = makeRow("流程：", lblP);
            _imgProcessCombo.Location = new Point(16, 44);
            _imgProcessCombo.SelectedIndexChanged += (s, e) => RefreshImageModules();
            _imgModuleCombo = makeRow("工具模块：", lblM);
            _imgModuleCombo.Location = new Point(16, 80);
            _imgModuleCombo.SelectedIndexChanged += (s, e) => RefreshImagePorts();
            _imgPortCombo = makeRow("图像输出(HObject)：", lblPort);
            _imgPortCombo.Location = new Point(16, 116);

            Controls.Add(lblP);
            Controls.Add(_imgProcessCombo);
            Controls.Add(lblM);
            Controls.Add(_imgModuleCombo);
            Controls.Add(lblPort);
            Controls.Add(_imgPortCombo);

            _imgStatusLbl = new Label
            {
                Text = "",
                Location = new Point(16, 152),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            Controls.Add(_imgStatusLbl);

            _okBtn = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(256, 278), Size = new Size(90, 28) };
            _okBtn.Click += ImageBindOk_Click;
            _cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(356, 278), Size = new Size(90, 28) };
            _clearBtn = new Button { Text = "清除绑定", Location = new Point(16, 278), Size = new Size(90, 28) };
            _clearBtn.Click += (s, e) =>
            {
                _bindings.Remove("Image");
                LoadImageBind(null);
            };
            Controls.Add(_okBtn);
            Controls.Add(_cancelBtn);
            Controls.Add(_clearBtn);
            AcceptButton = _okBtn;
            CancelButton = _cancelBtn;

            List<string> processNames = GetProcessNames();
            foreach (string name in processNames) _imgProcessCombo.Items.Add(name);
            if (processNames.Count == 0)
                _imgStatusLbl.Text = "当前无可用流程";

            // 恢复已有绑定："流程名|模块Text|端口名"
            EventBinding b;
            _bindings.TryGetValue("Image", out b);
            LoadImageBind(b?.Target);
        }

        private void LoadImageBind(string target)
        {
            string p = null, m = null, port = null;
            if (!string.IsNullOrEmpty(target))
            {
                string[] parts = target.Split('|');
                p = parts.Length > 0 ? parts[0] : null;
                m = parts.Length > 1 ? parts[1] : null;
                port = parts.Length > 2 ? parts[2] : null;
            }
            if (p != null && _imgProcessCombo.Items.Contains(p)) _imgProcessCombo.SelectedItem = p;
            else if (_imgProcessCombo.Items.Count > 0) _imgProcessCombo.SelectedIndex = 0;
            else { _imgModuleCombo.Items.Clear(); _imgPortCombo.Items.Clear(); return; }

            RefreshImageModules();
            if (m != null && _imgModuleCombo.Items.Contains(m)) _imgModuleCombo.SelectedItem = m;
            else if (_imgModuleCombo.Items.Count > 0) _imgModuleCombo.SelectedIndex = 0;

            RefreshImagePorts();
            if (port != null && _imgPortCombo.Items.Contains(port)) _imgPortCombo.SelectedItem = port;
            else if (_imgPortCombo.Items.Count > 0) _imgPortCombo.SelectedIndex = 0;
        }

        /// <summary>刷新选中流程内的ToolModule列表（按模块Text标识）</summary>
        private void RefreshImageModules()
        {
            _imgModuleCombo.Items.Clear();
            _imgPortCombo.Items.Clear();
            string pn = (string)_imgProcessCombo.SelectedItem;
            if (pn == null) return;
            try
            {
                HToolBase.ProcessPanel panel;
                if (!ProcessManager.instance().Processes.TryGetValue(pn, out panel) || panel == null) return;
                foreach (ToolModule tm in panel.modules.OfType<ToolModule>())
                {
                    string key = tm.Text ?? tm.Name;
                    if (!string.IsNullOrEmpty(key) && !_imgModuleCombo.Items.Contains(key))
                        _imgModuleCombo.Items.Add(key);
                }
            }
            catch { }
            if (_imgModuleCombo.Items.Count == 0)
                _imgStatusLbl.Text = "该流程内没有工具模块";
            else
                _imgStatusLbl.Text = "";
            if (_imgModuleCombo.Items.Count > 0) _imgModuleCombo.SelectedIndex = 0;
        }

        /// <summary>刷新选中ToolModule的ToolBlock输出中类型为IMAGE(HObject)的端口</summary>
        private void RefreshImagePorts()
        {
            _imgPortCombo.Items.Clear();
            string pn = (string)_imgProcessCombo.SelectedItem;
            string mn = (string)_imgModuleCombo.SelectedItem;
            if (pn == null || mn == null) return;
            try
            {
                HToolBase.ProcessPanel panel;
                if (!ProcessManager.instance().Processes.TryGetValue(pn, out panel) || panel == null) return;
                ToolModule tm = panel.modules.OfType<ToolModule>()
                    .FirstOrDefault(x => (x.Text ?? x.Name) == mn);
                if (tm == null) return;
                foreach (PortNode port in tm.GetToolBlock().Outputs.Values)
                {
                    if (port.PortType == TypeName.IMAGE && !string.IsNullOrEmpty(port.PortName))
                        _imgPortCombo.Items.Add(port.PortName);
                }
            }
            catch { }
            if (_imgPortCombo.Items.Count == 0)
                _imgStatusLbl.Text = "该模块没有HObject(IMAGE)类型的图像输出端口";
            else
                _imgStatusLbl.Text = "";
            if (_imgPortCombo.Items.Count > 0) _imgPortCombo.SelectedIndex = 0;
        }

        private void ImageBindOk_Click(object sender, EventArgs e)
        {
            string pn = (string)_imgProcessCombo.SelectedItem;
            string mn = (string)_imgModuleCombo.SelectedItem;
            string port = (string)_imgPortCombo.SelectedItem;
            if (string.IsNullOrEmpty(pn) || string.IsNullOrEmpty(mn) || string.IsNullOrEmpty(port))
            {
                _bindings.Remove("Image");
                return;
            }
            EventBinding b;
            if (!_bindings.TryGetValue("Image", out b))
            {
                b = new EventBinding { FunctionType = EventBinding.BindImage };
                _bindings["Image"] = b;
            }
            b.FunctionType = EventBinding.BindImage;
            b.Target = pn + "|" + mn + "|" + port;
        }

        #endregion

        #region 占位（暂未实现的绑定类型）

        private void InitPlaceholderUI()
        {
            string section;
            if (_target is NumericUpDown)
                section = "绑定程序内部变量（支持 Int / double 类型）";
            else if (_target is TextBox)
                section = "绑定程序内部变量";
            else
                section = "该控件类型暂不支持事件绑定";

            Label lblTitle = new Label { Text = section, Location = new Point(20, 30), AutoSize = true };
            Label lblTip = new Label
            {
                Text = "（暂未实现）",
                Location = new Point(20, 60),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            Controls.Add(lblTitle);
            Controls.Add(lblTip);

            _cancelBtn = new Button { Text = "关闭", DialogResult = DialogResult.Cancel, Location = new Point(356, 278), Size = new Size(90, 28) };
            Controls.Add(_cancelBtn);
            CancelButton = _cancelBtn;
        }

        #endregion
    }
}
