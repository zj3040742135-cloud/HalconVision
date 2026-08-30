using HalconDotNet;
using HToolBase.Tools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HToolBase.Controls
{
    /// <summary>
    /// 脚本编辑器窗体（从 WindowsFormsApp5.ScriptContral 移植并适配 Hal 架构）。
    /// 统一服务于两种脚本形态（均通过 IScriptHost 接入）：
    ///  - Form1：ScriptTool（端口型工具），脚本仅访问自身端口；本窗体提供端口增删 UI。
    ///  - Form2：ToolBlock 块级脚本，脚本访问整个 ToolBlock 与兄弟工具；本窗体提供"脚本/默认运行"模式切换。
    /// 保留原 IDE 全部能力：行号、语法高亮、智能联想（成员/关键字/已创建对象）、外部脚本加载/保存、输出框。
    /// </summary>
    public partial class ScriptToolForm : Form
    {
        #region 核心配置与API
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern int GetScrollPos(IntPtr hWnd, int nBar);
        [DllImport("user32.dll")]
        private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

        private const int WM_SETREDRAW = 0x000B;
        private const int SB_VERT = 0x0001;
        private const int WM_VSCROLL = 0x0115;

        private readonly IScriptHost _host;

        private string _tempScriptPath;

        private Panel _lineNumberPanel;
        private RichTextBox _lineNumberBox;

        private ListBox _autoCompleteList;
        private Timer _autoCompleteTimer;
        private bool _isAutoCompleting = false;
        private string _currentInputPrefix = "";
        private string _currentObjectName = "";
        private bool _isAfterDot = false;
        private const int AutoCompleteDelay = 150;
        private const int MinPrefixLength = 1;
        private bool _isSpecialCharHide = false;

        private readonly Timer _highlightTimer;
        private const int HighlightDelay = 100;
        private bool _isHighlighting = false;

        private int _lastCaretPosition = 0;
        private bool _isProgrammaticChange = false;
        private bool _skipNextTextChange = false;

        private readonly Dictionary<string, Color> _keywordColors;
        private HashSet<string> _keywords;
        private List<string> _autoCompleteItems;

        private Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        private Dictionary<Type, List<string>> _typeMembersCache = new Dictionary<Type, List<string>>();

        // 动态对象缓存：存储脚本中通过new创建的对象
        private Dictionary<string, string> _dynamicObjectCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _dynamicObjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 端口增删用上下文菜单（Form1）
        private ContextMenuStrip _portTypeMenu;
        private bool _addingInput = true;
        // 运行模式切换按钮（Form2）
        private ToolStripButton _runModeButton;
        #endregion

        public ScriptToolForm(IScriptHost host)
        {
            InitializeComponent();
            _host = host ?? throw new ArgumentNullException(nameof(host));

            _tempScriptPath = Path.Combine(Path.GetTempPath(), "CurrentScript_" + Guid.NewGuid().ToString("N") + ".cs");

            _keywordColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
            {
                { "using", Color.Blue }, { "public", Color.Magenta }, { "class", Color.Magenta },
                { "private", Color.Magenta }, { "protected", Color.Magenta }, { "static", Color.Magenta },
                { "void", Color.Green }, { "string", Color.Green }, { "int", Color.Green },
                { "double", Color.Green }, { "bool", Color.Green }, { "if", Color.Blue },
                { "else", Color.Blue }, { "for", Color.Blue }, { "foreach", Color.Blue },
                { "while", Color.Blue }, { "return", Color.Blue }, { "try", Color.Blue },
                { "catch", Color.Blue }, { "this", Color.Brown }, { "new", Color.Blue },
                { "null", Color.DarkGray }, { "true", Color.DarkGray }, { "false", Color.DarkGray }
            };

            _keywords = new HashSet<string>(_keywordColors.Keys, StringComparer.OrdinalIgnoreCase);
            _autoCompleteItems = new List<string>(_keywords)
            {
                "System", "HalconDotNet", "HToolBase", "HOperatorSet", "HObject", "HRegion", "HXLD",
                "ToolBase", "ToolBlock", "PortNode", "TypeName", "ImageSourceTool", "BlobTool",
                "Tools", "Inputs", "Outputs", "Value", "Run", "Message", "IsRunSuccess",
                "Console", "WriteLine", "ToString", "Length", "Count", "FirstOrDefault", "Where", "Select",
                "var", "foreach", "in", "get", "set"
            };

            CacheCommonTypes();

            _highlightTimer = new Timer { Interval = HighlightDelay, Enabled = true };
            _highlightTimer.Tick += HighlightTimer_Tick;

            _autoCompleteTimer = new Timer { Interval = AutoCompleteDelay, Enabled = true };
            _autoCompleteTimer.Tick += AutoCompleteTimer_Tick;

            InitEditorWithLineNumbers();
            InitAutoCompleteList();
            InitPortTypeMenu();

            // 加载已保存脚本；若为空则加载示例
            string saved = _host.ScriptText;
            if (!string.IsNullOrEmpty(saved))
            {
                LoadSavedScript(saved);
            }
            else
            {
                LoadExampleScript();
            }

            _lastCaretPosition = 0;
            UpdateLineNumbers();
            UpdateRunModeButton();

            Text = _host.HostTitle;

            FormClosing += (s, e) => CleanupTempFiles();
            FormClosing += (s, e) =>
            {
                // 关闭时写回脚本，随宿主持久化（ScriptTool.vpp / ToolBlock.vpp）
                if (!_isProgrammaticChange)
                    _host.ScriptText = CodeEditor.Text;
            };
        }

        #region 类型缓存初始化
        private void CacheCommonTypes()
        {
            AddTypeToCache("string", typeof(string));
            AddTypeToCache("int", typeof(int));
            AddTypeToCache("bool", typeof(bool));
            AddTypeToCache("double", typeof(double));
            AddTypeToCache("object", typeof(object));
            AddTypeToCache("var", typeof(object));

            AddTypeToCache("List", typeof(List<>));
            AddTypeToCache("Dictionary", typeof(Dictionary<,>));

            AddTypeToCache("ToolBase", typeof(ToolBase));
            AddTypeToCache("ToolBlock", typeof(ToolBlock));
            AddTypeToCache("PortNode", typeof(PortNode));
            AddTypeToCache("HObject", typeof(HObject));
            AddTypeToCache("HRegion", typeof(HRegion));
            AddTypeToCache("HXLD", typeof(HXLD));
            AddTypeToCache("HOperatorSet", typeof(HOperatorSet));
            AddTypeToCache("Console", typeof(Console));
        }

        private void AddTypeToCache(string typeName, Type type)
        {
            if (!_typeCache.ContainsKey(typeName))
            {
                _typeCache[typeName] = type;
                GetTypeMembers(type);
            }
        }
        #endregion

        #region 行号显示核心功能
        private void InitEditorWithLineNumbers()
        {
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };

            _lineNumberPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 40,
                BackColor = Color.LightGray,
                BorderStyle = BorderStyle.Fixed3D
            };

            _lineNumberBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.LightGray,
                ForeColor = Color.DarkBlue,
                Font = new Font("Consolas", 12f),
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.None,
                BorderStyle = BorderStyle.None
            };
            _lineNumberPanel.Controls.Add(_lineNumberBox);

            CodeEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 12f),
                Multiline = true,
                AcceptsTab = true,
                ScrollBars = RichTextBoxScrollBars.Both,
                ReadOnly = false,
                HideSelection = false,
                Cursor = Cursors.IBeam,
                BorderStyle = BorderStyle.None
            };

            var doubleBufferProp = CodeEditor.GetType().GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);
            doubleBufferProp?.SetValue(CodeEditor, true);

            CodeEditor.VScroll += (s, e) => SyncLineNumberScroll();
            CodeEditor.TextChanged += (s, e) =>
            {
                if (_isProgrammaticChange || _skipNextTextChange)
                {
                    _skipNextTextChange = false;
                    return;
                }

                UpdateLineNumbers();
                _lastCaretPosition = CodeEditor.SelectionStart;

                _highlightTimer.Stop();
                _highlightTimer.Start();

                CheckForDotInput();
                ParseDynamicObjects();

                if (!_isSpecialCharHide && !_isAutoCompleting)
                {
                    _autoCompleteTimer.Stop();
                    _autoCompleteTimer.Start();
                }
            };

            CodeEditor.SelectionChanged += (s, e) =>
            {
                if (!_isHighlighting && !_isProgrammaticChange && !_isAutoCompleting)
                {
                    _lastCaretPosition = CodeEditor.SelectionStart;
                    HighlightCurrentLine();
                }
            };

            CodeEditor.Resize += (s, e) => UpdateLineNumbers();

            CodeEditor.KeyPress += (s, e) =>
            {
                if (e.KeyChar == '.')
                {
                    _isAfterDot = true;
                }
                else if (!char.IsLetterOrDigit(e.KeyChar) && e.KeyChar != '_' && e.KeyChar != (char)8)
                {
                    HideAutoCompleteList(true);
                    _isAfterDot = false;
                }
            };

            CodeEditor.KeyDown += (s, e) =>
            {
                if (_autoCompleteList.Visible)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.Up:
                        case Keys.Down:
                            e.SuppressKeyPress = true;
                            e.Handled = true;

                            if (e.KeyCode == Keys.Up)
                            {
                                MoveSelectionUp();
                            }
                            else
                            {
                                MoveSelectionDown();
                            }
                            return;

                        case Keys.Tab:
                            e.SuppressKeyPress = true;
                            e.Handled = true;
                            if (_autoCompleteList.SelectedItem != null)
                            {
                                InsertSelectedAutoCompleteItem();
                            }
                            return;
                    }
                }

                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
                {
                    if (_autoCompleteList.Visible)
                    {
                        _autoCompleteList.Visible = false;
                        _autoCompleteTimer.Stop();
                        _autoCompleteTimer.Start();
                    }
                    _isAfterDot = false;
                }
                else if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
                {
                    _autoCompleteTimer.Stop();
                    _autoCompleteTimer.Start();
                    _isAfterDot = false;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    HideAutoCompleteList();
                    _isAfterDot = false;
                }
            };

            mainContainer.Controls.Add(CodeEditor);
            mainContainer.Controls.Add(_lineNumberPanel);

            var toolStrip = new ToolStrip { Dock = DockStyle.Top };
            var items = new List<ToolStripItem>();
            items.Add(new ToolStripButton("执行脚本", null, ExecuteScript_Click));
            items.Add(new ToolStripButton("外部运行当前脚本", null, (s, e) => RunCurrentScriptExternally_Click()));
            items.Add(new ToolStripButton("运行外部脚本", null, RunExternalScript_Click));
            items.Add(new ToolStripButton("加载示例", null, (s, e) =>
            {
                LoadExampleScript();
                UpdateLineNumbers();
            }));
            items.Add(new ToolStripButton("保存脚本", null, (s, e) => SaveScript()));
            items.Add(new ToolStripButton("打开脚本", null, (s, e) => OpenScript()));

            // Form2：切换"脚本/默认运行"模式
            if (_host.CanSwitchRunMode)
            {
                _runModeButton = new ToolStripButton("脚本模式：关", null, (s, e) =>
                {
                    _host.UseScriptRun = !_host.UseScriptRun;
                    UpdateRunModeButton();
                });
                items.Add(_runModeButton);
            }

            // Form1：端口增删
            if (_host.CanManagePorts)
            {
                items.Add(new ToolStripSeparator());
                items.Add(new ToolStripButton("添加输入", null, (s, e) =>
                {
                    _addingInput = true;
                    _portTypeMenu.Show(Cursor.Position);
                }));
                items.Add(new ToolStripButton("添加输出", null, (s, e) =>
                {
                    _addingInput = false;
                    _portTypeMenu.Show(Cursor.Position);
                }));
                items.Add(new ToolStripButton("删除端口", null, (s, e) => ShowRemovePortMenu()));
            }

            toolStrip.Items.AddRange(items.ToArray());

            var statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
            StatusLabel = new ToolStripStatusLabel { Text = "初始化中..." };
            statusStrip.Items.Add(StatusLabel);

            OutputBox = new RichTextBox
            {
                Dock = DockStyle.Bottom,
                Height = 150,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.Lime
            };

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 750
            };
            splitContainer.Panel1.Controls.Add(mainContainer);
            splitContainer.Panel2.Controls.Add(OutputBox);

            Controls.Add(toolStrip);
            Controls.Add(splitContainer);
            Controls.Add(statusStrip);

            this.MouseClick += (s, e) =>
            {
                if (_autoCompleteList.Visible && !_autoCompleteList.Bounds.Contains(e.Location))
                {
                    HideAutoCompleteList();
                }
            };

            Load += (s, e) =>
            {
                CodeEditor.Focus();
                SetCaretPosition(_lastCaretPosition);
                UpdateLineNumbers();
            };

            UpdateStatus();
        }

        private void UpdateRunModeButton()
        {
            if (_runModeButton == null) return;
            bool on = _host.UseScriptRun;
            _runModeButton.Text = on ? "脚本模式：开" : "脚本模式：关";
            _runModeButton.BackColor = on ? Color.LightGreen : Color.Transparent;
        }

        private void UpdateStatus()
        {
            string mode;
            if (_host.CanSwitchRunMode)
                mode = _host.UseScriptRun ? "脚本运行模式（ToolBlock.Run 已被脚本替换）" : "默认运行模式";
            else
                mode = "工具脚本";
            StatusLabel.Text = $"{mode} | 编译状态：{(_host.Executor.HasCompiledScript ? "已编译" : "未编译")}";
        }

        private void MoveSelectionUp()
        {
            if (_autoCompleteList.Items.Count == 0) return;

            int newIndex = _autoCompleteList.SelectedIndex - 1;
            if (newIndex < 0)
            {
                newIndex = _autoCompleteList.Items.Count - 1;
            }

            _autoCompleteList.SelectedIndex = newIndex;
            _autoCompleteList.TopIndex = Math.Max(0, newIndex - 3);
        }

        private void MoveSelectionDown()
        {
            if (_autoCompleteList.Items.Count == 0) return;

            int newIndex = _autoCompleteList.SelectedIndex + 1;
            if (newIndex >= _autoCompleteList.Items.Count)
            {
                newIndex = 0;
            }

            _autoCompleteList.SelectedIndex = newIndex;
            _autoCompleteList.TopIndex = Math.Min(newIndex - 3, _autoCompleteList.Items.Count - 4);
        }

        private void CheckForDotInput()
        {
            int cursorPos = CodeEditor.SelectionStart;
            if (cursorPos > 0 && cursorPos <= CodeEditor.TextLength)
            {
                char previousChar = CodeEditor.Text[cursorPos - 1];
                if (previousChar == '.')
                {
                    _currentObjectName = GetObjectNameBeforeDot(cursorPos - 1);
                    _isAfterDot = true;
                    _autoCompleteTimer.Stop();
                    _autoCompleteTimer.Start();
                }
            }
        }

        private string GetObjectNameBeforeDot(int dotPosition)
        {
            int startPos = dotPosition - 1;
            while (startPos >= 0)
            {
                char c = CodeEditor.Text[startPos];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    break;
                startPos--;
            }
            startPos++;

            if (startPos >= dotPosition)
                return "";

            return CodeEditor.Text.Substring(startPos, dotPosition - startPos);
        }

        private void UpdateLineNumbers()
        {
            if (CodeEditor.IsDisposed || _lineNumberBox.IsDisposed) return;

            try
            {
                _isProgrammaticChange = true;
                SendMessage(_lineNumberBox.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);

                int lineCount = CodeEditor.Lines.Length;
                _lineNumberBox.Text = string.Join(Environment.NewLine,
                    Enumerable.Range(1, lineCount).Select(i => i.ToString()));

                int maxLineDigits = lineCount.ToString().Length;
                _lineNumberPanel.Width = 20 + (maxLineDigits * 10);

                _lineNumberBox.SelectionAlignment = HorizontalAlignment.Right;

                SyncLineNumberScroll();
                HighlightCurrentLine();
            }
            finally
            {
                SendMessage(_lineNumberBox.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                _lineNumberBox.Invalidate();
                _isProgrammaticChange = false;
            }
        }

        private void SyncLineNumberScroll()
        {
            if (CodeEditor.IsDisposed || _lineNumberBox.IsDisposed) return;

            int scrollPos = GetScrollPos(CodeEditor.Handle, SB_VERT);
            SetScrollPos(_lineNumberBox.Handle, SB_VERT, scrollPos, true);
            SendMessage(_lineNumberBox.Handle, WM_VSCROLL, (IntPtr)(scrollPos + 0x10000), IntPtr.Zero);
        }

        private void HighlightCurrentLine()
        {
            if (CodeEditor.IsDisposed || _lineNumberBox.IsDisposed) return;

            try
            {
                _isProgrammaticChange = true;
                int currentLine = CodeEditor.GetLineFromCharIndex(CodeEditor.SelectionStart) + 1;
                int totalLines = CodeEditor.Lines.Length;

                if (currentLine < 1 || currentLine > totalLines) return;

                _lineNumberBox.SelectAll();
                _lineNumberBox.SelectionBackColor = _lineNumberBox.BackColor;
                _lineNumberBox.SelectionColor = Color.DarkBlue;

                int lineStart = _lineNumberBox.GetFirstCharIndexFromLine(currentLine - 1);
                int lineLength = _lineNumberBox.Lines[currentLine - 1].Length;
                _lineNumberBox.Select(lineStart, lineLength);
                _lineNumberBox.SelectionBackColor = Color.DodgerBlue;
                _lineNumberBox.SelectionColor = Color.White;

                _lineNumberBox.Select(0, 0);
            }
            finally
            {
                _isProgrammaticChange = false;
            }
        }
        #endregion

        #region 端口增删（Form1）
        private void InitPortTypeMenu()
        {
            _portTypeMenu = new ContextMenuStrip();
            _portTypeMenu.Items.Add("Single (double)", null, (s, e) => AddPort(TypeName.SINGAL));
            _portTypeMenu.Items.Add("String", null, (s, e) => AddPort(TypeName.STRING));
            _portTypeMenu.Items.Add("Bool", null, (s, e) => AddPort(TypeName.BOOL));
            _portTypeMenu.Items.Add("Image", null, (s, e) => AddPort(TypeName.IMAGE));
            _portTypeMenu.Items.Add("Region", null, (s, e) => AddPort(TypeName.REGION));
            _portTypeMenu.Items.Add("Line (XLD)", null, (s, e) => AddPort(TypeName.LINE));
        }

        private void AddPort(TypeName type)
        {
            try
            {
                _host.AddPort(_addingInput, type);
                StatusLabel.Text = $"已添加{(_addingInput ? "输入" : "输出")}端口（{type}）";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加端口失败：{ex.Message}");
            }
        }

        private void ShowRemovePortMenu()
        {
            var menu = new ContextMenuStrip();
            var inputs = _host.GetPorts(true).ToList();
            var outputs = _host.GetPorts(false).ToList();
            if (inputs.Count == 0 && outputs.Count == 0)
            {
                StatusLabel.Text = "当前无端口可删除";
                return;
            }
            foreach (var p in inputs)
            {
                var name = p.PortName;
                var mi = menu.Items.Add($"输入: {name} ({p.PortType})");
                mi.Tag = (name, true);
                mi.Click += (s, e) => RemovePortFromHost((ValueTuple<string, bool>)((ToolStripItem)s).Tag);
            }
            foreach (var p in outputs)
            {
                var name = p.PortName;
                var mi = menu.Items.Add($"输出: {name} ({p.PortType})");
                mi.Tag = (name, false);
                mi.Click += (s, e) => RemovePortFromHost((ValueTuple<string, bool>)((ToolStripItem)s).Tag);
            }
            menu.Show(Cursor.Position);
        }

        private void RemovePortFromHost((string name, bool isInput) tag)
        {
            try
            {
                _host.RemovePort(tag.name, tag.isInput);
                StatusLabel.Text = $"已删除端口 {tag.name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除端口失败：{ex.Message}");
            }
        }
        #endregion

        #region 外部脚本运行功能
        public void RunCurrentScriptExternally_Click()
        {
            if (string.IsNullOrWhiteSpace(CodeEditor.Text))
            {
                MessageBox.Show("脚本内容为空，无法执行", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                OutputBox.Clear();

                File.WriteAllText(_tempScriptPath, CodeEditor.Text);
                OutputBox.AppendText($"已将当前脚本保存到临时文件:\n{_tempScriptPath}\n\n");
                StatusLabel.Text = "正在外部执行当前脚本...";

                var errors = new List<string>();
                bool compileSuccess = _host.Executor.CompileScript(CodeEditor.Text, out errors);
                if (!compileSuccess)
                {
                    OutputBox.AppendText("编译失败：\n" + string.Join("\n", errors));
                    StatusLabel.Text = "当前脚本外部执行失败: 编译错误";
                    return;
                }

                string result = _host.Executor.RunCompiledScript(_host.ScriptArgument);
                OutputBox.AppendText("外部执行结果:\n" + result);
                StatusLabel.Text = "当前脚本外部执行完成（已保存编译结果）";
                UpdateStatus();
            }
            catch (Exception ex)
            {
                OutputBox.AppendText($"执行错误:\n{ex.Message}");
                StatusLabel.Text = $"当前脚本外部执行失败: {ex.Message}";
            }
        }

        private void RunExternalScript_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog
            {
                Filter = "C#脚本|*.cs|所有文件|*.*",
                Title = "选择要运行的外部脚本"
            })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        StatusLabel.Text = $"正在运行外部脚本: {Path.GetFileName(ofd.FileName)}";
                        OutputBox.Clear();

                        string scriptContent = File.ReadAllText(ofd.FileName);
                        OutputBox.AppendText($"正在执行外部脚本: {ofd.FileName}\n\n");

                        var errors = new List<string>();
                        bool compileSuccess = _host.Executor.CompileScript(scriptContent, out errors);
                        if (!compileSuccess)
                        {
                            OutputBox.AppendText("编译失败：\n" + string.Join("\n", errors));
                            StatusLabel.Text = "外部脚本执行失败: 编译错误";
                            return;
                        }

                        string result = _host.Executor.RunCompiledScript(_host.ScriptArgument);
                        OutputBox.AppendText("执行结果:\n" + result);
                        StatusLabel.Text = $"外部脚本执行完成: {Path.GetFileName(ofd.FileName)}";
                    }
                    catch (Exception ex)
                    {
                        OutputBox.AppendText($"执行错误:\n{ex.Message}");
                        StatusLabel.Text = $"外部脚本执行失败: {ex.Message}";
                    }
                }
            }
        }

        private void CleanupTempFiles()
        {
            try
            {
                if (File.Exists(_tempScriptPath))
                {
                    File.Delete(_tempScriptPath);
                }
            }
            catch
            {
                // 忽略删除错误
            }
        }
        #endregion

        #region 脚本内容保存与恢复
        private void LoadSavedScript(string content)
        {
            try
            {
                _isProgrammaticChange = true;
                CodeEditor.Text = content;
                _lastCaretPosition = 0;
                SetCaretPosition(_lastCaretPosition);
                UpdateLineNumbers();
                ParseDynamicObjects();
            }
            finally
            {
                _isProgrammaticChange = false;
            }
        }
        #endregion

        #region 联想功能
        private void InitAutoCompleteList()
        {
            _autoCompleteList = new ListBox
            {
                Visible = false,
                Size = new Size(280, 180),
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = SelectionMode.One,
                Font = new Font("Consolas", 10f),
                TabStop = false,
                CausesValidation = false
            };

            SetDoubleBuffered(_autoCompleteList, true);

            _autoCompleteList.MouseClick += (s, e) =>
            {
                if (_autoCompleteList.SelectedItem != null)
                {
                    InsertSelectedAutoCompleteItem();
                }
            };

            _autoCompleteList.KeyDown += (s, e) =>
            {
                var keyCode = e.KeyCode;

                if (keyCode == Keys.Enter || keyCode == Keys.Tab)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    if (_autoCompleteList.SelectedItem != null)
                    {
                        InsertSelectedAutoCompleteItem();
                    }
                }
                else if (keyCode == Keys.Escape)
                {
                    HideAutoCompleteList();
                    CodeEditor.Focus();
                }
                else if (!(keyCode == Keys.Up || keyCode == Keys.Down ||
                          keyCode == Keys.PageUp || keyCode == Keys.PageDown))
                {
                    var tempKey = keyCode.ToString();
                    HideAutoCompleteList(true);

                    BeginInvoke(new Action(() => {
                        SendKeys.Send(tempKey);
                        CodeEditor.Focus();
                    }));
                }
            };

            _autoCompleteList.LostFocus += (s, e) =>
            {
                if (!CodeEditor.Focused && !_autoCompleteList.Focused)
                {
                    BeginInvoke(new Action(() => {
                        if (!CodeEditor.Focused && !_autoCompleteList.Focused)
                        {
                            HideAutoCompleteList();
                        }
                    }), 100);
                }
            };

            Controls.Add(_autoCompleteList);
            _autoCompleteList.BringToFront();
        }

        private void InsertSelectedAutoCompleteItem()
        {
            if (_autoCompleteList.SelectedItem == null)
            {
                HideAutoCompleteList();
                return;
            }

            string selectedText = _autoCompleteList.SelectedItem.ToString();
            int parenIndex = selectedText.IndexOf('(');
            if (parenIndex > 0)
            {
                selectedText = selectedText.Substring(0, parenIndex);
            }
            // 去掉成员信息后缀（如 " [属性]"、"（ToolBlock）"）
            int bracket = selectedText.IndexOf(" [");
            if (bracket > 0) selectedText = selectedText.Substring(0, bracket);

            try
            {
                _isProgrammaticChange = true;

                int replaceLength = _isAfterDot ? 0 : _currentInputPrefix.Length;
                int startPos = CodeEditor.SelectionStart - replaceLength;

                if (startPos >= 0)
                {
                    CodeEditor.Select(startPos, replaceLength);
                    CodeEditor.SelectedText = selectedText;
                    _lastCaretPosition = startPos + selectedText.Length;
                    SetCaretPosition(_lastCaretPosition);
                    UpdateLineNumbers();
                }
            }
            finally
            {
                _isProgrammaticChange = false;
            }

            HideAutoCompleteList();
        }

        private void AutoCompleteTimer_Tick(object sender, EventArgs e)
        {
            _autoCompleteTimer.Stop();

            if (_isSpecialCharHide)
            {
                _isSpecialCharHide = false;
                return;
            }

            if (!CodeEditor.Focused && !_autoCompleteList.Focused)
            {
                HideAutoCompleteList();
                return;
            }

            int caretDiff = Math.Abs(CodeEditor.SelectionStart - _lastCaretPosition);
            if (caretDiff > 10)
            {
                _lastCaretPosition = CodeEditor.SelectionStart;
                _isAfterDot = false;
            }

            if (_isAfterDot && !string.IsNullOrEmpty(_currentObjectName))
            {
                ShowObjectMembers(_currentObjectName);
                return;
            }

            _currentInputPrefix = GetCurrentInputPrefix();

            if (string.IsNullOrEmpty(_currentInputPrefix) || _currentInputPrefix.Length < MinPrefixLength)
            {
                HideAutoCompleteList();
                return;
            }

            // 1. 匹配动态创建的对象
            var matchedDynamicObjects = _dynamicObjectNames
                .Where(obj => obj.StartsWith(_currentInputPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 2. 匹配已创建的对象（端口名/兄弟工具名）
            var createdObjects = GetCreatedObjectNames();
            var matchedCreatedObjects = createdObjects
                .Where(obj => obj.StartsWith(_currentInputPrefix, StringComparison.OrdinalIgnoreCase)
                           && !matchedDynamicObjects.Contains(obj))
                .ToList();

            // 3. 匹配关键字和类型名
            var matchedKeywords = _autoCompleteItems
                .Except(matchedDynamicObjects)
                .Except(matchedCreatedObjects)
                .Where(item => item.StartsWith(_currentInputPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 合并结果
            var filteredItems = matchedDynamicObjects
                .Concat(matchedCreatedObjects)
                .Concat(matchedKeywords)
                .ToList();

            if (filteredItems.Count > 0)
            {
                ShowAutoCompleteList(filteredItems);
            }
            else if (_autoCompleteList.Visible)
            {
                HideAutoCompleteList();
            }
        }

        private void ShowObjectMembers(string objectName)
        {
            Type objectType = GetObjectType(objectName);
            if (objectType == null)
            {
                _isAfterDot = false;
                return;
            }

            List<string> members = GetTypeMembers(objectType);
            if (members.Count == 0)
            {
                _isAfterDot = false;
                return;
            }

            ShowAutoCompleteList(members);
        }

        private Type GetObjectType(string objectName)
        {
            // 1. 检查动态创建的对象
            if (_dynamicObjectCache.TryGetValue(objectName, out string typeName))
            {
                if (_typeCache.TryGetValue(typeName, out Type type))
                {
                    return type;
                }

                var assembly = Assembly.GetExecutingAssembly();
                type = assembly.GetType(typeName) ?? assembly.GetType($"HToolBase.{typeName}")
                       ?? assembly.GetType($"HToolBase.Tools.{typeName}");
                if (type != null)
                {
                    AddTypeToCache(typeName, type);
                    return type;
                }
            }

            // 2. 检查宿主已创建对象（Form2=兄弟工具；Form1=端口名）
            //    Form2：若宿主实参为 ToolBlock，按名取兄弟工具的实际类型
            if (_host.ScriptArgument is ToolBlock tb && tb.Tools.TryGetValue(objectName, out var siblingTool))
            {
                return siblingTool.GetType();
            }

            // 3. 检查类型缓存
            if (_typeCache.TryGetValue(objectName, out Type cachedType))
            {
                return cachedType;
            }

            // 4. 检查特殊对象
            if (objectName == "tool" || objectName == "self")
            {
                return _host.ScriptArgument.GetType();
            }
            if (objectName == "this")
            {
                return this.GetType();
            }

            return null;
        }

        private List<string> GetTypeMembers(Type type)
        {
            if (_typeMembersCache.TryGetValue(type, out List<string> cachedMembers))
            {
                return new List<string>(cachedMembers);
            }

            var allMembers = new HashSet<string>();
            var currentType = type;

            // 递归遍历所有父类
            while (currentType != null && currentType != typeof(object))
            {
                var properties = currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                                            .Where(p => !p.IsSpecialName);

                foreach (var prop in properties)
                {
                    string propInfo = $"{prop.Name} [属性]{(currentType != type ? $"（{currentType.Name}）" : "")}";
                    allMembers.Add(propInfo);
                }

                var methods = currentType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                                         .Where(m => !m.IsSpecialName
                                                   && m.DeclaringType != typeof(object)
                                                   && !m.Name.StartsWith("get_")
                                                   && !m.Name.StartsWith("set_"));

                foreach (var method in methods)
                {
                    string parameters = string.Join(", ", method.GetParameters()
                        .Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    string methodInfo = $"{method.Name}({parameters}) [方法]{(currentType != type ? $"（{currentType.Name}）" : "")}";
                    allMembers.Add(methodInfo);
                }

                currentType = currentType.BaseType;
            }

            var sortedMembers = allMembers.OrderBy(m => m.Contains("[属性]") ? 0 : 1)
                                          .ThenBy(m => m)
                                          .ToList();

            _typeMembersCache[type] = sortedMembers;

            return sortedMembers;
        }

        private void ShowAutoCompleteList(List<string> items)
        {
            if (_autoCompleteList.Visible &&
                _autoCompleteList.Items.Count == items.Count)
            {
                bool isSame = true;
                for (int i = 0; i < items.Count; i++)
                {
                    if (_autoCompleteList.Items[i].ToString() != items[i])
                    {
                        isSame = false;
                        break;
                    }
                }
                if (isSame)
                {
                    _autoCompleteList.Visible = true;
                    return;
                }
            }

            _lastCaretPosition = CodeEditor.SelectionStart;

            SendMessage(_autoCompleteList.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);

            _autoCompleteList.Items.Clear();
            _autoCompleteList.Items.AddRange(items.ToArray());
            _autoCompleteList.SelectedIndex = 0;

            int cursorPos = CodeEditor.SelectionStart;
            Point cursorPoint = CodeEditor.GetPositionFromCharIndex(cursorPos);
            cursorPoint.Y += CodeEditor.Font.Height + 5;
            Point screenPoint = CodeEditor.PointToScreen(cursorPoint);
            Point newLocation = PointToClient(screenPoint);

            if (_autoCompleteList.Location != newLocation)
            {
                _autoCompleteList.Location = newLocation;
            }

            if (_autoCompleteList.Right > ClientSize.Width)
                _autoCompleteList.Left = Math.Max(0, ClientSize.Width - _autoCompleteList.Width - 10);
            if (_autoCompleteList.Bottom > ClientSize.Height)
                _autoCompleteList.Top = Math.Max(0, ClientSize.Height - _autoCompleteList.Height - 10);

            _autoCompleteList.Visible = true;
            _isAutoCompleting = true;
            _autoCompleteList.BringToFront();

            if (!_autoCompleteList.Focused)
            {
                _autoCompleteList.Focus();
            }

            SetCaretPosition(_lastCaretPosition);

            SendMessage(_autoCompleteList.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            _autoCompleteList.Invalidate();
        }

        private void HideAutoCompleteList(bool isSpecialChar = false)
        {
            if (!_autoCompleteList.Visible) return;

            SendMessage(_autoCompleteList.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);

            _autoCompleteList.Visible = false;
            _isAutoCompleting = false;
            _isSpecialCharHide = isSpecialChar;
            _currentInputPrefix = string.Empty;
            _isAfterDot = false;

            SetCaretPosition(_lastCaretPosition);

            if (!CodeEditor.Focused)
            {
                BeginInvoke(new Action(() => {
                    if (!_autoCompleteList.Visible)
                    {
                        CodeEditor.Focus();
                    }
                }), 50);
            }

            SendMessage(_autoCompleteList.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            _autoCompleteList.Invalidate();
        }

        private string GetCurrentInputPrefix()
        {
            int cursorPos = CodeEditor.SelectionStart;
            if (cursorPos <= 0 || cursorPos > CodeEditor.TextLength)
                return "";

            int startPos = cursorPos - 1;
            while (startPos >= 0)
            {
                char c = CodeEditor.Text[startPos];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                    break;
                startPos--;
            }
            startPos++;

            if (startPos >= CodeEditor.TextLength)
                return "";

            int length = cursorPos - startPos;
            if (length <= 0)
                return "";

            if (startPos + length > CodeEditor.TextLength)
                length = CodeEditor.TextLength - startPos;

            return CodeEditor.Text.Substring(startPos, length);
        }

        private void SetDoubleBuffered(Control control, bool enable)
        {
            var prop = control.GetType().GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(control, enable, null);
        }

        /// <summary>收集当前已创建的对象名称（Form2=兄弟工具名；Form1=端口名）</summary>
        private List<string> GetCreatedObjectNames()
        {
            return _host.GetCreatedNames().ToList();
        }

        /// <summary>解析脚本中通过new创建的对象</summary>
        private void ParseDynamicObjects()
        {
            if (string.IsNullOrWhiteSpace(CodeEditor.Text))
            {
                _dynamicObjectCache.Clear();
                _dynamicObjectNames.Clear();
                return;
            }

            var newCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var newNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = CodeEditor.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            var pattern = @"(?<type>\w+)\s+(?<var>\w+)\s*=\s*new\s+(?<type2>\w+)\s*\(.*?\)";
            var varPattern = @"var\s+(?<var>\w+)\s*=\s*new\s+(?<type>\w+)\s*\(.*?\)";

            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("//"))
                    continue;

                var match = System.Text.RegularExpressions.Regex.Match(line, pattern);
                if (match.Success && match.Groups["type"].Value == match.Groups["type2"].Value)
                {
                    string varName = match.Groups["var"].Value;
                    string typeName = match.Groups["type"].Value;
                    newCache[varName] = typeName;
                    newNames.Add(varName);
                    continue;
                }

                match = System.Text.RegularExpressions.Regex.Match(line, varPattern);
                if (match.Success)
                {
                    string varName = match.Groups["var"].Value;
                    string typeName = match.Groups["type"].Value;
                    newCache[varName] = typeName;
                    newNames.Add(varName);
                }
            }

            if (!_dynamicObjectCache.SequenceEqual(newCache) || !_dynamicObjectNames.SetEquals(newNames))
            {
                _dynamicObjectCache = newCache;
                _dynamicObjectNames = newNames;
            }
        }
        #endregion


        #region 高亮功能
        private void HighlightTimer_Tick(object sender, EventArgs e)
        {
            _highlightTimer.Stop();
            if (CodeEditor == null || CodeEditor.IsDisposed || _isHighlighting || _isProgrammaticChange)
                return;

            _isHighlighting = true;
            try
            {
                int currentCaret = CodeEditor.SelectionStart;
                HighlightSyntax(CodeEditor);
                SetCaretPosition(currentCaret);
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"高光错误: {ex.Message}";
            }
            finally
            {
                _isHighlighting = false;
            }
        }

        private void HighlightSyntax(RichTextBox editor)
        {
            int cursorPos = editor.SelectionStart;
            int selectionLength = editor.SelectionLength;
            int firstVisibleLine = GetFirstVisibleLine(editor);

            try
            {
                _isProgrammaticChange = true;
                SendMessage(editor.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);

                editor.SelectAll();
                editor.SelectionColor = Color.Black;

                foreach (var keyword in _keywordColors.Keys)
                {
                    int index = 0;
                    while (index < editor.TextLength)
                    {
                        index = editor.Find(keyword, index, editor.TextLength,
                            RichTextBoxFinds.WholeWord | RichTextBoxFinds.MatchCase);
                        if (index == -1) break;

                        if (!(index <= cursorPos && cursorPos < index + keyword.Length))
                        {
                            editor.Select(index, keyword.Length);
                            editor.SelectionColor = _keywordColors[keyword];
                        }
                        index += keyword.Length;
                    }
                }
            }
            finally
            {
                editor.Select(cursorPos, selectionLength);
                SendMessage(editor.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                editor.Invalidate();
                ScrollToLine(editor, firstVisibleLine);
                _isProgrammaticChange = false;
            }
        }
        #endregion


        #region 光标位置管理
        private void SetCaretPosition(int position)
        {
            if (CodeEditor.IsDisposed || _isProgrammaticChange) return;

            try
            {
                _isProgrammaticChange = true;

                int validPosition = Math.Min(position, CodeEditor.TextLength);
                validPosition = Math.Max(0, validPosition);

                if (CodeEditor.SelectionStart != validPosition)
                {
                    CodeEditor.SelectionStart = validPosition;
                    CodeEditor.SelectionLength = 0;
                }

                CodeEditor.Focus();
                CodeEditor.ScrollToCaret();
            }
            finally
            {
                _isProgrammaticChange = false;
            }
        }
        #endregion


        #region 滚动控制
        private void ScrollIfCaretOutOfView(RichTextBox editor)
        {
            if (editor.IsDisposed || !editor.IsHandleCreated) return;

            int cursorPos = editor.SelectionStart;
            Point caretPoint = editor.GetPositionFromCharIndex(cursorPos);
            int visibleTop = editor.ClientRectangle.Top + 10;
            int visibleBottom = editor.ClientRectangle.Bottom - 10 - editor.Font.Height;

            if (caretPoint.Y < visibleTop || caretPoint.Y > visibleBottom)
            {
                editor.ScrollToCaret();
            }
        }

        private int GetFirstVisibleLine(RichTextBox editor)
        {
            return editor.GetLineFromCharIndex(editor.GetCharIndexFromPosition(new Point(0, 10)));
        }

        private void ScrollToLine(RichTextBox editor, int lineNumber)
        {
            if (lineNumber >= 0 && lineNumber < editor.Lines.Length)
            {
                editor.Select(editor.GetFirstCharIndexFromLine(lineNumber), 0);
                ScrollIfCaretOutOfView(editor);
            }
        }
        #endregion


        #region 其他功能
        private void LoadExampleScript()
        {
            try
            {
                _isProgrammaticChange = true;
                CodeEditor.Text = _host.GetExampleScript();
                _lastCaretPosition = 0;
                SetCaretPosition(_lastCaretPosition);
                UpdateLineNumbers();
                ParseDynamicObjects();
            }
            finally
            {
                _isProgrammaticChange = false;
            }
        }

        private void ExecuteScript_Click(object sender, EventArgs e)
        {
            _lastCaretPosition = CodeEditor.SelectionStart;

            OutputBox.Clear();
            StatusLabel.Text = "正在执行脚本...";
            try
            {
                string scriptContent = CodeEditor.Text;

                var errors = new List<string>();
                bool compileSuccess = _host.Executor.CompileScript(scriptContent, out errors);

                if (!compileSuccess)
                {
                    OutputBox.AppendText("编译错误:\n");
                    foreach (string err in errors)
                        OutputBox.AppendText(err + "\n");
                    StatusLabel.Text = "脚本编译失败";
                    return;
                }

                string runResult = _host.Executor.RunCompiledScript(_host.ScriptArgument);
                OutputBox.AppendText("脚本执行结果:\n" + runResult);
                StatusLabel.Text = "脚本执行成功（已保存编译结果）";
                UpdateStatus();
            }
            catch (Exception ex)
            {
                OutputBox.AppendText("执行异常:\n" + ex.Message);
                StatusLabel.Text = "脚本执行异常";
            }
            finally
            {
                SetCaretPosition(_lastCaretPosition);
            }
        }

        private void SaveScript()
        {
            _lastCaretPosition = CodeEditor.SelectionStart;

            using (var sfd = new SaveFileDialog { Filter = "C#脚本|*.cs" })
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(sfd.FileName, CodeEditor.Text);
                    StatusLabel.Text = $"已保存至 {sfd.FileName}";
                    _host.ScriptText = CodeEditor.Text;
                }

            SetCaretPosition(_lastCaretPosition);
        }

        private void OpenScript()
        {
            try
            {
                _isProgrammaticChange = true;
                using (var ofd = new OpenFileDialog { Filter = "C#脚本|*.cs" })
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        CodeEditor.Text = File.ReadAllText(ofd.FileName);
                        StatusLabel.Text = $"已打开 {ofd.FileName}";
                        _host.ScriptText = CodeEditor.Text;
                        _lastCaretPosition = 0;
                        UpdateLineNumbers();
                        ParseDynamicObjects();
                    }
                SetCaretPosition(_lastCaretPosition);
            }
            finally
            {
                _isProgrammaticChange = false;
            }
        }

        #endregion

    }
}
