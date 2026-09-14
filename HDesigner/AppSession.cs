using System;
using System.Windows.Forms;

namespace HDesigner
{
    /// <summary>宿主应用会话：ApplicationContext持有者与主窗口切换。
    /// 宿主Program启动时注册Context与主窗口工厂（HDesigner不引用宿主程序集，
    /// 通过回调创建Form1，避免循环依赖）。切换主窗口后ApplicationContext保证
    /// 消息循环随新主窗口继续运行。</summary>
    public static class AppSession
    {
        public static ApplicationContext Context { get; set; }

        /// <summary>宿主主窗口（Form1）工厂</summary>
        public static Func<Form> CreateMainForm { get; set; }

        /// <summary>切换主窗口：显示newMain并关闭旧主窗口（旧主窗口上的Owned窗体一并关闭）</summary>
        public static void SwitchMain(Form newMain)
        {
            if (Context == null)
                throw new InvalidOperationException("宿主未初始化ApplicationContext，无法切换主窗口");
            Form old = Context.MainForm;
            Context.MainForm = newMain;
            newMain.Show();
            if (old != null && !old.IsDisposed && !ReferenceEquals(old, newMain))
                old.Close();
        }
    }
}
