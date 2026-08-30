using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HToolBase.Controls
{
    public partial class TerminalForm : Form
    {
        public string str;
        public  TreeNode RootNode;
        public TreeNode Node;
        public TerminalForm(ProcessPanel processPanel)
        {
            InitializeComponent();
            List<TreeNode> nodeList = TerminalHelper.GetAllToolBlockTer(processPanel);

            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();

            foreach (TreeNode srcNode in nodeList)
            {
                // Clone深度复制节点+所有子节点，全新对象，无所有权冲突
                TreeNode newNode = srcNode.Clone() as TreeNode;
                treeView1.Nodes.Add(newNode);
            }
            treeView1.EndUpdate();
            treeView1.ExpandAll(); // 自动展开方便查看
            treeView1.MouseDoubleClick += TreeView1_MouseDoubleClick;
        }

        private void TreeView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (treeView1.SelectedNode.Parent != null)
            {
                str=treeView1.SelectedNode.FullPath;
                RootNode = treeView1.SelectedNode.Parent;
                Node =treeView1.SelectedNode;
                Console.WriteLine(str);
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                str=String.Empty;
                RootNode = null;
                Node = null;
                this.DialogResult= DialogResult.Cancel;
            }
        }
    }
}
