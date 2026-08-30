using System;
using System.Drawing;
using System.Windows.Forms;

namespace HToolBase.Controls
{
    /// <summary>
    /// 产品管理窗体：维护产品列表，新增/删除/选择当前产品。
    /// 新增产品时可选择"复制源"，从源产品克隆全部配置。
    /// </summary>
    public class ProductManageForm : Form
    {
        private const string NoClone = "(无)";

        private Label currentLabel;
        private ListBox productListBox;
        private Label cloneSourceLabel;
        private ComboBox cloneSourceCombo;
        private TextBox newNameTextBox;
        private Button addButton;
        private Button deleteButton;
        private Button setCurrentButton;
        private Button closeButton;

        public ProductManageForm()
        {
            this.Text = "产品管理";
            this.Size = new Size(320, 440);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            BuildUI();
            RefreshList();
        }

        private void BuildUI()
        {
            // 当前产品提示
            currentLabel = new Label
            {
                Text = "当前产品：" + ProductManager.CurrentProduct,
                Location = new Point(15, 15),
                Size = new Size(280, 22),
                ForeColor = Color.Blue
            };
            this.Controls.Add(currentLabel);

            // 产品列表
            productListBox = new ListBox
            {
                Location = new Point(15, 45),
                Size = new Size(270, 180)
            };
            this.Controls.Add(productListBox);

            // 复制源标签
            cloneSourceLabel = new Label
            {
                Text = "复制源（新增产品时克隆其配置）：",
                Location = new Point(15, 232),
                Size = new Size(280, 18)
            };
            this.Controls.Add(cloneSourceLabel);

            // 复制源下拉框
            cloneSourceCombo = new ComboBox
            {
                Location = new Point(15, 252),
                Size = new Size(270, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.Controls.Add(cloneSourceCombo);

            // 新名称输入
            newNameTextBox = new TextBox
            {
                Location = new Point(15, 284),
                Size = new Size(180, 24)
            };
            this.Controls.Add(newNameTextBox);

            addButton = new Button
            {
                Text = "添加",
                Location = new Point(205, 283),
                Size = new Size(80, 26)
            };
            addButton.Click += AddButton_Click;
            this.Controls.Add(addButton);

            // 删除 / 设为当前
            deleteButton = new Button
            {
                Text = "删除",
                Location = new Point(15, 320),
                Size = new Size(130, 30)
            };
            deleteButton.Click += DeleteButton_Click;
            this.Controls.Add(deleteButton);

            setCurrentButton = new Button
            {
                Text = "设为当前产品",
                Location = new Point(155, 320),
                Size = new Size(130, 30)
            };
            setCurrentButton.Click += SetCurrentButton_Click;
            this.Controls.Add(setCurrentButton);

            // 关闭
            closeButton = new Button
            {
                Text = "关闭",
                Location = new Point(15, 360),
                Size = new Size(270, 32),
                DialogResult = DialogResult.OK
            };
            this.Controls.Add(closeButton);

            this.AcceptButton = closeButton;
        }

        private void RefreshList()
        {
            // 刷新产品列表
            productListBox.Items.Clear();
            foreach (var p in ProductManager.Products)
                productListBox.Items.Add(p);
            currentLabel.Text = "当前产品：" + ProductManager.CurrentProduct;

            // 刷新复制源下拉框：首项为"(无)"，其余为所有产品
            string prevSelected = cloneSourceCombo.SelectedItem?.ToString();
            cloneSourceCombo.Items.Clear();
            cloneSourceCombo.Items.Add(NoClone);
            foreach (var p in ProductManager.Products)
                cloneSourceCombo.Items.Add(p);

            // 恢复之前的选择，默认选"(无)"
            int idx = string.IsNullOrEmpty(prevSelected) ? 0 : cloneSourceCombo.Items.IndexOf(prevSelected);
            cloneSourceCombo.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            string name = newNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入产品名称");
                return;
            }
            if (ProductManager.Products.Contains(name))
            {
                MessageBox.Show($"产品 \"{name}\" 已存在");
                return;
            }

            // 读取复制源："(无)"或空则不克隆
            string cloneFrom = cloneSourceCombo.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(cloneFrom) || cloneFrom == NoClone)
                cloneFrom = null;

            ProductManager.AddProduct(name, cloneFrom);
            newNameTextBox.Clear();
            RefreshList();
            productListBox.SelectedItem = name;
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (productListBox.SelectedItem == null)
            {
                MessageBox.Show("请先选择要删除的产品");
                return;
            }
            string name = productListBox.SelectedItem.ToString();
            if (name == ProductManager.CurrentProduct &&
                MessageBox.Show($"删除当前产品 \"{name}\"？", "确认", MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;
            ProductManager.RemoveProduct(name);
            RefreshList();
        }

        private void SetCurrentButton_Click(object sender, EventArgs e)
        {
            if (productListBox.SelectedItem == null)
            {
                MessageBox.Show("请先选择要设为当前的产品");
                return;
            }
            ProductManager.CurrentProduct = productListBox.SelectedItem.ToString();
            RefreshList();
        }
    }
}
