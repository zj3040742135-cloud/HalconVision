using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HToolBase
{
    //工具空间基类，控件类命名标准“工具类名+Form",更具类名反射实例工具控件
    public class HForm:Form
    {
        public virtual ToolBase tool {  get; set; }
        public virtual void BindingEvent() { }
        public virtual void UnBindEvent() { }
    }
}
