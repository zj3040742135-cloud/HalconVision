using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HDesigner;

namespace Hal
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// 使用ApplicationContext托管主窗口：界面设计器生成运行窗口时可切换MainForm
        /// （关闭Form1程序不退出），运行窗口内可切回Form1。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ApplicationContext ctx = new ApplicationContext(new Form1());
            AppSession.Context = ctx;
            AppSession.CreateMainForm = () => new Form1();
            Application.Run(ctx);
        }
    }
}
