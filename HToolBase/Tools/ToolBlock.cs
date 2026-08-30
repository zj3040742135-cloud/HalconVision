﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using HToolBase.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HalconDotNet;
namespace HToolBase.Tools
{
    public struct PortConnection
    {
        public ToolBase FromTool;
        public PortNode FromPort;
        public TypeName PortType;
        public ToolBase ToTool;
        public PortNode ToPort;
    }

    
    public class ToolAddedEventArgs : EventArgs
    {
        public ToolBase Tool { get; }
        public ToolAddedEventArgs(ToolBase tool)
        {
            Tool = tool;
        }
    }

    public class ToolCollection : Dictionary<string, ToolBase>
    {
        public event EventHandler<ToolAddedEventArgs> ToolAdded;
        private Dictionary<string, int> _typeCounters = new Dictionary<string, int>();
        private Dictionary<string, string> _keyToTypeName = new Dictionary<string, string>();

        /// <summary>本集合所属的 ToolBlock。Add/Load 时据此为子工具设置 Parent 反向引用。
        /// 由 ToolBlock 构造函数赋值。</summary>
        internal ToolBlock Owner { get; set; }

        public new void Add(string key, ToolBase value)
        {
            string typeName = value.ToolName;
            if (!_typeCounters.ContainsKey(typeName))
            {
                _typeCounters[typeName] = 0;
            }
            _typeCounters[typeName]++;

            string indexedName = $"{typeName}{_typeCounters[typeName]}";
            value.ToolName = indexedName;
            value.RootNode.Text = indexedName;

            _keyToTypeName[indexedName] = typeName;
                // 嵌套ToolBlock标记：加入父级Tools后不再单独写.vpp，数据通过SaveToolParam嵌入父级文件
                if (value is ToolBlock tb) tb.IsNested = true;
                // 设置父级反向引用，供脚本工具等访问兄弟工具/父块
                value.Parent = Owner;
                base.Add(indexedName, value);
                ToolAdded?.Invoke(this, new ToolAddedEventArgs(value));
            }
            public void Load(string key, ToolBase value)
            {
                // 加载时直接使用读取的工具名，不加序号，不触发ToolAdded事件
                value.ToolName = key;
                value.RootNode.Text = key;

            // 解析工具名，提取基础类型名和序号（如 "ImageSourceTool1" → baseName="ImageSourceTool", number=1）
            var (baseName, number) = ParseToolName(key);

            // 更新类型计数器为当前最大值，确保后续手动添加同类型工具时序号继续递增
            if (!_typeCounters.ContainsKey(baseName) || _typeCounters[baseName] < number)
            {
                _typeCounters[baseName] = number;
            }

            _keyToTypeName[key] = baseName;
            // 嵌套ToolBlock标记：加载到父级Tools后不再单独写.vpp，数据通过SaveToolParam嵌入父级文件
            if (value is ToolBlock tbLoad) tbLoad.IsNested = true;
            // 设置父级反向引用，供脚本工具等访问兄弟工具/父块
            value.Parent = Owner;
            base.Add(key, value);
        }

