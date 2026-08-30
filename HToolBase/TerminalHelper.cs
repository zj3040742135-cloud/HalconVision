using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HToolBase
{
    public static class TerminalHelper
    {
        public static List<TreeNode> GetAllToolBlockTer(ProcessPanel processPanel)
        {
            // 用List存储节点，完全独立，不依附任何TreeView，不会GC回收
            List<TreeNode> nodeList = new List<TreeNode>();

            foreach (HToolBase.Module module in processPanel.modules)
            {
                TreeNode RootNode = new TreeNode();
                RootNode.Text = module.Text;

                if (module is ToolModule tool)
                {
                    foreach (PortNode portNode in tool.GetToolBlock().Outputs.Values)
                    {
                        RootNode.Nodes.Add(portNode.Text);
                        Console.WriteLine(portNode.Text);
                    }
                    nodeList.Add(RootNode);
                }
            }
            return nodeList;
        }
    }
}
