using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace HDesigner
{
    /// <summary>界面设计序列化文档：控件树+事件绑定，XML保存/加载。
    /// 事件绑定仅随设计保存，设计态不生效，由运行窗口恢复并生效</summary>
    public class DesignDocument
    {
        public int CanvasWidth { get; set; }
        public int CanvasHeight { get; set; }
        public List<ControlData> Controls { get; set; } = new List<ControlData>();

        #region 捕获（设计画布 → 文档）

        /// <summary>从设计画布捕获当前设计（含嵌套控件与事件绑定）。
        /// 按z序从前到后捕获，Populate按同序重建以保持层叠关系</summary>
        public static DesignDocument Capture(Control canvas, HashSet<Control> designed,
            Dictionary<Control, Dictionary<string, EventBinding>> bindings)
        {
            DesignDocument doc = new DesignDocument
            {
                CanvasWidth = canvas.ClientSize.Width,
                CanvasHeight = canvas.ClientSize.Height
            };
            foreach (Control c in canvas.Controls.Cast<Control>().OrderBy(ch => canvas.Controls.GetChildIndex(ch)))
            {
                if (!designed.Contains(c)) continue;
                doc.Controls.Add(CaptureControl(c, designed, bindings));
            }
            return doc;
        }

        private static ControlData CaptureControl(Control c, HashSet<Control> designed,
            Dictionary<Control, Dictionary<string, EventBinding>> bindings)
        {
            ControlData d = new ControlData
            {
                TypeName = c.GetType().FullName,
                Name = c.Name,
                Text = c.Text,
                X = c.Left,
                Y = c.Top,
                Width = c.Width,
                Height = c.Height,
                Dock = c.Dock.ToString(),
                SplitPanelIndex = -1
            };
            SplitContainer sc = c as SplitContainer;
            if (sc != null)
            {
                d.SplitterOrientation = (int)sc.Orientation;
                d.SplitterDistance = sc.SplitterDistance;
            }

            Dictionary<string, EventBinding> map;
            if (bindings.TryGetValue(c, out map))
            {
                foreach (KeyValuePair<string, EventBinding> kv in map)
                {
                    if (kv.Value == null || string.IsNullOrEmpty(kv.Value.Target)) continue;
                    d.Bindings.Add(new BindingData
                    {
                        EventName = kv.Key,
                        FunctionType = kv.Value.FunctionType,
                        Target = kv.Value.Target
                    });
                }
            }

            // SplitContainer的设计子控件位于Panel1/Panel2内，需下钻一层并记录所属分栏
            List<KeyValuePair<Control, int>> childItems = new List<KeyValuePair<Control, int>>();
            if (sc != null)
            {
                foreach (Control ch in sc.Panel1.Controls.Cast<Control>().OrderBy(x => sc.Panel1.Controls.GetChildIndex(x)))
                    childItems.Add(new KeyValuePair<Control, int>(ch, 0));
                foreach (Control ch in sc.Panel2.Controls.Cast<Control>().OrderBy(x => sc.Panel2.Controls.GetChildIndex(x)))
                    childItems.Add(new KeyValuePair<Control, int>(ch, 1));
            }
            else
            {
                foreach (Control ch in c.Controls.Cast<Control>().OrderBy(x => c.Controls.GetChildIndex(x)))
                    childItems.Add(new KeyValuePair<Control, int>(ch, -1));
            }
            foreach (KeyValuePair<Control, int> item in childItems)
            {
                if (!designed.Contains(item.Key)) continue;
                ControlData cd = CaptureControl(item.Key, designed, bindings);
                cd.SplitPanelIndex = item.Value;
                d.Children.Add(cd);
            }
            return d;
        }

        #endregion

        #region 重建（文档 → 控件树）

        /// <summary>按文档重建控件树挂到parent下。onCreated回调由调用方提供：
        /// 设计器加载时绑定设计器事件/登记设计控件/恢复绑定表；
        /// 运行窗口构建时恢复绑定表并订阅事件（绑定在运行窗口生效）</summary>
        public static Control Populate(ControlData data, Control parent, Action<Control, ControlData> onCreated)
        {
            if (data == null || string.IsNullOrEmpty(data.TypeName)) return null;
            Type t = ResolveType(data.TypeName);
            if (t == null)
            {
                MessageBox.Show($"未找到控件类型：{data.TypeName}，已跳过", "加载设计",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            Control c = (Control)Activator.CreateInstance(t);
            c.Name = data.Name ?? "";
            c.Text = data.Text ?? "";
            c.Bounds = new Rectangle(data.X, data.Y, data.Width, data.Height);
            DockStyle dock;
            Enum.TryParse(data.Dock, out dock);
            c.Dock = dock;

            SplitContainer sc = c as SplitContainer;
            if (sc != null)
            {
                sc.Orientation = data.SplitterOrientation == (int)Orientation.Horizontal
                    ? Orientation.Horizontal : Orientation.Vertical;
                if (data.SplitterDistance > 0)
                {
                    try { sc.SplitterDistance = data.SplitterDistance; }
                    catch { /* 距离超出范围时保持默认 */ }
                }
            }

            // SplitContainer的子控件实际挂到Panel1/Panel2
            Control container = parent;
            SplitContainer parentSc = parent as SplitContainer;
            if (parentSc != null && data.SplitPanelIndex >= 0)
                container = data.SplitPanelIndex == 0 ? (Control)parentSc.Panel1 : parentSc.Panel2;

            container.Controls.Add(c);
            if (onCreated != null) onCreated(c, data);

            foreach (ControlData child in data.Children)
                Populate(child, c, onCreated);
            return c;
        }

        #endregion

        #region 持久化

        public static void Save(DesignDocument doc, string fileName)
        {
            XmlSerializer ser = new XmlSerializer(typeof(DesignDocument));
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
                ser.Serialize(fs, doc);
        }

        public static DesignDocument Load(string fileName)
        {
            XmlSerializer ser = new XmlSerializer(typeof(DesignDocument));
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                return (DesignDocument)ser.Deserialize(fs);
        }

        /// <summary>按全名在已加载程序集中解析控件/窗口类型</summary>
        public static Type ResolveType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            Type t = Type.GetType(fullName, false);
            if (t != null) return t;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        #endregion
    }

    /// <summary>单个设计控件的序列化数据</summary>
    public class ControlData
    {
        public string TypeName { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Dock { get; set; }
        public int SplitterOrientation { get; set; }
        public int SplitterDistance { get; set; }
        /// <summary>父容器为SplitContainer时所属分栏（0=Panel1, 1=Panel2, -1=非分栏子控件）</summary>
        public int SplitPanelIndex { get; set; } = -1;
        public List<BindingData> Bindings { get; set; } = new List<BindingData>();
        public List<ControlData> Children { get; set; } = new List<ControlData>();
    }

    /// <summary>单条事件绑定序列化数据</summary>
    public class BindingData
    {
        public string EventName { get; set; }
        public string FunctionType { get; set; }
        public string Target { get; set; }
    }
}
