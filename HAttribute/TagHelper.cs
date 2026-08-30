using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HAttribute
{
    /// <summary>
    /// 存储属性名、值、标签信息
    /// </summary>
    public class PropertyTagModel
    {
        // 原生属性名
        public string PropertyName { get; set; }
        // 属性当前值
        public object PropertyValue { get; set; }
        // 是否标记自定义标签
        public bool HasCustomTag { get; set; }

        // 标签内容
        public string DisplayName { get; set; }
        public Type Category { get; set; }
        public string Remark { get; set; }
    }
    public static class TagHelper
    {
        /// <summary>
        /// 通用：读取任意对象（普通类/控件）的属性+自定义标签
        /// </summary>
        public static List<PropertyTagModel> GetAllPropertyWithTag(object obj)
        {
            List<PropertyTagModel> list = new List<PropertyTagModel>();
            if (obj == null) return list;

            // 获取所有公开实例属性
            PropertyInfo[] props = obj.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                var model = new PropertyTagModel
                {
                    PropertyName = prop.Name,
                    PropertyValue = prop.GetValue(obj)
                };

                // 读取自定义标签
                var tag = prop.GetCustomAttribute<FieldInfoTagAttribute>();
                if (tag != null)
                {
                    model.HasCustomTag = true;
                    model.DisplayName = tag.DisplayName;
                    model.Category = tag.Category;
                    model.Remark = tag.Remark;
                }
                else
                {
                    model.HasCustomTag = false;
                }
                list.Add(model);
            }
            return list;
        }
    }
}
