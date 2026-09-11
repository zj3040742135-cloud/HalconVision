using HAttribute;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using HalconDotNet;
namespace HToolBase.Tools
{

    public class ImageSourceTool:ToolBase
    {
        HObject r;
        private HObject _outputImage = new HObject();
        public event Action UpdataImage;
        public string ImageStr;
        #region//工具属性
        [FieldInfoTagAttribute("OutputImage", typeof(HObject), "Output")]
        public HObject OutputImage
        {
            get
            {
                return _outputImage;//GetPortValue<HObject>(Inputs, nameof(OutputImage))
            }
            set
            {
                // 第一步：同步本地缓存字段（关键缺失代码）
                _outputImage = value;
                // 第二步：写入端口字典
                SetPortValue(Outputs, nameof(OutputImage), value);
            }
        }
        [FieldInfoTagAttribute("OutputImage", typeof(int), "Output")]
        public int Width {
            get
            {
                return GetPortValue<int>(Inputs, nameof(Width));
            }
            set
            {
                SetPortValue(Outputs, nameof(Width), value);
            }
        }
        [FieldInfoTagAttribute("OutputImage", typeof(int), "Output")]
        public int Height {
            get
            {
                return GetPortValue<int>(Inputs, nameof(Height));
            }
            set
            {
                SetPortValue(Outputs, nameof(Height), value);
            }
        }
        #endregion
        public ImageSourceTool()
        {
            this.RootNode.Text = "ImageSourceTool";
            this.RootNode.Tag = "ImageSourceTool";
            this.ToolName= "ImageSourceTool";
            this.AddOutput("OutputImage", TypeName.IMAGE);
            //this.AddInput("Debug", TypeName.SINGAL);
            //this.Outputs["OutputImage"].Value = "ssssssccca";
            this.RootNode.ImageIndex = 0;
            
            //HOperatorSet.GenRectangle1(out r, 200, 200, 800, 800);
            //AddDisplayRegion("FoundRegion", r, color: "green", draw: "margin", lineWidth: 1.5);
        }
        public override void Run()
        {
            HObject image = null;
            try
            {
                RemoveDisplayItem("FoundRegion");
                HOperatorSet.GenRectangle1(out r, 200, 200, 800, 800);
                AddDisplayRegion("FoundRegion", r, color: "green", draw: "margin", lineWidth: 1.5);
                //this.Outputs["IsRunSuccess"].Value = this.Inputs["IsRunSuccess"].Value;
                HOperatorSet.ReadImage(out image, ImageStr);
                // setter 深拷贝，端口独占副本；image 由 finally 释放
                OutputImage = image;
                UpdateDisplayData("FoundRegion", r);
                UpdataImage?.Invoke();
                IsRunSuccess = true;
            }
            catch
            {
                IsRunSuccess = false;
                // 保留原吞异常行为，避免阻塞流程
            }
            finally
            {
                image?.Dispose();
            }
            //IsRunSuccess = true;
        }
    }
}
