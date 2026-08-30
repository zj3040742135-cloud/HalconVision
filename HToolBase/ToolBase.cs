using HalconDotNet;
using HAttribute;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace HToolBase
{
    public enum TypeName
    {
        SINGAL,
        STRING,
        IMAGE,
        LINE,
        R1,
        R2,
        CIRCLE,
        REGION,
        BOOL
    }
    public static class TypeNameHelper
    {
        /// <summary>
        /// 自定义枚举TypeName 转 编译器原生System.Type
        /// </summary>
        public static Type ToSystemType(this TypeName typeName)
        {
            return typeName switch
            {
                TypeName.SINGAL => typeof(double),
                TypeName.STRING => typeof(string),
                TypeName.BOOL => typeof(bool),

                // 以下是自定义视觉/图形类型，根据你的项目替换真实类
                TypeName.IMAGE => typeof(HalconDotNet.HObject),
                TypeName.LINE => typeof(HalconDotNet.HXLD),
                //TypeName.R1 => typeof(Rect1),
                //TypeName.R2 => typeof(Rect2),
                //TypeName.CIRCLE => typeof(Circle),
                TypeName.REGION => typeof(HalconDotNet.HRegion),

                _ => throw new ArgumentOutOfRangeException(nameof(typeName), $"未定义的类型：{typeName}")
            };
        }

        /// <summary>
        /// 原生Type 反向转自定义TypeName枚举
        /// </summary>
        public static TypeName ToTypeName(this Type type)
        {
            if (type == typeof(int) || type == typeof(double))
                return TypeName.SINGAL;
            if (type == typeof(string)) return TypeName.STRING;
            if (type == typeof(bool)) return TypeName.BOOL;

            if (type == typeof(HalconDotNet.HObject)) return TypeName.IMAGE;
            if (type == typeof(HalconDotNet.HXLD)) return TypeName.LINE;
            //if (type == typeof(Rect1)) return TypeName.R1;
            //if (type == typeof(Rect2)) return TypeName.R2;
            //if (type == typeof(Circle)) return TypeName.CIRCLE;
            if (type == typeof(HalconDotNet.HRegion)) return TypeName.REGION;

            throw new ArgumentException($"无法匹配到TypeName枚举：{type.FullName}");
        }
        /// <summary>
        /// 扩展方法：将任意数字统一转为SINGAL对应double类型
        /// 适配你DataGridView单元格赋值逻辑
        /// </summary>
        public static object ConvertToSingalType(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0.0;
            return Convert.ChangeType(value, typeof(double));
        }

        /// <summary>
        /// 判断端口类型是否为 HObject 类型（IMAGE/REGION/LINE）。
        /// 这些类型的 Value 是运行时数据（图像像素/区域/XLD），不应序列化到 .vpp 文件——
        /// 由工具 Run() 重新生成，持久化只会导致文件膨胀。
        /// </summary>
        public static bool IsHObjectPort(this TypeName typeName) =>
            typeName == TypeName.IMAGE || typeName == TypeName.REGION || typeName == TypeName.LINE;
    }
    public struct rectangle1
    {
        public Double Row1, Row2, Column1, Column2;
    }
    public struct rectangle2
    {
        public Double Row1, Width, Column1, Heighht, Angle;
    }
    public struct circle
    {
        public Double R, CenterX, CenterY;
    }
    public struct LineParam
    {
        public double RowStart { get; set; } // 直线起点行坐标（Y轴）
        public double ColStart { get; set; } // 直线起点列坐标（X轴）
        public double RowEnd { get; set; }   // 直线终点行坐标
        public double ColEnd { get; set; }   // 直线终点列坐标
        public double Angle { get; set; }    // 直线角度（弧度，0~π）
        public double Length { get; set; }   // 直线长度（像素）
        public int PointCount { get; set; }  // 拟合用边缘点数量
    }
    /// <summary>动态存储任意值+记录真实数据类型</summary>
    public class DynamicItem
    {
        // 原始数据值
        public object Value { get; set; }
        // 数据完整类型名称：System.Int32 / System.Boolean / System.DateTime...
        public string TypeName { get; set; }

        // 构造：自动提取传入对象的类型
        public DynamicItem(object val)
        {
            Value = val;
            TypeName = val?.GetType().FullName;
        }

        // 读取时把Value转换成真实类型
        public T GetValue<T>()
        {
            return (T)Convert.ChangeType(Value, typeof(T));
        }

        // 根据TypeName自动还原object真实类型
        public object GetRealValue()
        {
            Type targetType = Type.GetType(TypeName);
            if (targetType == null || Value == null) return null;
            return Convert.ChangeType(Value, targetType);
        }
    }
    /// <summary>
    /// Region/XLD 显示叠加项：独立于端口的运行时显示数据 + 可持久化配置。
    /// 工具在 Run() 中通过 ToolBase.AddDisplayRegion/AddDisplayXLD 发布，
    /// 由 ToolBlock.CollectDisplayItems 聚合后交给 HDisplayControl 渲染。
    /// Data setter 深拷贝 + 释放旧值，与 PortNode.Value 所有权不变式一致。
    /// </summary>
    public class DisplayItem
    {
        /// <summary>工具内唯一名，如 "FoundRegion"</summary>
        public string Name { get; set; }
        /// <summary>REGION 或 LINE</summary>
        public TypeName Type { get; set; }
        /// <summary>是否显示</summary>
        public bool Visible { get; set; } = true;
        /// <summary>HALCON 颜色名：red/green/blue/yellow/cyan/magenta/white/black...</summary>
        public string Color { get; set; } = "red";
        /// <summary>region 绘制模式："fill"/"margin"；XLD 忽略此字段</summary>
        public string Draw { get; set; } = "margin";
        /// <summary>线宽（region 边界与 XLD 均生效）</summary>
        public double LineWidth { get; set; } = 1.0;

        // 运行时数据（不持久化）。setter 深拷贝 + 释放旧值，断开与调用方的引用共享。
        private HObject _data;
        public HObject Data
        {
            get => _data;
            set
            {
                if (ReferenceEquals(_data, value)) return;
                if (_data is HObject old && old.IsInitialized()) { try { old.Dispose(); } catch { } }
                if (value is HObject n && n.IsInitialized())
                {
                    try { HOperatorSet.CopyObj(n, out HObject copy, 1, -1); _data = copy; }
                    catch { _data = value; }
                }
                else
                {
                    _data = value;
                }
            }
        }

        /// <summary>仅克隆配置字段（不含 Data），用于显示层独立持有配置副本。</summary>
        public DisplayItem CloneConfig() => new DisplayItem
        {
            Name = Name,
            Type = Type,
            Visible = Visible,
            Color = Color,
            Draw = Draw,
            LineWidth = LineWidth
        };

        /// <summary>直接设置内部 _data 引用，跳过 setter 的二次深拷贝。
        /// 仅供显示层在已 CopyObj 后赋值使用，避免双重拷贝。调用方须保证传入的是独立副本。</summary>
        internal void SetDataRaw(HObject data)
        {
            if (_data is HObject old && old.IsInitialized()) { try { old.Dispose(); } catch { } }
            _data = data;
        }
    }

    public static class JsonDynamicHelper
    {
        // Json全局配置
        private static readonly JsonSerializerSettings JsonSetting = new JsonSerializerSettings()
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat
        };

        /// <summary>获取程序根目录，解决Release目录权限报错</summary>
        public static string GetAppRootPath()
        {
            return Path.GetDirectoryName(Application.ExecutablePath);
        }

        /// <summary>安全创建文件夹（修复你代码里文件夹/文件路径混淆BUG）</summary>
        public static string GetSafeFilePath(string subDir, string fileName)
        {
            string root = GetAppRootPath();
            string dirPath = Path.Combine(root, subDir);
            // 只创建文件夹，不要把文件名传入CreateDirectory
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            string fullFile = Path.Combine(dirPath, fileName);
            return fullFile;
        }

        #region 方案1：两组字典嵌套分组存入单个文件
        /// <summary>把两组Dynamic字典合并为分组结构，保存到同一个文件</summary>
        public static void SaveTwoGroupData(
            Dictionary<string, DynamicItem> groupA,
            Dictionary<string, DynamicItem> groupB,
            string subFolder, string fileName)
        {
            // 外层根对象，区分两组数据
            var allData = new Dictionary<string, Dictionary<string, DynamicItem>>
            {
                ["GroupA"] = groupA,
                ["GroupB"] = groupB
            };

            string filePath = GetSafeFilePath(subFolder, fileName);
            string json = JsonConvert.SerializeObject(allData, JsonSetting);
            // 写入文件，UTF8防止中文乱码
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        }

        /// <summary>读取分组JSON，还原两组字典</summary>
        public static (Dictionary<string, DynamicItem> groupA, Dictionary<string, DynamicItem> groupB) LoadTwoGroupData(string subFolder, string fileName)
        {
            string filePath = GetSafeFilePath(subFolder, fileName);
            if (!File.Exists(filePath))
                return (new Dictionary<string, DynamicItem>(), new Dictionary<string, DynamicItem>());

            string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            var allData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, DynamicItem>>>(json, JsonSetting);

            allData.TryGetValue("GroupA", out var a);
            allData.TryGetValue("GroupB", out var b);
            return (a ?? new(), b ?? new());
        }
        #endregion

        #region 方案2：数组形式保存多组（适合多工具/多配方）
        public static void SaveDataList(List<Dictionary<string, DynamicItem>> dataList, string subFolder, string fileName)
        {
            string filePath = GetSafeFilePath(subFolder, fileName);
            string json = JsonConvert.SerializeObject(dataList, JsonSetting);
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        }

        public static List<Dictionary<string, DynamicItem>> LoadDataList(string subFolder, string fileName)
        {
            string filePath = GetSafeFilePath(subFolder, fileName);
            if (!File.Exists(filePath)) return new();
            string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<Dictionary<string, DynamicItem>>>(json, JsonSetting);
        }
        #endregion
    }
    public class PortNode : TreeNode
    {
        public String PortName { get; set; }
        public TypeName PortType { get; set; }
        public string Direction { get; set; }
        public ToolBase BelongTool { get; set; }
        private object _value;

        public object Value
        {
            get => _value;
            set
            {
                if (Equals(_value, value)) return;       // 同引用/同值 → 无操作
                // 先释放旧 HObject（端口独占持有，可安全释放）
                if (_value is HObject oldHObj)
                {
                    try { if (oldHObj.IsInitialized()) oldHObj.Dispose(); } catch { }
                }
                // HObject 深拷贝：断开与调用方的引用共享，端口独占副本
                // 用 CopyObj 覆盖 HImage/HRegion/HXLD 全部 iconic 类型
                if (value is HObject newHObj && newHObj.IsInitialized())
                {
                    try { HOperatorSet.CopyObj(newHObj, out HObject copy, 1, -1); _value = copy; }
                    catch { _value = value; }            // 拷贝失败回退为引用（保降级可用）
                }
                else
                {
                    _value = value;                      // 非 HObject / null / 未初始化：直接存
                }
                ValueChanged?.Invoke(this, EventArgs.Empty);
                //Console.WriteLine("Invole");
            }
        }
        public event EventHandler ValueChanged;
        public PortNode() : base() { }

        public PortNode(string text, TypeName portType, string direction, ToolBase belongTool, string portName, object value)
            : base(text)
        {
            PortType = portType;
            Direction = direction;
            BelongTool = belongTool;
            PortName = portName;
            _value = value;
        }
    }

    public class ToolBase : IDisposable
    {

        public TreeNode RootNode;
        public string ToolName;
        public Dictionary<string, PortNode> Inputs = new Dictionary<string, PortNode>();
        public Dictionary<string, PortNode> Outputs = new Dictionary<string, PortNode>();

        /// <summary>所属父 ToolBlock（由 ToolCollection.Add/Load 设置）。
        /// 脚本工具等需要访问兄弟工具/父块的场景使用；普通工具可不关心。
        /// 顶层 ToolBlock（由 ToolModule 持有）的 Parent 为 null。</summary>
        public HToolBase.Tools.ToolBlock Parent { get; set; }

        /// <summary>本工具发布的 Region/XLD 显示叠加项（按 Name 索引）。
        /// 配置（颜色/Draw/线宽/可见性）持久化，Data 运行时由 Run() 填充。
        /// 独立于端口系统——不参与连线，仅供 HDisplayControl 渲染。</summary>
        public Dictionary<string, DisplayItem> DisplayItems { get; } = new Dictionary<string, DisplayItem>();

        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        // 四个运行状态属性改用私有字段，不再经 SetPortValue 隐式创建 SINGAL 幽灵端口。
        // 旧实现中 setter 写 Outputs、getter 读 Inputs，读写方向不一致导致值完全不同步，且每次赋值
        // 会在 Outputs 里自动创建 TypeName.SINGAL 类型的端口（块级脚本赋值 Message/IsRunSuccess
        // 后触发保存→下次加载，多出两个 Single 类型端口）。
        private bool _isRunSuccess;
        private string _message = string.Empty;
        private double _totalTime;
        private string _result = string.Empty;

        [FieldInfoTagAttribute("RunStatus", typeof(bool), "Output")]
        public bool IsRunSuccess
        {
            get => _isRunSuccess;
            set => _isRunSuccess = value;
        }
        [FieldInfoTagAttribute("RunStatus", typeof(string), "Output")]
        public string Message
        {
            get => _message ?? string.Empty;
            set => _message = value ?? string.Empty;
        }
        [FieldInfoTagAttribute("RunStatus", typeof(double), "Output")]
        public double TotalTime
        {
            get => _totalTime;
            set => _totalTime = value;
        }
        [FieldInfoTagAttribute("RunStatus", typeof(string), "Output")]
        public string Result
        {
            get => _result ?? string.Empty;
            set => _result = value ?? string.Empty;   // 修复旧 Bug：此处误写 nameof(TotalTime)
        }
        //public delegate void AddHandler(PortNode portNode);
        //public event AddHandler AddInputEvent;
        //public event AddHandler AddOutputEvent;
        public ToolBase()
        {
            RootNode = new TreeNode();
            ToolName = string.Empty;
        }
        public virtual void Run() { }
        public virtual void OnDeleted() { }
        public virtual void ShowWin() { }
        /// <summary>
        /// 释放端口持有的 HObject 等非托管资源并清空集合。
        /// ToolBlock override 此方法以递归释放内部工具与显示缓存。
        /// 叶节点工具继承默认实现即可自动释放端口 Value，无需额外代码。
        /// </summary>
        public virtual void Dispose()
        {
            DisposePortValues(Inputs);
            DisposePortValues(Outputs);
            ClearDisplayItems();
            Inputs?.Clear();
            Outputs?.Clear();
            Parameters?.Clear();
            RootNode = null;
        }
        /// <summary>
        /// 释放端口字典中所有 HObject 类型的 Value（端口独占副本，可安全释放）。
        /// </summary>
        private static void DisposePortValues(Dictionary<string, PortNode> ports)
        {
            if (ports == null) return;
            foreach (var port in ports.Values)
            {
                if (port.Value is HObject hObj)
                {
                    try { if (hObj.IsInitialized()) hObj.Dispose(); } catch { }
                }
            }
        }
        public virtual List<PortNode> CollectImage()
        {
            List<PortNode> portNodes = new List<PortNode>();
            foreach (PortNode n in this.Outputs.Values)
            {
                if (n.PortType is TypeName.IMAGE)
                {
                    portNodes.Add(n);
                }
            }
            return portNodes;
        }

        #region Region/XLD 显示叠加项发布 API
        // 独立于端口系统：工具在 Run() 中调用以下方法发布/更新显示项。
        // 首次发布用 Add*，后续帧用 UpdateDisplayData 按 Name 刷新数据。
        // 传入的 HObject 会被深拷贝（CopyObj），调用方须自行 Dispose 临时对象。

        /// <summary>发布一个 Region 显示项（Name 自动去重）。</summary>
        protected void AddDisplayRegion(string name, HObject region,
            string color = "red", string draw = "margin", double lineWidth = 1.0)
        {
            if (string.IsNullOrEmpty(name)) name = "Region";
            name = HToolBase.Tools.ToolBlock.GetUniqueName(DisplayItems, name);
            var item = new DisplayItem
            {
                Name = name,
                Type = TypeName.REGION,
                Color = color,
                Draw = draw,
                LineWidth = lineWidth,
                Data = region
            };
            DisplayItems[name] = item;
        }

        /// <summary>发布一个 XLD 显示项（Name 自动去重）。</summary>
        protected void AddDisplayXLD(string name, HObject xld,
            string color = "yellow", double lineWidth = 1.0)
        {
            if (string.IsNullOrEmpty(name)) name = "XLD";
            name = HToolBase.Tools.ToolBlock.GetUniqueName(DisplayItems, name);
            var item = new DisplayItem
            {
                Name = name,
                Type = TypeName.LINE,
                Color = color,
                Draw = "margin",      // XLD 忽略 Draw，保留默认值
                LineWidth = lineWidth,
                Data = xld
            };
            DisplayItems[name] = item;
        }

        /// <summary>仅更新已存在显示项的运行时数据（Run() 每帧调用以刷新结果）。
        /// 项不存在时返回 false，不自动创建（避免误发布）。</summary>
        protected bool UpdateDisplayData(string name, HObject data)
        {
            if (DisplayItems.TryGetValue(name, out var item))
            {
                item.Data = data;
                return true;
            }
            return false;
        }

        /// <summary>移除并释放指定显示项。返回是否成功移除。</summary>
        protected bool RemoveDisplayItem(string name)
        {
            if (DisplayItems.TryGetValue(name, out var item))
            {
                if (item.Data is HObject h && h.IsInitialized()) { try { h.Dispose(); } catch { } }
                return DisplayItems.Remove(name);
            }
            return false;
        }

        /// <summary>清空并释放所有显示项的 Data HObject（配置随字典清空一并移除）。
        /// Dispose 时调用；运行时不应调用（会丢失用户配置）。</summary>
        protected void ClearDisplayItems()
        {
            if (DisplayItems == null) return;
            foreach (var item in DisplayItems.Values)
            {
                if (item.Data is HObject h && h.IsInitialized()) { try { h.Dispose(); } catch { } }
            }
            DisplayItems.Clear();
        }
        #endregion
        public virtual List<TreeNode> GetAllProperty()
        {
            var dataList = TagHelper.GetAllPropertyWithTag(this);

            // 1. 分组：按DisplayName分组
            var groupData = dataList
                .GroupBy(m => m.HasCustomTag ? m.DisplayName : "无标签属性")
                .ToDictionary(g => g.Key, g => g.ToList());
            List<TreeNode> nodeList = new List<TreeNode>();
            // 2. 遍历分组创建根节点
            foreach (var group in groupData)
            {
                // 创建根节点（DisplayName）
                TreeNode rootNode = new TreeNode(group.Key);
                // 遍历该分组下所有属性，创建子节点（原生属性名）
                foreach (var propItem in group.Value)
                {
                    // 子节点文本：原生属性名
                    TreeNode childNode = new TreeNode(propItem.PropertyName + $" <{propItem.Category}>");
                    // 把子模型存入Tag，点击节点可取值、备注、分类
                    childNode.Tag = propItem;
                    // 可选：子节点后缀显示当前值
                    childNode.Text += $" = {propItem.PropertyValue}";

                    rootNode.Nodes.Add(childNode);
                }
                nodeList.Add(rootNode);
            }
            return nodeList;
        }
        public virtual bool AddInput(string portName, TypeName portType)
        {
            if (RootNode == null)
                return false;
            if (Inputs.TryGetValue(portName, out var existingPort))
                return false;
            PortNode portNode = new PortNode(
                text: portName,
                portType: portType,
                direction: "Input",
                belongTool: this,
                portName: portName,
                value: null
            );
            portNode.ImageIndex = 2;
            portNode.SelectedImageIndex = 2;
            // 插入到第一个输出端口之前，确保输入节点在输出节点上方
            int insertIndex = RootNode.Nodes.Count;
            for (int i = 0; i < RootNode.Nodes.Count; i++)
            {
                if (RootNode.Nodes[i] is PortNode p && p.Direction == "Output")
                {
                    insertIndex = i;
                    break;
                }
            }
            RootNode.Nodes.Insert(insertIndex, portNode);
            Inputs[portName] = portNode;
            //AddInputEvent?.Invoke(portNode);
            return true;
        }
        public virtual bool AddOutput(string portName, TypeName portType)
        {
            if (RootNode == null)
                return false;
            if (Outputs.TryGetValue(portName, out var existingPort))
                return false;
            PortNode portNode = new PortNode(
                text: portName,
                portType: portType,
                direction: "Output",
                belongTool: this,
                portName: portName,
                value: null
            );
            portNode.ImageIndex = 1;
            portNode.SelectedImageIndex = 1;
            // 追加到末尾，确保输出节点在所有输入节点下方
            RootNode.Nodes.Add(portNode);
            Outputs[portName] = portNode;
            //AddOutputEvent?.Invoke(portNode);
            return true;
        }
        /// <summary>
        /// 从端口字典读取值回属性
        /// portDict：Inputs / Outputs
        /// propName：属性名（如ImageStr）
        /// </summary>
        protected T GetPortValue<T>(Dictionary<string, PortNode> portDict, string propName)
        {
            // 端口不存在返回默认值
            if (!portDict.ContainsKey(propName))
                return default;

            var port = portDict[propName];
            if (port.Value is T val)
                return val;

            // 类型转换兼容
            try
            {
                return (T)Convert.ChangeType(port.Value, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// 将属性值写入对应端口
        /// </summary>
        protected void SetPortValue<T>(Dictionary<string, PortNode> portDict, string propName, T value)
        {
            // 端口不存在则跳过，不再隐式 new PortNode()——避免误写入时自动创建 TypeName.SINGAL
            // 幽灵端口（旧实现中 IsRunSuccess/Message 赋值触发 ToolBlock 持久化后反复出现）。
            if (!portDict.TryGetValue(propName, out var port))
                return;
            port.Value = value;
        }
        public virtual Dictionary<string, DynamicItem> SaveToolParam()
        {
            var saveData = new ToolSaveData();
            foreach (PortNode port in this.Inputs.Values)
            {
                // HObject 类型端口（IMAGE/REGION/LINE）的 Value 是运行时数据（图像像素/区域/XLD），
                // 由 Run() 重新生成，不持久化——序列化这些数据会导致 .vpp 文件膨胀数百 KB
                bool skipValue = port.PortType.IsHObjectPort();
                saveData.ToolInputs.Add(new PortSaveData
                {
                    Name = port.PortName,
                    PortType = port.PortType.ToString(),
                    Value = (!skipValue && port.Value != null) ? new DynamicItem(port.Value) : null
                });
            }

            // 3. 保存ToolBlock自身的输出端口
            foreach (PortNode port in this.Outputs.Values)
            {
                bool skipValue = port.PortType.IsHObjectPort();
                saveData.ToolOutputs.Add(new PortSaveData
                {
                    Name = port.PortName,
                    PortType = port.PortType.ToString(),
                    Value = (!skipValue && port.Value != null) ? new DynamicItem(port.Value) : null
                });
            }
            return new Dictionary<string, DynamicItem>
            {


                {"Tool",new DynamicItem(this.GetType().AssemblyQualifiedName)},
                {"ToolName",new DynamicItem(this.ToolName)},
                {"RootNode_Text",new DynamicItem(this.RootNode.Text)},
                {"Inputs",new DynamicItem(saveData.ToolInputs)},
                {"OutPorts",new DynamicItem(saveData.ToolOutputs)},
                // 显示叠加项配置（仅配置，不含 HObject Data——Data 由 Run() 重新生成）
                {"DisplayItems", new DynamicItem(
                    DisplayItems.Values.Select(it => new
                    {
                        Name = it.Name,
                        Type = it.Type.ToString(),
                        it.Visible,
                        it.Color,
                        it.Draw,
                        it.LineWidth
                    }).ToList())},
            };
        }
        public virtual void LoadToolParam(Dictionary<string, DynamicItem> paramDict)
        {
            if (paramDict.TryGetValue("ToolName", out var item1))
                ToolName = Convert.ToString(item1.Value);
            if (paramDict.TryGetValue("RootNode_Text", out var item2))
                this.RootNode.Text = Convert.ToString(item2.Value);

            // 加载输入端口（SaveToolParam保存格式：List<PortSaveData>，键"Inputs"）
            if (paramDict.TryGetValue("Inputs", out var inputsItem) && inputsItem?.Value != null)
            {
                foreach (var psd in ToPortSaveDataList(inputsItem.Value))
                {
                    // 向后兼容：过滤旧版本误作为 SINGAL 端口持久化的四个运行状态属性（幽灵端口）
                    if (Tools.ToolBlock.IsLegacyGhostPort(psd.Name, psd.PortType))
                        continue;

                    if (Enum.TryParse<TypeName>(psd.PortType, out var portType))
                    {
                        this.AddInput(psd.Name, portType);
                        if (psd.Value != null && this.Inputs.ContainsKey(psd.Name))
                            this.Inputs[psd.Name].Value = psd.Value.GetRealValue();
                    }
                }
            }

            // 加载输出端口（SaveToolParam保存格式：List<PortSaveData>，键"OutPorts"）
            if (paramDict.TryGetValue("OutPorts", out var outputsItem) && outputsItem?.Value != null)
            {
                foreach (var psd in ToPortSaveDataList(outputsItem.Value))
                {
                    if (Tools.ToolBlock.IsLegacyGhostPort(psd.Name, psd.PortType))
                        continue;

                    if (Enum.TryParse<TypeName>(psd.PortType, out var portType))
                    {
                        this.AddOutput(psd.Name, portType);
                        if (psd.Value != null && this.Outputs.ContainsKey(psd.Name))
                            this.Outputs[psd.Name].Value = psd.Value.GetRealValue();
                    }
                }
            }

            // 加载显示叠加项配置（仅配置，Data 保持 null——由 Run() 重新填充）。
            // 若同名项已存在（运行时已发布），仅更新配置字段，保留已有 Data。
            if (paramDict.TryGetValue("DisplayItems", out var dispItem) && dispItem?.Value != null)
            {
                foreach (var jo in ToJObjectList(dispItem.Value))
                {
                    string name = jo["Name"]?.ToString();
                    if (string.IsNullOrEmpty(name)) continue;
                    Enum.TryParse<TypeName>(jo["Type"]?.ToString(), out var dtype);
                    bool visible = jo["Visible"]?.ToObject<bool>() ?? true;
                    string color = jo["Color"]?.ToString() ?? "red";
                    string draw = jo["Draw"]?.ToString() ?? "margin";
                    double lw = jo["LineWidth"]?.ToObject<double>() ?? 1.0;

                    if (DisplayItems.TryGetValue(name, out var existing))
                    {
                        // 仅更新配置，保留 Data
                        existing.Type = dtype;
                        existing.Visible = visible;
                        existing.Color = color;
                        existing.Draw = draw;
                        existing.LineWidth = lw;
                    }
                    else
                    {
                        DisplayItems[name] = new DisplayItem
                        {
                            Name = name,
                            Type = dtype,
                            Visible = visible,
                            Color = color,
                            Draw = draw,
                            LineWidth = lw
                            // Data = null，等 Run() 填充
                        };
                    }
                }
            }
        }

        /// <summary>将 DynamicItem.Value 反序列化为 List<JObject>。
        /// DisplayItems 保存为匿名对象列表，JSON 反序列化后是 JArray，逐项转 JObject 读取字段。</summary>
        private static List<JObject> ToJObjectList(object value)
        {
            var result = new List<JObject>();
            if (value is JArray jarr)
            {
                foreach (var t in jarr)
                {
                    if (t is JObject jo) result.Add(jo);
                }
            }
            else if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (var t in enumerable)
                {
                    if (t is JObject jo) result.Add(jo);
                    else if (t != null)
                    {
                        // 已是强类型对象（非 JSON 路径）→ 转回 JObject
                        var back = JObject.FromObject(t);
                        if (back != null) result.Add(back);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 将DynamicItem.Value反序列化为List<PortSaveData>。
        /// JSON反序列化时List<PortSaveData>会变成JArray，需手动转换。
        /// </summary>
        private static List<PortSaveData> ToPortSaveDataList(object value)
        {
            if (value is List<PortSaveData> list)
                return list;
            if (value is JArray jarr)
                return jarr.ToObject<List<PortSaveData>>();
            return new List<PortSaveData>();
        }

        /// <summary>
        /// 修改字典key，value保持不变
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dict">字典</param>
        /// <param name="oldKey">旧键</param>
        /// <param name="newKey">新键</param>
        /// <returns>修改成功返回true；旧键不存在/新键已存在返回false</returns>
        public bool RenameKey<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey oldKey, TKey newKey)
        {
            // 校验旧键是否存在
            if (!dict.ContainsKey(oldKey))
                return false;

            // 校验新键是否冲突
            if (dict.ContainsKey(newKey))
                return false;

            // 取出原值
            TValue val = dict[oldKey];
            dict.Remove(oldKey);
            dict.Add(newKey, val);
            return true;
        }
    }
}

