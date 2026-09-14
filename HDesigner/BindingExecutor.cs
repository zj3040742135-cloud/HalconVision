using System;
using System.Windows.Forms;

namespace HDesigner
{
    /// <summary>事件绑定执行器（运行窗口中生效，设计态不调用）：
    /// 绑定流程→运行ProcessManager中的流程；打开窗口→实例化并显示所选Form</summary>
    internal static class BindingExecutor
    {
        public static void Execute(EventBinding b)
        {
            if (b == null || string.IsNullOrEmpty(b.Target)) return;
            try
            {
                if (b.FunctionType == EventBinding.RunProcess)
                {
                    HToolBase.ProcessPanel panel;
                    if (HToolBase.ProcessManager.instance().Processes.TryGetValue(b.Target, out panel) && panel != null)
                        panel.Run();
                    else
                        MessageBox.Show($"未找到流程：{b.Target}", "事件绑定",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if (b.FunctionType == EventBinding.OpenWindow)
                {
                    Type t = DesignDocument.ResolveType(b.Target);
                    if (t != null && typeof(Form).IsAssignableFrom(t))
                        ((Form)Activator.CreateInstance(t)).Show();
                    else
                        MessageBox.Show($"未找到窗口类型：{b.Target}", "事件绑定",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"执行绑定失败：{ex.Message}", "事件绑定",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
