using HAttribute;
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
    public partial class ToolTerminalForm : Form
    {
        private HToolBase.ToolBase ToolBase;
        PropertyTagModel Item;
        public ToolTerminalForm(HToolBase.ToolBase tool)
        {
            InitializeComponent();
            ToolBase=tool;
            List<TreeNode> nodeList = ToolBase.GetAllProperty();
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
            treeView1.NodeMouseClick += TreeView1_NodeMouseClick;
        }

        private void TreeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if(e.Node.Parent!=null)
            {
                Item= e.Node.Tag as PropertyTagModel;
                this.textBox1.Text = e.Node.Parent.Text + "." + e.Node.Text;
            }
            else
            {
                Item = null;
                this.textBox1.Text = null;
            }
        }

        private void Input_Click(object sender, EventArgs e)
        {
            ToolBase.AddInput(Item.PropertyName, TypeNameHelper.ToTypeName(Item.Category));
        }
        private void Output_Click(object sender, EventArgs e)
        {
            ToolBase.AddOutput(Item.PropertyName, TypeNameHelper.ToTypeName(Item.Category));
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
