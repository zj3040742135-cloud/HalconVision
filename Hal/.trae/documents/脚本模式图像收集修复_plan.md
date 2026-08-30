# 脚本模式图像收集修复 — 实现计划

## 一、Summary

ToolBlock.Run() 在脚本模式（形态2，`_useScriptRun=true`）下，执行 `RunViaBlockScript()` 后直接调用 `CollectToolImage(ports)`，但 `ports` 列表始终为空 —— 脚本模式路径既没有遍历 Tools 调用 `tool.CollectImage()`，也没有调用 `SetOutportValue(tool)` 传播连线。导致 `ToolImage` 字典为空，ToolBlockControl 图像下拉框无项可选，HDisplayControl 不显示任何图像。

本计划新增一个公共方法 `CollectAllToolsImages(List<PortNode> ports)`，遍历 ToolBlock.Tools 聚合每个工具的 IMAGE 输出端口并传播连线（与默认迭代路径行为一致），并在脚本模式路径中调用它。

## 二、Current State Analysis

### 默认迭代路径（正确）
[ToolBlock.cs L193-L200](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L193-L200)：
```csharp
foreach (ToolBase tool in Tools.Values)
{
    tool.Run();
    ports.AddRange(tool.CollectImage());   // ← 收集 IMAGE 端口
    SetOutportValue(tool);                  // ← 传播连线
}
CollectToolImage(ports);
```

### 脚本模式路径（缺陷）
[ToolBlock.cs L183-L190](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L183-L190)：
```csharp
if (_useScriptRun && !string.IsNullOrWhiteSpace(_blockScriptText))
{
    RunViaBlockScript();
    CollectToolImage(ports);   // ports 为空 → ToolImage 为空
    OnRunCompleted();
    return;
}
```

### 关键事实
- `ToolBase.CollectImage()`（[ToolBase.cs L427-L438](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs#L427-L438)）：返回 `Outputs.Values` 中 `PortType == TypeName.IMAGE` 的 PortNode 列表。
- `ToolBlock.SetOutportValue(tool)`（[ToolBlock.cs L452-L468](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L452-L468)）：根据 connections 把 tool 的输出端口值传播到下游工具的输入或 ToolBlock 的输出端口。
- `ToolBlock.CollectToolImage(ports)`（[ToolBlock.cs L241-L268](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L241-L268)）：把 ports 中的 HObject 深拷贝到 ToolImage 字典（key=工具名.端口名），跳过 Value==null 的端口。
- 已有公共聚合方法先例：`CollectDisplayItems()`（[ToolBlock.cs L271-L282](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L271-L282)）遍历 Tools 聚合 DisplayItem，本计划仿照此命名与模式。

## 三、Proposed Changes

### 修改文件：`c:\Users\Administrator\source\repos\HVisoion\HToolBase\Tools\ToolBlock.cs`

#### Change 1：新增公共方法 `CollectAllToolsImages`

**位置**：紧邻 `CollectToolImage` 方法之后（约 L268 附近），与 `CollectDisplayItems` 同区。

**What/Why/How**：
- **What**：新增公共方法，遍历 `Tools.Values`，对每个工具调用 `tool.CollectImage()` 把 IMAGE 端口追加到传入的 `ports` 列表，并调用 `SetOutportValue(tool)` 传播连线。
- **Why**：脚本模式下脚本负责运行兄弟工具，但 ToolBlock 仍需统一收集图像端口与传播连线，行为与默认迭代路径保持一致；设为 `public` 使脚本在需要时也可显式调用（例如脚本只跑了部分工具后想刷新图像收集）。
- **How**：
```csharp
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
```

#### Change 2：脚本模式路径调用 `CollectAllToolsImages`

**位置**：[ToolBlock.cs L183-L190 Run() 脚本分支](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L183-L190)。

**What/Why/How**：
- **What**：在 `RunViaBlockScript()` 之后、`CollectToolImage(ports)` 之前插入 `CollectAllToolsImages(ports)`。
- **Why**：填补脚本模式路径缺失的图像收集与连线传播，使 ToolImage 字典正确填充，UI 图像下拉框与 HDisplayControl 能显示脚本运行后的输出图像。
- **How**：
```csharp
if (_useScriptRun && !string.IsNullOrWhiteSpace(_blockScriptText))
{
    RunViaBlockScript();
    CollectAllToolsImages(ports);   // ← 新增：收集脚本运行后各工具的输出图像 + 传播连线
    CollectToolImage(ports);
    OnRunCompleted();
    return;
}
```

## 四、Assumptions & Decisions

1. **包含 `SetOutportValue(tool)`**：默认迭代路径对每个工具都调用 `SetOutportValue` 传播连线。为保持两条路径行为一致，新方法也包含此调用。
   - 影响评估：脚本模式下，若脚本已显式设置了 ToolBlock 输出端口值，`SetOutportValue` 会按 connections 覆盖下游输入端口——这与默认路径行为相同，不引入新副作用。
   - 若未来需要"纯图像收集、不传播连线"的场景，可再拆分方法；当前不预先抽象。

2. **方法可见性为 `public`**：与 `CollectDisplayItems()` 一致，便于脚本在需要时显式调用（例如脚本只运行了部分工具，想立即刷新图像收集而不等 Run 结束）。

3. **不修改默认迭代路径**：默认路径已正确，不抽取重构，避免引入回归风险。新方法仅服务于脚本模式路径。若未来要统一两条路径，可再重构。

4. **空值与重入安全**：`CollectAllToolsImages` 内部对 `Tools`/`ports`/`tool` 做 null 检查；`CollectImage` 与 `SetOutportValue` 本身对空 Outputs / 无 connections 的工具都是安全的（无副作用）。

5. **关于"脚本未运行的工具的旧图像"**：若脚本只运行了 Tools 的子集，`CollectAllToolsImages` 会遍历全部 Tools，未运行工具的 IMAGE 端口 Value 若为上一次 Run 的残留 HObject，仍会被收集。`CollectToolImage` 内部深拷贝时不会跳过这些（仅跳过 `Value==null`）。此行为与默认迭代路径一致（默认路径也是全量遍历），且脚本运行前用户通常先清空或首次运行无残留，不在本次修复范围扩展。

## 五、Verification

1. **构建验证**：MSBuild 构建 `Hal.slnx`，确认 0 errors。
2. **功能验证（手动）**：
   - 在 ToolBlockControl 中添加一个 ImageSourceTool 与一个 BlobTool，建立连线。
   - 打开"脚本"按钮，编写块级脚本调用 `tool.Tools["ImageSourceTool1"].Run();` 并切换"脚本模式：开"。
   - 点击"运行"按钮：预期 comboBox1 下拉出现 `ImageSourceTool1.OutputImage` 等图像项，HDisplayControl 显示对应图像。
   - 切回"脚本模式：关"，确认默认迭代路径仍正常显示图像（无回归）。
3. **回归验证**：确认 ToolBlock.RunCompleted 事件仍正常触发（脚本路径末尾的 `OnRunCompleted()` 不变），ToolBlockControl UI 刷新无异常。
