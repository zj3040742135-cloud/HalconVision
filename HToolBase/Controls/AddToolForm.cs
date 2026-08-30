using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HToolBase.Tools;
namespace HToolBase.Controls
{
    public partial class AddToolForm : Form
    {
        ToolBlock ToolBlock;
        public AddToolForm(ToolBlock toolBlock)
        {
            InitializeComponent();
            ToolBlock=toolBlock;
            treeView1.DoubleClick += TreeView1_DoubleClick;
        }

        private void TreeView1_DoubleClick(object sender, EventArgs e)
        {
            string ToolName = this.treeView1.SelectedNode.Text;
            CreateInstanceByFullName("HToolBase.Tools."+ToolName);
        }
        public void CreateInstanceByFullName(string fullClassName, string assemblyName = null)
        {
            try
            {
                Assembly assembly = assemblyName == null ?
                     Assembly.GetExecutingAssembly() :
                     Assembly.Load(assemblyName);
                Type type = assembly.GetType(fullClassName);
                if (type == null)
                    throw new TypeLoadException("$未找到类型 { fullClassName }");
                if (!typeof(HToolBase.ToolBase).IsAssignableFrom(type))
                    throw new ArgumentException("$类型 { fullClassName } 不是 ProcessTool 的子类");

                ToolBase process;
                if (type == typeof(HToolBase.Tools.ToolBlock))
                {
                    process = (HToolBase.ToolBase)Activator.CreateInstance(type);
                }
                else
                {
                    // 工具构造函数需要 TreeViewContral 参数，且内部使用 Nodes.Insert(Nodes.Count - 1)
                    // 需要至少1个节点才能正常插入，这里用临时 TreeView，AddProcessTool 会将节点移到 ToolsTreeView
                    process = (HToolBase.ToolBase)Activator.CreateInstance(type);
                }

                if (process != null)
                {
                    ToolBlock.Tools.Add(fullClassName, process);
                    
                }
                else
                {
                    throw new InvalidOperationException("$无法实例化 { fullClassName } 为 ProcessTool");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("$实例化类型 { fullClassName } 失败 ", ex);
            }
        }
    }
}
