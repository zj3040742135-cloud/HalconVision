using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HToolBase
{
    public class HForm:Form
    {
        public virtual ToolBase tool {  get; set; }
        public virtual void BindingEvent() { }
        public virtual void UnBindEvent() { }
    }
}
