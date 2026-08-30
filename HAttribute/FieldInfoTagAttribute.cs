using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HAttribute
{
    // 自定义属性标签，用于标记控件属性附加信息
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FieldInfoTagAttribute : Attribute
    {
        // 自定义字段显示名称
        public string DisplayName { get; set; }
        // 字段分类
        public Type Category { get; set; }
        // 字段备注说明
        public string Remark { get; set; }

        public FieldInfoTagAttribute(string displayName, Type category, string remark)
        {
            DisplayName = displayName;
            Category = category;
            Remark = remark;
        }
    }
}
