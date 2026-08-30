# Region/XLD 统一显示管理 — 实施计划

## 摘要

为工具系统添加一套**独立于端口**的 Region/XLD 显示叠加层管理机制。每个工具通过内部函数发布带显示配置（可见性/颜色/Draw 模式/线宽）的 Region/XLD 显示项；ToolBlock 运行后聚合所有工具的显示项；HDisplayControl 维护多叠加层列表并在平移/缩放/重绘时统一渲染；ToolBlockControl 提供统一 UI 管理所有显示项的配置，并支持「仅显示当前 ComboBox 所选工具」的快捷过滤。配置随 `.vpp` 持久化（仅配置，不含 HObject 数据）。

## 当前状态分析

### 已实现
- [ToolBase.CollectImage()](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs#L325-L336) 仅收集 `TypeName.IMAGE` 输出端口
- [ToolBlock.Run()](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L150-L162) 聚合图像端口 → `CollectToolImage` → `ToolImage` 字典（键=端口名）
- [comboBox1_SelectedIndexChanged](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Controls/ToolBlockControl.cs#L464-L473) 调用 `ShowImage` 后**硬编码** `GenRectangle1(200,200,800,800)` + `SetRegion(r)` 作为占位演示
- [HDisplayControl.SetRegion](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Controls/HDisplayControl.cs#L211-L221) 仅支持**单个** region 叠加，无 XLD 支持，无线宽设置
- [HDisplayControl.SetView()](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Controls/HDisplayControl.cs#L152-L169) 重绘时只 `DispObj(image)` + `DispObj(region)`（单个 region）

### 缺失
- 无 Region/XLD 显示项的数据模型（独立于端口）
- 无工具内部发布显示项的 API
- HDisplayControl 无多叠加层 / XLD / 线宽支持
- 无统一管理 UI
- 无配置持久化

## 用户确认的关键决策

| 决策点 | 选择 |
|--------|------|
| 显示耦合 | 两者都要：默认显示所有已启用工具，提供「仅显示当前工具」快捷开关 |
| 配置粒度 | **独立于端口**：Region/XLD 不是工具输出端口，由工具内部函数添加/修改 |
| 持久化 | 保存到 `.vpp`（仅配置，HObject 数据运行时由 Run() 重新生成） |

## 提议变更

### 1. 新增 `DisplayItem` 数据模型 — `ToolBase.cs`

在 `TypeName` 枚举下方新增类，承载单个显示项的配置 + 运行时数据：

```csharp
public class DisplayItem
{
    public string Name { get; set; }               // 工具内唯一名，如 "FoundRegion"
    public TypeName Type { get; set; }             // REGION 或 LINE
    public bool Visible { get; set; } = true;
    public string Color { get; set; } = "red";
    public string Draw { get; set; } = "margin";   // region: "fill"/"margin"；xld 忽略
    public double LineWidth { get; set; } = 1.0;

    // 运行时数据（不持久化）。setter 深拷贝 + 释放旧值，与 PortNode.Value 一致。
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
            else _data = value;
        }
    }

    public DisplayItem CloneConfig() => new DisplayItem
    {
        Name = Name, Type = Type, Visible = Visible,
        Color = Color, Draw = Draw, LineWidth = LineWidth
        // 不拷贝 Data
    };
}
```

**为什么**：`Data` setter 深拷贝遵循 [project_memory 中 HObject 所有权不变式](file:///c:/Users/Administrator/.trae-cn/memory/projects/-c-Users-Administrator-source-repos-HVisoion-Hal--p2-b848f4670f3a689454d0/project_memory.md)（CopyObj Index=1, Number=-1，覆盖 HRegion/HXLD 全部 iconic 类型），调用方赋值后须自行 Dispose 临时 HObject。

### 2. ToolBase 新增显示项字典 + 内部 API — `ToolBase.cs`

在 `ToolBase` 类中新增字段与 protected 方法：

```csharp
/// <summary>本工具发布的 Region/XLD 显示项（按 Name 索引）。配置持久化，Data 运行时填充。</summary>
public Dictionary<string, DisplayItem> DisplayItems { get; } = new Dictionary<string, DisplayItem>();

protected void AddDisplayRegion(string name, HObject region,
    string color = "red", string draw = "margin", double lineWidth = 1.0)
{
    if (string.IsNullOrEmpty(name)) name = "Region";
    name = ToolBlock.GetUniquePortName(DisplayItems, name);  // 复用去重逻辑
    var item = new DisplayItem { Name = name, Type = TypeName.REGION,
        Color = color, Draw = draw, LineWidth = lineWidth, Data = region };
    DisplayItems[name] = item;
}

protected void AddDisplayXLD(string name, HObject xld,
    string color = "yellow", double lineWidth = 1.0)
{
    if (string.IsNullOrEmpty(name)) name = "XLD";
    name = ToolBlock.GetUniquePortName(DisplayItems, name);
    var item = new DisplayItem { Name = name, Type = TypeName.LINE,
        Color = color, Draw = "margin", LineWidth = lineWidth, Data = xld };
    DisplayItems[name] = item;
}

/// <summary>仅更新已存在显示项的运行时数据（Run() 中每帧调用以刷新结果）。</summary>
protected void UpdateDisplayData(string name, HObject data)
{
    if (DisplayItems.TryGetValue(name, out var item))
        item.Data = data;
}

protected bool RemoveDisplayItem(string name)
{
    if (DisplayItems.TryGetValue(name, out var item))
    {
        if (item.Data is HObject h && h.IsInitialized()) { try { h.Dispose(); } catch { } }
        return DisplayItems.Remove(name);
    }
    return false;
}

protected void ClearDisplayItems()
{
    foreach (var item in DisplayItems.Values)
        if (item.Data is HObject h && h.IsInitialized()) { try { h.Dispose(); } catch { } }
    DisplayItems.Clear();
}
```

**为什么独立于端口**：用户明确要求 Region/XLD 不是工具输出端口。这套 API 让工具在 `Run()` 中以代码方式发布中间结果（如阈值区域、检测到的边缘 XLD），不占用端口、不参与连线。

**ToolBase.Dispose 释放**：在 [ToolBase.Dispose()](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs#L302-L310) 末尾追加 `ClearDisplayItems()` 调用，确保工具销毁时释放所有 Data HObject。

### 3. ToolBlock 聚合显示项 + 图像归属映射 — `ToolBlock.cs`

```csharp
/// <summary>ComboBox 选中图像 → 所属工具 的映射（与 ToolImage 同步填充）</summary>
public Dictionary<string, ToolBase> ToolImageOwner { get; } = new Dictionary<string, ToolBase>();

/// <summary>聚合所有内部工具的 DisplayItem（带工具名前缀，便于 UI 分组）</summary>
public List<(ToolBase Tool, DisplayItem Item)> CollectDisplayItems()
{
    var list = new List<(ToolBase, DisplayItem)>();
    foreach (ToolBase tool in Tools.Values)
        foreach (var item in tool.DisplayItems.Values)
            list.Add((tool, item));
    return list;
}
```

修改 [CollectToolImage](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L163-L177)：填充 `ToolImage` 时**同步**填充 `ToolImageOwner`（key 相同）。需要 `ports` 元素能反查所属工具——`CollectImage()` 当前只返回 `PortNode`，`PortNode.BelongTool` 字段已存在，可直接用。

修改 [Run()](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L150-L162)：Run 结束后触发一次显示刷新回调（见下文）。

修改 [Dispose()](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L200-L242)：在步骤 4 释放内部工具前，先清空 `ToolImageOwner`（HObject 由工具自身 Dispose 释放，这里只清字典引用）。

### 4. HDisplayControl 多叠加层渲染 — `HDisplayControl.cs`

替换单 `region` 字段为列表，新增 `SetOverlays` API：

```csharp
private List<DisplayItem> _overlays = new List<DisplayItem>();

/// <summary>替换全部叠加层（深拷贝 Data）。传 null/空列表则清空。</summary>
public void SetOverlays(IEnumerable<DisplayItem> items)
{
    foreach (var o in _overlays)
        if (o.Data is HObject h && h.IsInitialized()) { try { h.Dispose(); } catch { } }
    _overlays.Clear();
    if (items == null) { SetView(); return; }
    foreach (var src in items)
    {
        // 深拷贝 Data，断开与 ToolBlock 持有引用的共享
        var copy = src.CloneConfig();
        if (src.Data is HObject d && d.IsInitialized())
        {
            HOperatorSet.CopyObj(d, out HObject c, 1, -1);
            copy.Data = c;        // setter 会再拷贝一次，可优化为直接赋 _data
        }
        _overlays.Add(copy);
    }
    SetView();
}
```

> **优化备注**：`CloneConfig()` 不拷 Data，`SetOverlays` 手动 CopyObj 后赋给 `copy.Data`，setter 又拷贝一次（两次拷贝）。可在 `DisplayItem` 增加 `SetDataRaw(HObject)` 内部方法跳过 setter 二次拷贝。初版先求正确，后续优化。

修改 [SetView()](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Controls/HDisplayControl.cs#L152-L169)：在 `DispObj(image)` 后遍历 `_overlays`，对每个 `Visible && Data.IsInitialized()` 的项：
```csharp
foreach (var o in _overlays)
{
    if (!o.Visible || o.Data == null || !o.Data.IsInitialized()) continue;
    HalconWindow.SetLineWidth(o.LineWidth);
    HalconWindow.SetColor(o.Color);
    if (o.Type == TypeName.REGION)
        HalconWindow.SetDraw(o.Draw);   // "fill"/"margin"
    // XLD 忽略 SetDraw（HALCON 对 XLDCont 无 fill 概念）
    HalconWindow.DispObj(o.Data);
}
```

保留旧 `SetRegion` 方法但标记 `[Obsolete]`（向后兼容，内部转调 `SetOverlays` 单项）。移除 [comboBox1_SelectedIndexChanged](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Controls/ToolBlockControl.cs#L464-L473) 中的硬编码 `GenRectangle1` 演示代码。

修改 [Dispose(bool)](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Controls/HDisplayControl.Designer.cs#L14-L25)：释放 `_overlays` 中所有 Data HObject，再 `base.Dispose`。

**XLD 渲染说明**：`HXLD`/`HXLDCont` 均继承自 `HObject`，`HalconWindow.DispObj` 直接支持，无需特殊处理。`SetLineWidth` 对 region 边界与 XLD 均生效。

### 5. ToolBlockControl 统一管理 UI — `ToolBlockControl.cs` + `.Designer.cs`

在 `tabPage2`（输入/输出页）旁新增 `tabPage3`「显示叠加层」，或直接在现有页面底部加一个 `DataGridView overlayGridView` + `CheckBox cbOnlyCurrentTool`。

**DataGridView 列**：
| 列 | 类型 | 说明 |
|----|------|------|
| Visible | DataGridViewCheckBoxColumn | 是否显示 |
| Tool | DataGridViewTextBoxColumn（只读） | 所属工具名 |
| Name | DataGridViewTextBoxColumn（只读） | 显示项名 |
| Type | DataGridViewComboBoxColumn | REGION/LINE |
| Color | DataGridViewComboBoxColumn | red/green/blue/yellow/cyan/magenta... |
| Draw | DataGridViewComboBoxColumn | margin/fill |
| LineWidth | DataGridViewTextBoxColumn | double |

**数据流**：
- `button2_Click`（Run）后：调用 `RefreshOverlayGrid()` 填充 `CollectDisplayItems()` 结果
- `cbOnlyCurrentTool` 勾选时：`RefreshOverlayGrid` 仅列出 `ToolImageOwner[comboBox1.SelectedItem]` 对应工具的项
- `comboBox1_SelectedIndexChanged`：若 `cbOnlyCurrentTool` 勾选，重刷 overlay 列表 + `ApplyOverlays()`
- `overlayGridView.CellEndEdit`：将编辑写回对应 `DisplayItem`（找到 `(Tool, Item)` 引用直接改属性），然后 `ApplyOverlays()`
- 新增 `ApplyOverlays()`：从 `ToolBlock.CollectDisplayItems()` 取（或仅取当前工具）→ 调 `hDisplayControl1.SetOverlays(items)`

**新增按钮**「刷新显示」：手动触发 `ApplyOverlays()`（无需重跑 Run，仅重绘）。

### 6. 持久化 — `ToolBase.cs` SaveToolParam/LoadToolParam

修改 [ToolBase.SaveToolParam()](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs#L458-L491)：在返回字典中追加：
```csharp
{"DisplayItems", new DynamicItem(
    DisplayItems.Values.Select(it => new {
        it.Name, Type = it.Type.ToString(),
        it.Visible, it.Color, it.Draw, it.LineWidth
    }).ToList()  // 不含 Data
)}
```

修改 [ToolBase.LoadToolParam()](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs#L492-L526)：读取 "DisplayItems" 键，反序列化为 `List<JObject>`，逐个重建 `DisplayItem`（仅配置，Data=null）。**保留**已存在的同名项的 Data（避免 Run() 前清空已有运行时数据）——若同名则只更新配置字段。

> **嵌套 ToolBlock**：[ToolBlock.SaveToolParam](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L462-L467) 调 `base.SaveToolParam()`，DisplayItems 自动包含；内部工具的 DisplayItems 在 `BuildSaveData` 第1步 `tool.SaveToolParam()` 时各自序列化。递归天然支持。

## 假设与约定

1. **HALCON 颜色名**：使用 HALCON 标准颜色字符串（"red"/"green"/"blue"/"yellow"/"cyan"/"magenta"/"white"/"black" 等），UI ComboBox 列表硬编码这些。
2. **工具在 Run() 中发布显示项**：示例工具（如阈值/边缘检测）应在 Run() 中调用 `AddDisplayRegion`/`AddDisplayXLD` 或 `UpdateDisplayData`。首次发布用 Add，后续帧用 Update（按 Name）。
3. **HObject 所有权**：`DisplayItem.Data` setter 深拷贝，工具 Run() 中传入的临时 HObject 必须在 finally 中 Dispose（与 PortNode.Value 一致）。
4. **不去除现有 `SetRegion`**：标记 Obsolete 但保留，避免破坏外部调用。
5. **ToolImage key 冲突**：现有 `CollectToolImage` 用 `portNode.Text` 做 key，同名端口会覆盖（既有 bug，不在本任务范围）。`ToolImageOwner` 用相同 key 同步填充，行为一致。
6. **不修改 ProcessPanel**：ProcessPanel.Run 不直接调用显示，显示仅由 ToolBlockControl 触发。如未来需要 ProcessPanel 触发，可复用 `CollectDisplayItems` + HDisplayControl.SetOverlays。

## 实施步骤（建议顺序）

1. **DisplayItem 类 + ToolBase 字段/API**（ToolBase.cs）—— 含 Dispose 追加
2. **HDisplayControl 多叠加层**（HDisplayControl.cs + .Designer.cs）—— 含 SetOverlays、SetView 改造、Dispose
3. **ToolBlock 聚合 + 图像归属映射**（ToolBlock.cs）—— CollectDisplayItems、ToolImageOwner、CollectToolImage 同步、Run 后刷新
4. **持久化**（ToolBase.cs SaveToolParam/LoadToolParam）
5. **ToolBlockControl UI**（ToolBlockControl.cs + .Designer.cs）—— overlayGridView、cbOnlyCurrentTool、ApplyOverlays、RefreshOverlayGrid、CellEndEdit
6. **移除占位代码**：comboBox1_SelectedIndexChanged 中的 `GenRectangle1` demo
7. **示例工具改造**（可选，验证用）：在某个现有工具的 Run() 中调用 `AddDisplayRegion`/`AddDisplayXLD` 验证端到端流程

## 验证步骤

1. **编译**：`Hal.slnx` 0 error
2. **单元验证**：
   - 工具 Run() 后 `DisplayItems` 含预期项，Data 非空且 IsInitialized
   - `CollectDisplayItems()` 返回所有工具的项
   - `ToolImageOwner[key]` 与 `ToolImage[key]` 所属工具一致
3. **显示验证**：
   - ComboBox 选图像后，叠加层正确渲染（region 颜色/draw/线宽、XLD 颜色/线宽）
   - 平移/缩放时叠加层与图像同步重绘，无残留、无错位
   - 勾选/取消 Visible，叠加层立即更新
   - 「仅显示当前工具」勾选后，overlay 列表与渲染均只含当前工具
4. **内存验证**：连续 Run() 10 次，`HSystem.CountObj()` 稳定（无泄漏）；关闭 ToolBlockControl 后叠加层 HObject 已释放
5. **持久化验证**：保存 .vpp → 关闭 → 重开，DisplayItem 配置（颜色/draw/线宽/可见性）恢复；Data 在 Run() 后重新填充
6. **嵌套验证**：嵌套 ToolBlock 的显示项随父级 .vpp 保存/加载，递归正确
