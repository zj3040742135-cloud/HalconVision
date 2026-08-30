using HalconDotNet;
using HAttribute;
using HToolBase.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace HToolBase.Tools
{
    public class BlobTool : ToolBase
    {
        #region//工具属性
        [FieldInfoTagAttribute("InputImage", typeof(HObject), "Input")]
        public HObject InputImage
        {
            get
            {
                return GetPortValue<HObject>(Inputs, nameof(InputImage));
            }
            set
            {
                SetPortValue(Outputs, nameof(InputImage), value);
            }
        }
        #endregion
        BlobToolForm BlobToolForm;
        public event Action UpdataImage;
        public BlobTool()
        {
            this.RootNode.Text = "BlobTool";
            this.ToolName = "BlobTool";
            this.AddInput("InputImage", TypeName.IMAGE);
            this.RootNode.ImageIndex = 0;
            
        }
        public override void ShowWin() 
        {
            BlobToolForm = new BlobToolForm(this);
            BlobToolForm.Show();
        }
        public override void Run()
        {
            base.Run();
            IsRunSuccess = true;
            UpdataImage?.Invoke();

        }
    }
}
