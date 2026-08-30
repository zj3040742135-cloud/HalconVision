using System.Windows.Forms;

namespace HToolBase.Controls
{
    partial class ScriptToolForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            this.Text = "ScriptToolForm";
        }

        #endregion

        #region 控件引用（UI 在 ScriptToolForm.InitEditorWithLineNumbers 中代码构建）
        public RichTextBox CodeEditor { get; private set; }
        public RichTextBox OutputBox { get; private set; }
        public ToolStripStatusLabel StatusLabel { get; private set; }
        #endregion
    }
}
