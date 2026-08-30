using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using HToolBase;

namespace Hal
{
    /// <summary>单个流程的配置项</summary>
    public class ProcessDataItem
    {
        public string ProcessName { get; set; }
        public string ProcessFolderName { get; set; }
    }

    /// <summary>项目配置（记录该产品下所有流程及最后激活的流程）</summary>
    [Serializable]
    public class ProjectData
    {
        public int LastActiveProcessIndex { get; set; }
        public DateTime LastSavedTime { get; set; }
        public System.Collections.Generic.List<ProcessDataItem> Processes { get; set; }
            = new System.Collections.Generic.List<ProcessDataItem>();
    }

    /// <summary>
    /// 项目管理器：按当前产品保存/加载所有ProcessPanel流程。
    /// 目录结构：System/{当前产品名}/Project.xml + System/{当前产品名}/{流程名}/...
    /// </summary>
    public static class ProjectManager
    {
        /// <summary>清理文件名中的非法字符</summary>
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        /// <summary>保存整个项目（当前产品下所有流程）</summary>
        public static void SaveProject(Form1 mainForm)
        {
            try
            {
                Directory.CreateDirectory(ProductManager.CurrentProductPath);

                var projectData = new ProjectData
                {
                    LastActiveProcessIndex = ProcessManager.instance().ProcessIndex,
                    LastSavedTime = DateTime.Now
                };

                foreach (var kvp in ProcessManager.instance().Processes)
                {
                    string processName = kvp.Key;
                    ProcessPanel panel = kvp.Value;
                    string folderName = SanitizeFileName(processName);
                    string folder = ProductManager.GetProcessFolder(folderName);
                    panel.PanelName = processName;
                    panel.SaveToFolder(folder);
                    projectData.Processes.Add(new ProcessDataItem
                    {
                        ProcessName = processName,
                        ProcessFolderName = folderName
                    });
                }

                XmlSerializer serializer = new XmlSerializer(typeof(ProjectData));
                using (StreamWriter writer = new StreamWriter(ProductManager.ProjectConfigPath))
                {
                    serializer.Serialize(writer, projectData);
                }

                // 记录当前产品名，供下次启动时自动恢复
                ProductManager.SaveLastProduct();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存项目失败: {ex.Message}");
            }
        }

        /// <summary>加载整个项目（当前产品下所有流程）</summary>
        public static void LoadProject(Form1 mainForm)
        {
            // 0. 先释放现有流程的所有资源（模块、ToolBlock、连线、事件订阅）
            foreach (var panel in ProcessManager.instance().Processes.Values)
            {
                if (panel == null) continue;
                try { panel.ClearAll(); panel.Dispose(); }
                catch { /* 释放失败不阻断加载 */ }
            }
            ProcessManager.instance().Processes.Clear();
            if (mainForm.GetTabControl2() != null)
                mainForm.GetTabControl2().TabPages.Clear();
            ProcessManager.instance().currentProcess = null;
            ProcessManager.instance().ProcessIndex = -1;

            if (!File.Exists(ProductManager.ProjectConfigPath)) return;

            ProjectData projectData;
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ProjectData));
                using (StreamReader reader = new StreamReader(ProductManager.ProjectConfigPath))
                {
                    projectData = (ProjectData)serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载项目失败: {ex.Message}");
                return;
            }
            if (projectData == null) return;

            // 2. 加载每个流程
            foreach (var item in projectData.Processes)
            {
                string folder = ProductManager.GetProcessFolder(item.ProcessFolderName);
                if (!Directory.Exists(folder)) continue;

                var panel = new ProcessPanel();
                panel.PanelName = item.ProcessName;
                panel.Dock = DockStyle.Fill;
                TabPage page = new TabPage { Text = item.ProcessName };
                page.Controls.Add(panel);
                mainForm.GetTabControl2().TabPages.Add(page);
                panel.LoadFromFolder(folder);
                ProcessManager.instance().Processes[item.ProcessName] = panel;
            }

            // 3. 恢复最后激活的流程
            if (mainForm.GetTabControl2() != null &&
                projectData.LastActiveProcessIndex >= 0 &&
                projectData.LastActiveProcessIndex < mainForm.GetTabControl2().TabPages.Count)
            {
                mainForm.GetTabControl2().SelectedIndex = projectData.LastActiveProcessIndex;
                ProcessManager.instance().ProcessIndex = projectData.LastActiveProcessIndex;
            }

            // 4. 设置当前流程
            if (ProcessManager.instance().Processes.Count > 0)
                ProcessManager.instance().currentProcess = ProcessManager.instance().Processes.Values.First();
        }
    }
}