        /// <summary>
        /// 解析工具名，提取基础类型名和尾部序号
        /// 如 "ImageSourceTool1" → ("ImageSourceTool", 1)
        /// 如 "ImageSourceTool"  → ("ImageSourceTool", 0)
        /// </summary>
        private static (string baseName, int number) ParseToolName(string name)
        {
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i]))
            {
                i--;
            }
            if (i < name.Length - 1)
            {
                string baseName = name.Substring(0, i + 1);
                int number = int.Parse(name.Substring(i + 1));
                return (baseName, number);
            }
            return (name, 0);
        }
        public new bool Remove(string key)
        {
            if (ContainsKey(key))
            {
                string typeName = _keyToTypeName.ContainsKey(key) ? _keyToTypeName[key] : key;
                if (_typeCounters.ContainsKey(typeName) && _typeCounters[typeName] > 0)
                {
                    _typeCounters[typeName]--;
                }
                _keyToTypeName.Remove(key);
            }
            return base.Remove(key);
        }

        public new void Clear()
        {
            _typeCounters.Clear();
            _keyToTypeName.Clear();
            base.Clear();
        }
    }

    public class ToolBlock : ToolBase, IScriptHost
    {
        public ToolCollection Tools = new ToolCollection();
        private Dictionary<ToolBase, List<PortConnection>> connections = new Dictionary<ToolBase, List<PortConnection>>();
        public delegate void AddHandler(PortNode portNode);
        public event AddHandler AddInputEvent;
        public event AddHandler AddOutputEvent;
       // public ToolTreeviewControl toolTreeview;

        /// <summary>Run 正常完成后触发（所有执行路径都会走到）。
        /// 订阅者（ToolBlockControl 等）据此刷新图像窗口、叠加层网格、ComboBox 等 UI。
        /// 外部若跨线程调用 ToolBlock.Run，请在处理器里 BeginInvoke 回 UI 线程。</summary>
        public event EventHandler RunCompleted;
        /// <summary>
        /// 是否为嵌套ToolBlock(被加入另一ToolBlock的Tools集合)。
        /// true=不单独写.vpp文件，数据通过SaveToolParam嵌入父级文件统一保存；
        /// false=顶层ToolBlock(由ToolModule持有)，SaveTools写入自己的.vpp文件。
        /// </summary>
        public bool IsNested = false;

        // —— 形态2：ToolBlock 块级脚本 ——
        // 由 ToolBlockControl 的"脚本"按钮编辑；启用 UseScriptRun 后，ToolBlock.Run() 不再执行
        // 默认迭代逻辑，改由脚本接管（脚本可访问 Tools、兄弟工具、端口等全部公共成员）。
        private readonly ScriptExecutor _blockExecutor = new ScriptExecutor();
        private string _blockScriptText = string.Empty;
        private bool _useScriptRun = false;
        private bool _isBlockScriptRunning = false;   // 重入守卫
        private ScriptToolForm _blockScriptForm;

        public Dictionary<string, HObject> ToolImage = new Dictionary<string, HObject>();
        /// <summary>ToolImage 的 key → 产出该图像端口的所属工具。
        /// 与 ToolImage 同步填充（CollectToolImage），供 UI「仅显示当前工具」过滤使用。
        /// 注意：key 与 ToolImage 完全一致（端口名），同名端口会覆盖（既有行为，不在本任务范围）。</summary>
        public Dictionary<string, ToolBase> ToolImageOwner = new Dictionary<string, ToolBase>();
        public ToolBlock()
        {
            this.RootNode.Name = "ToolBlock";
            this.RootNode.Text = "ToolBlock";
            this.ToolName = "ToolBlock";
            this.RootNode.ImageIndex = 3;
            this.RootNode.SelectedImageIndex = 3;
            Tools.Clear();
            // 建立 ToolCollection → ToolBlock 反向引用，Add/Load 时据此设置子工具 Parent
            Tools.Owner = this;
            connections.Clear();
            //toolTreeview = new ToolTreeviewControl(this);

        }
        public override void Run()
        {
            GetInputValue();
            List<PortNode> ports = new List<PortNode>();
            // 形态2：启用脚本运行模式时，由脚本接管整个 Run（不再执行默认迭代）
            if (_useScriptRun && !string.IsNullOrWhiteSpace(_blockScriptText))
            {
                
                RunViaBlockScript();
                CollectAllToolsImages(ports);   // 收集脚本运行后各工具的输出图像 + 传播连线
                CollectToolImage(ports);
                OnRunCompleted();
                return;
            }

            
            foreach (ToolBase tool in Tools.Values)
            {
                tool.Run();
                ports.AddRange(tool.CollectImage());
                SetOutportValue(tool);
            }
            CollectToolImage(ports);
            OnRunCompleted();
        }

        /// <summary>触发 RunCompleted 事件（默认迭代与脚本运行模式两种路径都会调用）。</summary>
        protected virtual void OnRunCompleted()
        {
            RunCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>形态2：编译并执行块级脚本，传入 this 作为脚本实参（脚本可访问 Tools/端口等）。</summary>
        private void RunViaBlockScript()
        {
            if (_isBlockScriptRunning)
            {
                this.Message = "块级脚本正在运行，忽略重入调用";
                return;
            }
            _isBlockScriptRunning = true;
            try
            {
                var errors = new List<string>();
                if (!_blockExecutor.CompileIfChanged(_blockScriptText, out errors))
                {
                    this.IsRunSuccess = false;
                    this.Message = "脚本编译失败: " + string.Join("; ", errors);
                    return;
                }
                string result = _blockExecutor.RunCompiledScript(this);
                this.Message = result;
                this.IsRunSuccess = result != null && !result.StartsWith("脚本执行异常");
            }
            catch (Exception ex)
            {
                this.IsRunSuccess = false;
                this.Message = "脚本执行异常: " + ex.Message;
            }
            finally
            {
                _isBlockScriptRunning = false;
            }
        }
        public void CollectToolImage(List<PortNode> ports)
        {
            foreach(HObject hObject in ToolImage.Values)
            {
                hObject.Dispose();
            }
            ToolImage.Clear();
            ToolImageOwner.Clear();
            foreach (PortNode portNode in ports)
            {
                if (portNode.Value == null)
                    continue;
                HObject TempImage;
                HOperatorSet.CopyImage((HObject)portNode.Value, out TempImage);
                // 同名端口会覆盖（既有行为）；ToolImageOwner 用相同 key 同步覆盖，保持一致
                if (ToolImage.ContainsKey(portNode.Parent.Text+"."+portNode.Text))
                {
                    ToolImage[portNode.Parent.Text + "." + portNode.Text]?.Dispose();
                    ToolImage[portNode.Parent.Text + "." + portNode.Text] = (HObject)TempImage;
                    ToolImageOwner[portNode.Parent.Text + "." + portNode.Text] = portNode.BelongTool;
                }
                else
                {
                    ToolImage.Add(portNode.Parent.Text + "." + portNode.Text, (HObject)TempImage);
                    ToolImageOwner.Add(portNode.Parent.Text + "." + portNode.Text, portNode.BelongTool);
                }
            }
        }

        /// <summary>聚合所有内部工具的 IMAGE 输出端口到 ports，并按连线传播各工具输出值。
        /// 供脚本模式 Run 路径使用（脚本运行兄弟工具后，由本方法统一收集图像与传播连线），
        /// 行为与默认迭代路径中 per-tool 的 CollectImage + SetOutportValue 一致。</summary>
        public void CollectAllToolsImages(List<PortNode> ports)
        {
            if (Tools == null || ports == null) return;
            foreach (ToolBase tool in Tools.Values)
            {
                if (tool == null) continue;
                ports.AddRange(tool.CollectImage());
                SetOutportValue(tool);
            }
        }

        /// <summary>聚合所有内部工具的 DisplayItem（带所属工具引用，便于 UI 分组与「仅显示当前工具」过滤）。
        /// 运行时 Data 由各工具 Run() 填充；本方法仅收集引用，不拷贝 HObject。</summary>
        public List<(ToolBase Tool, DisplayItem Item)> CollectDisplayItems()
        {
            var list = new List<(ToolBase, DisplayItem)>();
            if (Tools == null) return list;
            foreach (ToolBase tool in Tools.Values)
            {
                if (tool?.DisplayItems == null) continue;
                foreach (var item in tool.DisplayItems.Values)
                    list.Add((tool, item));
            }
            return list;
        }
        public override void ShowWin()
        {
            // 销毁旧的ToolBlockControl（ShowDialog不会自动Dispose），确保旧的事件订阅被清理
            // Dispose会从旧窗体移除toolTreeview（不销毁它），此时toolTreeview暂无父控件
            //ToolBlockControl?.Dispose();
            // 先创建新窗体（构造函数会把toolTreeview添加到tabPage1，使其获得有效父控件和窗口句柄），
            // 再调用ReloadData。若在Dispose后、新窗体创建前调用ReloadData，toolTreeview无父控件，
            // 其句柄（及子控件ConnectionTreeView的句柄）可能已销毁，EndUpdate触发句柄重建时
            // WM_PAINT会让base.WndProc访问未初始化的TreeView内部状态，抛NullReferenceException。
            //ToolBlockControl = new ToolBlockControl(this);
            // 清空toolTreeview旧节点并从最新ToolBlock数据重新加载，避免重复打开时节点重复添加
            // 注意：toolTreeview使用独立的克隆端口节点显示（见ToolTreeviewControl.AddInput/AddOutput），
            // canonical端口始终保留在RootNode中（父级树视图），两个树视图互不干扰。
            //toolTreeview?.ReloadData();
            //ToolBlockControl.ShowDialog();
            //SaveTools();
        }

        /// <summary>
        /// 释放ToolBlock持有的所有关联资源：窗体、内部工具、端口、连线、事件订阅、显示缓存
        /// </summary>
        public override void Dispose()
        {
            // 0. 销毁可能仍打开的块级脚本编辑器窗体（形态2）
            _blockScriptForm?.Dispose();
            _blockScriptForm = null;

            // 1. 销毁可能仍存在（已关闭未释放）的ToolBlockControl窗体，
            //    其Dispose会取消事件订阅并从窗体移除toolTreeview（不销毁toolTreeview）
            //ToolBlockControl?.Dispose();
            //ToolBlockControl = null;

            // 2. 释放toolTreeview（由ToolBlock管理生命周期，窗体关闭时不销毁它）
            //    其Dispose会取消订阅ToolBlock事件并清空TreeView节点
            //toolTreeview?.Dispose();
            //toolTreeview = null;

            // 3. 释放ToolBlock自身显示缓存 ToolImage 的 HObject
            if (ToolImage != null)
            {
                foreach (var ho in ToolImage.Values)
                {
                    try { if (ho is HObject h && h.IsInitialized()) h.Dispose(); } catch { }
                }
                ToolImage.Clear();
            }
            // ToolImageOwner 仅持有工具引用（不持有 HObject），工具释放见步骤4；这里只清字典
            ToolImageOwner?.Clear();

            // 4. 清空并释放内部工具（递归释放嵌套的ToolBlock；ToolBase.Dispose释放其端口HObject）
            if (Tools != null)
            {
                foreach (var tool in Tools.Values)
                {
                    tool?.OnDeleted();
                    tool?.Dispose();
                }
                Tools.Clear();
            }

            // 5. 清空连线（端口Value的释放交给base.Dispose统一处理）
            connections?.Clear();

            // 6. 释放本ToolBlock自身端口的HObject Value并清空Inputs/Outputs/Parameters/RootNode
            base.Dispose();

            // 7. 清除所有外部事件订阅者，避免悬挂回调
            AddInputEvent = null;
            AddOutputEvent = null;
        }
        /// <summary>
        /// 生成不重复的端口名：若baseName已存在，则在末尾追加序号(2,3,...)用于区分。
        /// </summary>
        internal static string GetUniquePortName(Dictionary<string, PortNode> existing, string baseName)
        {
            return GetUniqueName(existing, baseName, "Port");
        }

        /// <summary>
        /// 通用名称去重：若baseName已存在于字典，则追加序号(2,3,...)。
        /// 供端口(PortNode)与显示项(DisplayItem)共用，避免重复实现。
        /// </summary>
        internal static string GetUniqueName<T>(Dictionary<string, T> existing, string baseName, string fallback = "Item")
        {
            if (string.IsNullOrEmpty(baseName))
                baseName = fallback;
            if (!existing.ContainsKey(baseName))
                return baseName;
            int counter = 2;
            while (existing.ContainsKey(baseName + counter))
                counter++;
            return baseName + counter;
        }

        // 四个运行状态属性的名字（旧实现误作为 SINGAL 端口持久化，需要过滤）
        private static readonly HashSet<string> GhostPortNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "IsRunSuccess", "Message", "TotalTime", "Result"
        };

        /// <summary>判断端口是否为旧版本误保存的 SINGAL 幽灵端口，加载 .vpp 时跳过。
        /// 仅当"名称属于四个状态属性名 且 类型 == SINGAL"时匹配，最大限度避免误删。
        /// internal 供 ToolBase.LoadToolParam 复用过滤逻辑（ScriptTool 等非 ToolBlock 工具也需要）。</summary>
        internal static bool IsLegacyGhostPort(string portName, string portTypeStr)
        {
            if (string.IsNullOrEmpty(portName) || string.IsNullOrEmpty(portTypeStr)) return false;
            if (!GhostPortNames.Contains(portName)) return false;
            return portTypeStr == TypeName.SINGAL.ToString();
        }

        public override bool AddInput(string portName, TypeName portType)
        {
            if (RootNode == null)
                return false;
            // 名称去重：已存在同名端口时自动追加序号
            portName = GetUniquePortName(Inputs, portName);
            PortNode portNode = new PortNode(
                text: portName,
                portType: portType,
                direction: "Output",
                belongTool: this,
                portName: portName,
                value: null
            );
            // 外部父级树视图(RootNode下)交换输入/输出端口图标：输入端口用 ImageIndex 2(Output.png)。
            // 注意：direction保持"Output"不变(连接层依赖此方向)，仅交换显示图标。
            // 内部编辑器树的克隆端口图标由ClonePort按direction独立设置，不受此处影响。
            portNode.ImageIndex = 2;
            portNode.SelectedImageIndex = 2;
            // 外部父级树视图：输入端口置于顶部(插入到第一个输出端口 direction=="Input" 之前)，
            // 输出端口置于底部(AddOutput中追加)，使外部视图呈"输入在上、输出在下"。
            int insertIndex = RootNode.Nodes.Count;
            for (int i = 0; i < RootNode.Nodes.Count; i++)
            {
                if (RootNode.Nodes[i] is PortNode p && p.Direction == "Input")
                {
                    insertIndex = i;
                    break;
                }
            }
            RootNode.Nodes.Insert(insertIndex, portNode);
            Inputs[portName] = portNode;
            AddInputEvent?.Invoke(portNode);
            return true;
        }
        public override bool AddOutput(string portName, TypeName portType)
        {
            if (RootNode == null)
                return false;
            // 名称去重：已存在同名端口时自动追加序号
            portName = GetUniquePortName(Outputs, portName);
            PortNode portNode = new PortNode(
                text: portName,
                portType: portType,
                direction: "Input",
                belongTool: this,
                portName: portName,
                value: null
            );
            // 外部父级树视图(RootNode下)交换输入/输出端口图标：输出端口用 ImageIndex 1(Input.png)。
            portNode.ImageIndex = 1;
            portNode.SelectedImageIndex = 1;
            // 外部父级树视图：输出端口置于底部(追加)，输入端口置于顶部(AddInput中插入)。
            RootNode.Nodes.Add(portNode);
            Outputs[portName] = portNode;
            AddOutputEvent?.Invoke(portNode);
            return true;
        }
        private void SetOutportValue(ToolBase tool)
        {
            if (!connections.ContainsKey(tool))
                return;
            foreach (PortConnection port in connections[tool])
            {

                if (port.ToTool is HToolBase.Tools.ToolBlock)
                {
                    port.ToTool.Outputs[port.ToPort.Text].Value = port.FromTool.Outputs[port.FromPort.Text].Value;
                }
                else
                {
                    port.ToTool.Inputs[port.ToPort.Text].Value = port.FromTool.Outputs[port.FromPort.Text].Value;
                }
            }
        }
        private void GetInputValue()
        {
            if (!connections.ContainsKey(this))
                return;
            foreach (PortConnection port in connections[this])
            {
                if (port.ToTool == port.FromTool)
                    port.ToTool.Outputs[port.ToPort.Text].Value = port.FromTool.Inputs[port.FromPort.Text].Value;
                else
                    port.ToTool.Inputs[port.ToPort.Text].Value = port.FromTool.Inputs[port.FromPort.Text].Value;
            }
        }
        public bool ConnectPort(ToolBase FromTool, PortNode FromPort, ToolBase ToTool, PortNode ToPort)
        {
            if (FromTool == null || FromPort == null || ToTool == null || ToPort == null)
                return false;
            if (FromPort.PortType != ToPort.PortType)
                return false;

            DisconnectPort(FromTool, FromPort, ToTool, ToPort);

            if (connections.ContainsKey(FromTool))
            {
                foreach (PortConnection port in connections[FromTool])
                {
                    if (port.FromPort == FromPort && port.ToTool == ToTool && port.ToPort == ToPort)
                        return false;
                }
                PortConnection p = new PortConnection();
                p.FromTool = FromTool;
                p.FromPort = FromPort;
                p.PortType = FromPort.PortType;
                p.ToTool = ToTool;
                p.ToPort = ToPort;
                connections[FromTool].Add(p);
                return true;
            }
            else
            {
                PortConnection p = new PortConnection();
                p.FromTool = FromTool;
                p.FromPort = FromPort;
                p.PortType = FromPort.PortType;
                p.ToTool = ToTool;
                p.ToPort = ToPort;
                connections.Add(FromTool, new List<PortConnection>());
                connections[FromTool].Add(p);
                return true;
            }
        }

        public void DisconnectPort(ToolBase fromTool, PortNode fromPort, ToolBase toTool, PortNode toPort)
        {
            if (connections.ContainsKey(fromTool))
            {
                connections[fromTool].RemoveAll(p =>
                    p.FromPort == fromPort && p.ToTool == toTool && p.ToPort == toPort);

                if (connections[fromTool].Count == 0)
                    connections.Remove(fromTool);
            }
        }

        public void DisconnectPortByTarget(ToolBase toTool, PortNode toPort)
        {
            var keysToRemove = new List<ToolBase>();
            foreach (var kvp in connections)
            {
                kvp.Value.RemoveAll(p => p.ToTool == toTool && p.ToPort == toPort);
                if (kvp.Value.Count == 0)
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var key in keysToRemove)
                connections.Remove(key);
        }

        /// <summary>
        /// 移除所有涉及指定端口的连线（该端口作为源FromPort或目标ToPort均移除）。
        /// 在删除输入/输出端口前调用，确保连线不残留。
        /// </summary>
        public void DisconnectPortByPort(PortNode port)
        {
            if (port == null || connections == null) return;
            var keysToRemove = new List<ToolBase>();
            foreach (var kvp in connections)
            {
                kvp.Value.RemoveAll(p => p.FromPort == port || p.ToPort == port);
                if (kvp.Value.Count == 0)
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var key in keysToRemove)
                connections.Remove(key);
        }
        [Obsolete]
        public object RefreshInput(string Name)
        {
            return this.Inputs[Name].Value;
        }
        [Obsolete]
        public object RefreshOutput(string Name)
        {
            return this.Outputs[Name].Value;
        }
        /// <summary>
        /// 保存顶层ToolBlock到独立.vpp文件。
        /// 嵌套ToolBlock(IsNested=true)不单独写文件——其数据通过SaveToolParam嵌入父级文件统一保存。
        /// </summary>
        public void SaveTools(bool showMessage = true)
        {
            // 嵌套ToolBlock不单独写文件，数据随父级SaveToolParam嵌入父级.vpp
            if (IsNested) return;
            try
            {
                var saveData = BuildSaveData();
                // 序列化保存（按当前产品分组：System/{产品}/{ProcessPanel名}/{ToolName}.vpp）
                string filePath = JsonDynamicHelper.GetSafeFilePath(ProductManager.GetProcessDir(), this.ToolName + ".vpp");
                string json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ToolBlock[{this.ToolName}]保存失败：{ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 重写SaveToolParam：在基类字段(Tool类型/名称/端口)基础上，嵌入完整ToolBlock数据
        /// (内部工具、自身端口、连线)，使嵌套ToolBlock像普通工具一样随父级文件统一保存。
        /// </summary>
        public override Dictionary<string, DynamicItem> SaveToolParam()
        {
            var dict = base.SaveToolParam();
            dict["ToolBlockData"] = new DynamicItem(BuildSaveData());
            return dict;
        }

        /// <summary>
        /// 重写LoadToolParam：从父级文件嵌入的ToolBlockData恢复完整数据。
        /// 兼容旧格式：父级文件未嵌入ToolBlockData时，回退读取单独的.vpp文件(迁移已有数据)。
        /// </summary>
        public override void LoadToolParam(Dictionary<string, DynamicItem> paramDict)
        {
            if (paramDict == null) return;
            // 先从保存数据恢复ToolName，确保RestoreFromSaveData中LookupTool能正确识别本ToolBlock自身。
            // 嵌套ToolBlock的ToolName由父级Tools.Load在LoadToolParam返回后才设置，但RestoreFromSaveData
            // 内的连线恢复(第5步)需要通过ToolName查找本ToolBlock(如"ToolBlock1输出→父级输出"连线，
            // ToToolName=父级名)，若此时ToolName仍为构造函数默认值"ToolBlock"，LookupTool将返回null，
            // 导致该连线被静默丢弃。提前恢复ToolName可避免此问题。
            if (paramDict.TryGetValue("ToolName", out DynamicItem nameItem) && nameItem?.Value != null)
                this.ToolName = nameItem.Value.ToString();
            if (paramDict.TryGetValue("ToolBlockData", out DynamicItem dataItem) && dataItem?.Value != null)
            {
                ToolSaveData saveData = null;
                if (dataItem.Value is ToolSaveData direct)
                    saveData = direct;
                else if (dataItem.Value is JObject jobj)
                    saveData = jobj.ToObject<ToolSaveData>();
                if (saveData != null)
                    RestoreFromSaveData(saveData);
            }
            else
            {
                // 兼容旧格式：父级文件未嵌入ToolBlockData，回退读取单独.vpp文件
                LoadVpp();
            }
        }

        /// <summary>
        /// 构建本ToolBlock的完整保存数据(内部工具、自身端口、连线)。
        /// 供SaveToolParam(嵌入父级)与SaveTools(写顶层文件)共用。
        /// </summary>
        private ToolSaveData BuildSaveData()
        {
            var saveData = new ToolSaveData();

            // 1. 保存内部工具（嵌套ToolBlock会递归嵌入其ToolBlockData）
            foreach (HToolBase.ToolBase tool in this.Tools.Values)
            {
                saveData.Tools.Add(tool.SaveToolParam());
            }

            // 2. 保存ToolBlock自身的输入端口
            foreach (PortNode port in this.Inputs.Values)
            {
                // HObject 类型端口跳过 Value 序列化（运行时数据，由 Run() 重新生成）
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

            // 4. 保存连线（用工具名+端口名引用）
            foreach (var kvp in connections)
            {
                foreach (PortConnection conn in kvp.Value)
                {
                    saveData.Connections.Add(new ConnectionSaveData
                    {
                        FromToolName = conn.FromTool.ToolName,
                        FromPortName = conn.FromPort.PortName,
                        ToToolName = conn.ToTool.ToolName,
                        ToPortName = conn.ToPort.PortName,
                        PortType = conn.PortType.ToString()
                    });
                }
            }

            // 5. 保存形态2块级脚本（文本 + 运行模式开关）
            saveData.BlockScriptText = this._blockScriptText;
            saveData.UseScriptRun = this._useScriptRun;
            return saveData;
        }

        /// <summary>
        /// 从保存数据恢复ToolBlock(清空旧数据→内部工具→自身端口→连线)。
        /// 供LoadVpp(顶层文件)与LoadToolParam(父级嵌入)共用。
        /// </summary>
        private void RestoreFromSaveData(ToolSaveData saveData)
        {
            if (saveData == null) return;

            // 1. 清空旧数据
            this.Tools.Clear();
            this.Inputs.Clear();
            this.Outputs.Clear();
            // 重建RootNode（而非Nodes.Clear()），避免上一会话残留的端口TreeNode
            // 仍关联已Dispose的旧TreeView句柄，导致Clear()向死句柄发消息而卡死
            this.RootNode = new TreeNode();
            this.RootNode.Name = "ToolBlock";
            this.RootNode.Text = "ToolBlock";
            // 图标与构造函数保持一致(ImageIndex/SelectedImageIndex=3, CogToolBlock.ico)，
            // 否则重建后的RootNode图标为默认值0，加载后树视图显示错误图标
            this.RootNode.ImageIndex = 3;
            this.RootNode.SelectedImageIndex = 3;
            connections.Clear();

            // 2. 加载内部工具（嵌套ToolBlock会递归通过LoadToolParam恢复）
            foreach (var toolDict in saveData.Tools)
            {
                if (!toolDict.TryGetValue("Tool", out DynamicItem typeItem))
                    continue;
                string typeFullStr = typeItem.Value.ToString();

                Type toolType = Type.GetType(typeFullStr);
                if (toolType == null || !typeof(HToolBase.ToolBase).IsAssignableFrom(toolType))
                {
                    MessageBox.Show($"无法识别工具类型：{typeFullStr}");
                    continue;
                }

                HToolBase.ToolBase toolInstance = Activator.CreateInstance(toolType) as HToolBase.ToolBase;
                if (toolInstance == null) continue;

                toolInstance.LoadToolParam(toolDict);

                string toolKey = toolDict["ToolName"].Value.ToString();
                this.Tools.Load(toolKey, toolInstance);
            }

            // 3. 加载ToolBlock自身输入端口
            foreach (var portData in saveData.ToolInputs)
            {
                // 向后兼容：过滤旧版本自动创建的 SINGAL 幽灵端口（IsRunSuccess/Message/TotalTime/Result）。
                // 这些端口是旧 ToolBase.SetPortValue 在属性赋值时自动 new PortNode() 产生的（无参构造→
                // PortType=default SINGAL），保存后每次加载又被还原，导致 ToolBlock 持续多出 Single 端口。
                if (IsLegacyGhostPort(portData.Name, portData.PortType))
                    continue;

                TypeName portType = (TypeName)Enum.Parse(typeof(TypeName), portData.PortType);
                // 先计算去重后的实际名称，AddInput内部也会去重，此处确保值能正确恢复到对应端口
                string actualName = GetUniquePortName(Inputs, portData.Name);
                AddInput(actualName, portType);
                if (portData.Value != null && Inputs.TryGetValue(actualName, out var inPort))
                {
                    inPort.Value = portData.Value.GetRealValue();
                }
            }

            // 4. 加载ToolBlock自身输出端口
            foreach (var portData in saveData.ToolOutputs)
            {
                if (IsLegacyGhostPort(portData.Name, portData.PortType))
                    continue;

                TypeName portType = (TypeName)Enum.Parse(typeof(TypeName), portData.PortType);
                string actualName = GetUniquePortName(Outputs, portData.Name);
                AddOutput(actualName, portType);
                if (portData.Value != null && Outputs.TryGetValue(actualName, out var outPort))
                {
                    outPort.Value = portData.Value.GetRealValue();
                }
            }

            // 5. 加载连线（通过工具名和端口名查找实际对象）
            foreach (var connData in saveData.Connections)
            {
                ToolBase fromTool = LookupTool(connData.FromToolName);
                ToolBase toTool = LookupTool(connData.ToToolName);
                PortNode fromPort = LookupPort(fromTool, connData.FromPortName);
                PortNode toPort = LookupPort(toTool, connData.ToPortName);

                if (fromTool != null && toTool != null && fromPort != null && toPort != null)
                {
                    ConnectPort(fromTool, fromPort, toTool, toPort);
                }
            }

            // 6. 恢复形态2块级脚本（文本 + 运行模式开关）
            this._blockScriptText = saveData.BlockScriptText ?? string.Empty;
            this._useScriptRun = saveData.UseScriptRun;
            _blockExecutor.Invalidate();
        }
        public void LoadVpp(string FileName)
        {
            try
            {
                
                if (!File.Exists(FileName))
                {
                    // 首次打开无配置文件属正常情况，静默返回
                    return;
                }

                // 3. 释放ToolBlock自身显示缓存 ToolImage 的 HObject
                if (ToolImage != null)
                {
                    foreach (var ho in ToolImage.Values)
                    {
                        try { if (ho is HObject h && h.IsInitialized()) h.Dispose(); } catch { }
                    }
                    ToolImage.Clear();
                }
                // ToolImageOwner 仅持有工具引用（不持有 HObject），工具释放见步骤4；这里只清字典
                //ToolImageOwner?.Clear();

                // 4. 清空并释放内部工具（递归释放嵌套的ToolBlock；ToolBase.Dispose释放其端口HObject）
                if (Tools != null)
                {
                    foreach (var tool in Tools.Values)
                    {
                        tool?.OnDeleted();
                        tool?.Dispose();
                    }
                    Tools.Clear();
                }

                // 5. 清空连线（端口Value的释放交给base.Dispose统一处理）
                connections?.Clear();

                // 6. 释放本ToolBlock自身端口的HObject Value并清空Inputs/Outputs/Parameters/RootNode
                base.Dispose();

                // 7. 清除所有外部事件订阅者，避免悬挂回调
                AddInputEvent = null;
                AddOutputEvent = null;



                string json = File.ReadAllText(FileName, Encoding.UTF8);
                ToolSaveData saveData;

                // 兼容旧格式（JSON数组）和新格式（JSON对象）
                json = json.TrimStart();
                if (json.StartsWith("["))
                {
                    // 旧格式：仅有工具列表
                    var oldToolList = JsonConvert.DeserializeObject<List<Dictionary<string, DynamicItem>>>(json);
                    saveData = new ToolSaveData { Tools = oldToolList ?? new List<Dictionary<string, DynamicItem>>() };
                }
                else
                {
                    // 新格式：完整保存数据
                    saveData = JsonConvert.DeserializeObject<ToolSaveData>(json);
                }

                if (saveData == null)
                {
                    MessageBox.Show("配置文件数据为空");
                    return;
                }

                RestoreFromSaveData(saveData);
                //toolTreeview?.ReloadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载VPP失败：{ex.Message}\n{ex.StackTrace}");
            }
        }
        public void LoadVpp()
        {
            try
            {
                // 按当前产品分组加载
                string filePath = JsonDynamicHelper.GetSafeFilePath(ProductManager.GetProcessDir(), this.ToolName + ".vpp");
                if (!File.Exists(filePath))
                {
                    // 首次打开无配置文件属正常情况，静默返回
                    return;
                }

                string json = File.ReadAllText(filePath, Encoding.UTF8);
                ToolSaveData saveData;

                // 兼容旧格式（JSON数组）和新格式（JSON对象）
                json = json.TrimStart();
                if (json.StartsWith("["))
                {
                    // 旧格式：仅有工具列表
                    var oldToolList = JsonConvert.DeserializeObject<List<Dictionary<string, DynamicItem>>>(json);
                    saveData = new ToolSaveData { Tools = oldToolList ?? new List<Dictionary<string, DynamicItem>>() };
                }
                else
                {
                    // 新格式：完整保存数据
                    saveData = JsonConvert.DeserializeObject<ToolSaveData>(json);
                }

                if (saveData == null)
                {
                    MessageBox.Show("配置文件数据为空");
                    return;
                }

                RestoreFromSaveData(saveData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载VPP失败：{ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 根据工具名查找工具实例（ToolBlock自身或内部工具）
        /// </summary>
        private ToolBase LookupTool(string toolName)
        {
            if (toolName == this.ToolName)
                return this;
            if (Tools.TryGetValue(toolName, out var tool))
                return tool;
            return null;
        }

        /// <summary>
        /// 根据端口名查找端口节点（在工具的输入或输出端口中查找）
        /// </summary>
        private PortNode LookupPort(ToolBase tool, string portName)
        {
            if (tool == null) return null;
            if (tool.Inputs.TryGetValue(portName, out var inputPort))
                return inputPort;
            if (tool.Outputs.TryGetValue(portName, out var outputPort))
                return outputPort;
            return null;
        }

        /// <summary>
        /// 获取所有连线的可序列化数据（供ToolTreeviewControl恢复连线使用）
        /// </summary>
        public List<PortConnection> GetAllConnections()
        {
            var result = new List<PortConnection>();
            foreach (var kvp in connections)
            {
                result.AddRange(kvp.Value);
            }
            return result;
        }

        #region 形态2：IScriptHost 实现（ToolBlock 块级脚本）
        ScriptExecutor IScriptHost.Executor => _blockExecutor;
        object IScriptHost.ScriptArgument => this;
        string IScriptHost.HostTitle => "ToolBlock 脚本 - " + this.ToolName;
        bool IScriptHost.CanSwitchRunMode => true;
        bool IScriptHost.CanManagePorts => false;   // 形态2不支持端口增删（端口由"输入/输出"标签页管理）

        string IScriptHost.ScriptText
        {
            get => _blockScriptText;
            set
            {
                if (_blockScriptText != value)
                {
                    _blockScriptText = value ?? string.Empty;
                    _blockExecutor.Invalidate();
                }
            }
        }

        bool IScriptHost.UseScriptRun
        {
            get => _useScriptRun;
            set => _useScriptRun = value;
        }

        IEnumerable<string> IScriptHost.GetCreatedNames() => Tools.Keys;

        IEnumerable<PortNode> IScriptHost.GetPorts(bool isInput)
            => isInput ? Inputs.Values : Outputs.Values;

        void IScriptHost.AddPort(bool isInput, TypeName type) { /* 形态2不支持 */ }
        void IScriptHost.RemovePort(string portName, bool isInput) { /* 形态2不支持 */ }

        string IScriptHost.GetExampleScript() => GetBlockExampleScript();

        /// <summary>由 ToolBlockControl 的"脚本"按钮调用：打开块级脚本编辑器（形态2）。</summary>
        public void ShowScriptEditor()
        {
            _blockScriptForm?.Dispose();
            _blockScriptForm = new ScriptToolForm(this);
            _blockScriptForm.FormClosed += (s, e) => _blockScriptForm = null;
            _blockScriptForm.Show();
        }

        /// <summary>形态2示例脚本：访问整个 ToolBlock 与兄弟工具。</summary>
        internal static string GetBlockExampleScript()
        {
            return @"using System;
using System.Collections.Generic;
using HalconDotNet;
using HToolBase;
using HToolBase.Tools;

public class ToolScript
{
    private ToolBlock tool;
    public ToolScript(ToolBlock _tool) { tool = _tool; }

    public string Run()
    {
        try
        {
            // 运行指定兄弟工具（避免运行本块脚本自身导致递归——本块脚本由 tool.Run 触发，
            // 脚本内若再调 tool.Run 会因 UseScriptRun 再次进入脚本，被重入守卫拦截）
            if (tool.Tools.ContainsKey(""ImageSourceTool1""))
            {
                ToolBase img = tool.Tools[""ImageSourceTool1""];
                img.Run();
                // 读取端口值
                // HObject image = (HObject)img.Outputs[""OutputImage""].Value;
            }

            // 也可遍历运行全部兄弟工具
            // foreach (ToolBase t in tool.Tools.Values) t.Run();

            tool.IsRunSuccess = true;
            tool.Message = ""块级脚本执行成功"";
            return ""块级脚本执行成功"";
        }
        catch (Exception ex)
        {
            tool.IsRunSuccess = false;
            tool.Message = ex.Message;
            return ""块级脚本执行失败: "" + ex.Message;
        }
    }
}";
        }
        #endregion
    }
}
