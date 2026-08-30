
using HToolBase;
using HToolBase.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace Hal
{
    public partial class Form1 : Form
    {
        //public static Dictionary<string, ProcessPanel> Processes = new Dictionary<string, ProcessPanel>();
        //public static int ProcessIndex = -1;
        private System.Windows.Forms.ToolTip toolTip = new System.Windows.Forms.ToolTip();
        public System.Windows.Forms.TabControl GetTabControl2() => tabControl2;
        public System.Windows.Forms.TabControl GetTabControl3() => tabControl3;
        private readonly object _halconLock = new object();

        public Form1()
        {
            InitializeComponent();

            // 1. 加载产品列表（System/Products.json）
            ProductManager.LoadProducts();
            // 2. 恢复上次退出时的当前产品
            ProductManager.LoadLastProduct();

            // 3. 启动时自动加载当前产品下所有流程（内部会先释放旧流程再加载）
            ProjectManager.LoadProject(this);

            // 4. 若加载不到任何流程，创建一个默认 Debug 流程
            if (ProcessManager.instance(). Processes.Count == 0)
                CreateDefaultProcess("Debug");

            // 5. 默认选中第一个流程，同步当前流程指针
            if (tabControl2.TabPages.Count > 0)
            {
                tabControl2.SelectedIndex = 0;
                ProcessManager.instance().ProcessIndex = 0;
                SetCurrentProcess(ProcessManager.instance().Processes.Values.FirstOrDefault());
            }

            tabControl2.SelectedIndexChanged += tabControl2_SelectedIndexChanged;
            // 6. 关闭时自动保存当前产品
            this.FormClosing += Form1_FormClosing;
            UpdateTitle();
        }

        /// <summary>创建一个默认流程并加入tabControl2，同时登记到Processes字典</summary>
        private void CreateDefaultProcess(string name)
        {
            var panel = new ProcessPanel { PanelName = name, Dock = DockStyle.Fill };
            var page = new TabPage { Text = name };
            page.Controls.Add(panel);
            tabControl2.TabPages.Add(page);
            ProcessManager.instance().Processes[name] = panel;
        }

        /// <summary>
        /// 设置当前活动流程，并同步 ProductManager.CurrentProcessPanel，
        /// 确保 ToolBlock.SaveTools/LoadVpp 构建的路径与当前流程一致。
        /// </summary>
        private static void SetCurrentProcess(ProcessPanel panel)
        {
            ProcessManager.instance().currentProcess = panel;
            if (panel != null)
                ProductManager.CurrentProcessPanel = panel.PanelName;
        }

        /// <summary>更新窗口标题，显示当前产品名</summary>
        private void UpdateTitle()
        {
            this.Text = $"视觉系统 - 当前产品: {ProductManager.CurrentProduct}";
        }

        private void tabControl2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl2.SelectedIndex < 0) return;
            ProcessManager.instance().ProcessIndex = tabControl2.SelectedIndex;
            string name = tabControl2.SelectedTab?.Text;
            if (!string.IsNullOrEmpty(name) && ProcessManager.instance().Processes.TryGetValue(name, out var panel))
                SetCurrentProcess(panel);
        }

        /// <summary>关闭时自动保存当前产品下所有流程</summary>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            ProjectManager.SaveProject(this);
        }

        /// <summary>保存按钮：保存当前产品下所有流程（ProcessPanel布局 + 连线 + ToolBlock配置）</summary>
        private void Save_Click(object sender, EventArgs e)
        {
            ProjectManager.SaveProject(this);
            MessageBox.Show("保存完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 产品管理按钮：打开产品管理窗体。
        /// 切换产品时：先保存当前产品 →（LoadProject内部）清空所有对象 → 加载新产品。
        /// </summary>
        private void ProjectManger_Click(object sender, EventArgs e)
        {
            string oldProduct = ProductManager.CurrentProduct;

            // 1. 切换前先保存当前产品（此时 CurrentProduct 仍是旧产品）
            ProjectManager.SaveProject(this);

            // 2. 打开产品管理窗体，用户可能切换产品或新增/克隆产品
            using (var form = new ProductManageForm())
            {
                form.ShowDialog(this);
            }

            // 3. 若产品已切换：清空当前所有对象后加载新产品（LoadProject 内部先释放旧流程）
            if (oldProduct != ProductManager.CurrentProduct)
            {
                ProjectManager.LoadProject(this);
                if (ProcessManager.instance().Processes.Count == 0)
                    CreateDefaultProcess("Debug");
                if (tabControl2.TabPages.Count > 0)
                {
                    tabControl2.SelectedIndex = 0;
                    ProcessManager.instance(). ProcessIndex = 0;
                    SetCurrentProcess(ProcessManager.instance().Processes.Values.FirstOrDefault());
                }
                UpdateTitle();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
        }

        private void Add_Click(object sender, EventArgs e)
        {
        }

        private void Run_Click_1(object sender, EventArgs e)
        {
            ProcessManager.instance().currentProcess?.Run();
        }
    }
}
