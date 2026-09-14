using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HalconDotNet;

namespace HDesigner
{
    /// <summary>由界面设计生成的运行窗口：控件事件绑定在此生效（设计态不生效）。
    /// 底部固定"切换为原Form1窗口"按钮，点击后经AppSession重建宿主主窗口
    /// 并关闭本窗口（ApplicationContext切换MainForm保证程序不退出）。
    /// HDisplayControl图像绑定：轮询绑定端口的HObject值，变化时更新显示</summary>
    public class RuntimeForm : Form
    {
        // 事件绑定表：控件 → 事件名 → 绑定配置
        private readonly Dictionary<Control, Dictionary<string, EventBinding>> _bindings =
            new Dictionary<Control, Dictionary<string, EventBinding>>();

        // HDisplayControl图像绑定运行项
        private class ImageBindItem
        {
            public HToolBase.Controls.HDisplayControl Display;
            public string ProcessName, ModuleText, PortName;
            public HObject LastImage; // 上次显示的图像引用（引用变化即认为图像更新）
        }

        private readonly List<ImageBindItem> _imageBinds = new List<ImageBindItem>();
        private Timer _imageTimer; // 图像轮询定时器（UI线程）

        public RuntimeForm(DesignDocument doc)
        {
            Text = "运行界面";
            
            // 底部切换条占44px，内容区高度与设计画布一致
            ClientSize = new Size(Math.Max(400, doc.CanvasWidth), Math.Max(300, doc.CanvasHeight + 44));
            MinimumSize = new Size(400, 300);
            StartPosition = FormStartPosition.CenterScreen;

            Panel content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            // 先加content后加bottom条：停靠按添加逆序处理，Bottom条先占用底部，Fill取剩余区域
            Controls.Add(content);
            Controls.Add(CreateBottomBar());

            foreach (ControlData data in doc.Controls)
                DesignDocument.Populate(data, content, OnRuntimeControlCreated);

            // 存在图像绑定时启动轮询（200ms，流程运行后端口值更新即刷新显示）
            if (_imageBinds.Count > 0)
            {
                _imageTimer = new Timer { Interval = 200 };
                _imageTimer.Tick += ImageTimer_Tick;
                _imageTimer.Start();
            }
        }

        /// <summary>底部固定切换条：按钮垂直居中、水平居中（随窗口缩放保持位置）</summary>
        private Control CreateBottomBar()
        {
            Panel bar = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Color.FromArgb(243, 243, 243) };
            Button btn = new Button { Text = "切换为原Form1窗口", Size = new Size(160, 28) };
            btn.Click += (s, e) =>
            {
                if (AppSession.CreateMainForm == null)
                {
                    MessageBox.Show("宿主未注册主窗口工厂，无法切换回Form1", "切换窗口",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Form main = AppSession.CreateMainForm();
                AppSession.SwitchMain(main);
            };
            bar.Controls.Add(btn);
            Action center = () =>
            {
                btn.Left = (bar.Width - btn.Width) / 2;
                btn.Top = (bar.Height - btn.Height) / 2;
            };
            bar.Resize += (s, e) => center();
            bar.HandleCreated += (s, e) => center();
            return bar;
        }

        /// <summary>运行控件构建回调：恢复绑定表并订阅按钮事件（运行窗口中绑定生效）</summary>
        private void OnRuntimeControlCreated(Control c, ControlData data)
        {
            if (data.Bindings.Count > 0)
            {
                Dictionary<string, EventBinding> map = new Dictionary<string, EventBinding>();
                foreach (BindingData bd in data.Bindings)
                    map[bd.EventName] = new EventBinding { FunctionType = bd.FunctionType, Target = bd.Target };
                _bindings[c] = map;
            }
            Button b = c as Button;
            if (b != null)
            {
                b.MouseClick += RuntimeButton_MouseClick;
                b.DoubleClick += RuntimeButton_DoubleClick;
                return;
            }
            // HDisplayControl图像绑定："流程名|模块Text|端口名"
            HToolBase.Controls.HDisplayControl disp = c as HToolBase.Controls.HDisplayControl;
            if (disp != null)
            {
                Dictionary<string, EventBinding> map;
                EventBinding ib;
                if (_bindings.TryGetValue(c, out map) && map.TryGetValue("Image", out ib)
                    && ib.FunctionType == EventBinding.BindImage && !string.IsNullOrEmpty(ib.Target))
                {
                    string[] parts = ib.Target.Split('|');
                    if (parts.Length >= 3)
                    {
                        _imageBinds.Add(new ImageBindItem
                        {
                            Display = disp,
                            ProcessName = parts[0],
                            ModuleText = parts[1],
                            PortName = parts[2]
                        });
                    }
                }
            }
        }

        /// <summary>轮询绑定的图像输出端口：流程运行后端口Value更新为新HObject时刷新显示。
        /// 流程在Task线程写入Value，此处UI线程读引用（引用读写原子），安全</summary>
        private void ImageTimer_Tick(object sender, EventArgs e)
        {
            foreach (ImageBindItem item in _imageBinds)
            {
                try
                {
                    HObject img = ResolveBoundImage(item.ProcessName, item.ModuleText, item.PortName);
                    if (img == null || !img.IsInitialized()) continue;
                    if (ReferenceEquals(img, item.LastImage)) continue;
                    item.LastImage = img;
                    item.Display.ShowImage(img);
                }
                catch
                {
                    // 流程/模块被销毁或未初始化时跳过本轮，下轮重试
                }
            }
        }

        /// <summary>解析绑定路径取当前图像：流程→ToolModule(按Text)→ToolBlock输出端口Value</summary>
        private static HObject ResolveBoundImage(string processName, string moduleText, string portName)
        {
            try
            {
                HToolBase.ProcessPanel panel;
                if (!HToolBase.ProcessManager.instance().Processes.TryGetValue(processName, out panel) || panel == null)
                    return null;
                HToolBase.ToolModule tm = panel.modules.OfType<HToolBase.ToolModule>()
                    .FirstOrDefault(x => (x.Text ?? x.Name) == moduleText);
                if (tm == null) return null;
                HToolBase.PortNode port;
                if (!tm.GetToolBlock().Outputs.TryGetValue(portName, out port) || port == null)
                    return null;
                return port.Value as HObject;
            }
            catch
            {
                return null;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (_imageTimer != null)
            {
                _imageTimer.Stop();
                _imageTimer.Dispose();
                _imageTimer = null;
            }
        }

        private bool HasBinding(Control c, string evt)
        {
            Dictionary<string, EventBinding> map;
            EventBinding b;
            return c != null
                && _bindings.TryGetValue(c, out map)
                && map.TryGetValue(evt, out b)
                && b != null && !string.IsNullOrEmpty(b.Target);
        }

        // 双击的第二击会先触发MouseClick(Clicks=2)：配置了双击绑定时让位给DoubleClick
        private void RuntimeButton_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Clicks > 1 && HasBinding(sender as Control, "DoubleClick")) return;
            Execute(sender as Control, "MouseClick");
        }

        private void RuntimeButton_DoubleClick(object sender, EventArgs e)
        {
            Execute(sender as Control, "DoubleClick");
        }

        private void Execute(Control c, string evt)
        {
            if (!HasBinding(c, evt)) return;
            BindingExecutor.Execute(_bindings[c][evt]);
        }
    }
}
