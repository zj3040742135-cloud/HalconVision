# HObject 内存泄漏检查与修复计划

## Summary

HToolBase（当前主代码）大量使用 `HalconDotNet.HObject` 承载图像/区域/XLD。HObject 是非托管资源的托管包装，必须显式 `Dispose`，否则会泄漏直到不确定的终结器回收。本次审计发现 8 处确定的泄漏/释放缺陷，集中在 `PortNode.Value` 赋值语义、`ToolBlock.Dispose` 不完整、端口删除与显示控件错误释放、以及叶节点工具未实现释放模式。

经用户确认：**范围仅限 HToolBase 目录**；**端口间共享 HObject 的修复策略为「setter 深拷贝 + 释放旧值」**——每个端口持有独立副本，彻底断开引用共享，从根上消除 use-after-free。

## Current State Analysis（审计结果）

### 范围说明
- **HToolBase/**：当前主代码，本次修复目标。
- **repos/**：遗留代码（`ProcessTool.cs` 的 PortNode.Value 赋值时 `CopyImage` 但**不释放旧值**，每次重赋值都泄漏）。本次不修改，仅在附录列出不作为修复项。

### 根因分析：端口间 HObject 引用共享

[ToolBase.cs:309-337](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Tools\ToolBlock.cs#L309-L337) 中 `SetOutportValue` / `GetInputValue` 直接把源端口 `Outputs[fromPort].Value` 引用赋给目标端口 `Inputs[toPort].Value`：

```csharp
port.ToTool.Inputs[port.ToPort.Text].Value = port.FromTool.Outputs[port.FromPort.Text].Value;
```

而 [ToolBase.cs:224-240](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\ToolBase.cs#L224-L240) 的 `PortNode.Value` setter **不深拷贝**，仅存引用，且**释放旧值**：

```csharp
set {
    if (!Equals(_value, value)) {
        if (_value is HObject oldHObj) oldHObj.Dispose();  // 释放旧值
        _value = value;                                    // 仅存引用，未拷贝
    }
}
```

后果：源端口与目标端口指向**同一个 HObject**。下次 Run 源端口被赋新值时，setter Dispose 了这个 HObject，而目标端口仍持有该引用 → **use-after-free**；目标端口随后被赋值时又 Dispose 一次 → **double-dispose**。这是最严重的根因，牵动多条路径。

### 确认的缺陷清单（HToolBase）

| # | 级别 | 位置 | 缺陷 |
|---|------|------|------|
| 1 | HIGH | [ToolBase.cs:224-240](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\ToolBase.cs#L224-L240) | setter 存引用而非拷贝，且释放旧值 → 端口间共享导致 use-after-free/double-dispose |
| 2 | HIGH | [ToolBlock.cs:200-235](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Tools\ToolBlock.cs#L200-L235) | `Dispose()` 未释放 `ToolImage.Values` 的 HObject；`Inputs.Clear()/Outputs.Clear()` 未释放端口 `Value` |
| 3 | HIGH | [ToolBlockControl.cs:401-439](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Controls\ToolBlockControl.cs#L401-L439) | 删除端口(button6/button7)未 Dispose `port.Value` 的 HObject |
| 4 | HIGH | [HDisplayControl.cs:35](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Controls\HDisplayControl.cs#L35) | `ShowImage` 末尾 `Image.Dispose()` 释放了调用方的 HObject（`ToolImage[key]`），再次选中即 ObjectDisposedException |
| 5 | HIGH | [ToolBase.cs:255-529](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\ToolBase.cs#L255) / [ImageSourceTool.cs](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Tools\ImageSourceTool.cs) | `ToolBase` 未实现 `IDisposable`；叶节点工具(如 ImageSourceTool)未 override `OnDeleted`，删除工具时端口 HObject 泄漏 |
| 6 | MED  | [ToolBlock.cs:172](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Tools\ToolBlock.cs#L172) | `CollectToolImage` 中 `if ((HObject)portNode.Value == null) return;` 应为 `continue`，否则任一端口为空就丢弃后续所有图像 |
| 7 | MED  | [ImageSourceTool.cs:96-114](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Tools\ImageSourceTool.cs#L96-L114) | `Run()` 的 `catch{}` 吞异常，`ReadImage` 成功后若 `CopyImage`/`Clone` 抛异常则 `image`/`outimage` 泄漏；且 `.Clone()` 与新 setter 叠加会导致临时 clone 泄漏 |
| 8 | MED  | [ImageSourceTool.cs:21-30](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Tools\ImageSourceTool.cs#L21-L30) | `OutputImage` getter 读 `Inputs`、setter 写 `Outputs`，不对称，getter 恒返回 null（非泄漏，但影响数据流，顺带修） |

## Proposed Changes

### Fix 1：PortNode.Value setter 深拷贝 + 释放旧值（核心修复）
**文件**：`HToolBase\ToolBase.cs`（PortNode.Value setter，约 224-240 行）

**改动**：对 `HObject` 类型新值用 `HOperatorSet.CopyObj` 深拷贝（覆盖 IMAGE/REGION/LINE 全部 iconic 类型），每个端口持有独立副本；保留对旧 HObject 的 Dispose。保持 `!Equals(_value, value)` 守卫避免同引用重复拷贝。

```csharp
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
        if (value is HObject newHObj && newHObj.IsInitialized())
        {
            try { HOperatorSet.CopyObj(newHObj, out HObject copy, 1, -1); _value = copy; }
            catch { _value = value; }            // 拷贝失败回退为引用（保降级可用）
        }
        else
        {
            _value = value;                      // 非 HObject / null / 未初始化：直接存
        }
    }
}
```

**为何用 CopyObj 而非 CopyImage**：`CopyObj` 适用于所有 iconic 对象（HImage/HRegion/HXLD），对应 TypeName.IMAGE/REGION/LINE 全覆盖；CopyImage 仅适用图像。

**连带影响**：调用方赋值后仍持有原 HObject 引用，需自行释放临时变量（见 Fix 7）。`SetOutportValue`/`GetInputValue` 无需改动——setter 已自动深拷贝，源/目标端口各持独立副本。

### Fix 2：ToolBlock.Dispose() 补全 ToolImage 与端口 Value 释放
**文件**：`HToolBase\Tools\ToolBlock.cs`（Dispose 方法，约 200-235 行）

**改动**：在 Dispose 中先释放 `ToolImage.Values` 的 HObject；端口 Value 的释放由 Fix 5 的 `base.Dispose()` 统一处理。

```csharp
public override void Dispose()   // 改为 override（Fix 5 让 ToolBase 实现 IDisposable）
{
    // 1. 销毁窗体、树视图（原逻辑保留）
    ToolBlockControl?.Dispose();
    ToolBlockControl = null;
    toolTreeview?.Dispose();
    toolTreeview = null;

    // 2. 释放 ToolBlock 自身显示缓存 ToolImage 的 HObject（新增）
    if (ToolImage != null)
    {
        foreach (var ho in ToolImage.Values)
        {
            try { if (ho is HObject h && h.IsInitialized()) h.Dispose(); } catch { }
        }
        ToolImage.Clear();
    }

    // 3. 递归释放内部工具（原逻辑保留，tool.Dispose() 现在会释放其端口 Value）
    if (Tools != null)
    {
        foreach (var tool in Tools.Values)
        {
            tool?.OnDeleted();
            tool?.Dispose();   // ToolBase 现已实现 IDisposable（Fix 5）
        }
        Tools.Clear();
    }

    // 4. 释放本 ToolBlock 自身端口的 Value + 清空集合（交给 base.Dispose）
    connections?.Clear();
    base.Dispose();            // 释放 Inputs/Outputs 的 HObject Value 并清空（Fix 5）

    // 5. 其余原逻辑
    RootNode = null;
    AddInputEvent = null;
    AddOutputEvent = null;
}
```

### Fix 3：ToolBlockControl 删除端口时释放 port.Value
**文件**：`HToolBase\Controls\ToolBlockControl.cs`（button6_Click 约 401-422、button7_Click 约 424-439）

**改动**：在 `Inputs.Remove(name)` / `Outputs.Remove(name)` 之前，释放被删端口的 HObject Value。

```csharp
// button6_Click（输入端口删除）
if (this.ToolBlock.Inputs.TryGetValue(name, out var port))
{
    this.ToolBlock.DisconnectPortByPort(port);
    // 新增：释放该端口持有的 HObject（删除即不再使用）
    if (port.Value is HObject hObj)
    {
        try { if (hObj.IsInitialized()) hObj.Dispose(); } catch { }
    }
    port.Parent?.Nodes.Remove(port);
}
this.ToolBlock.Inputs.Remove(name);
```

对 `button7_Click`（输出端口删除）做同样处理。注意：Fix 1 之后端口 Value 是独立副本，释放安全，不影响其他端口。

### Fix 4：HDisplayControl.ShowImage 不释放调用方的 HObject
**文件**：`HToolBase\Controls\HDisplayControl.cs`（ShowImage，约 23-37 行）

**改动**：删除末尾 `Image.Dispose();`。显示已基于 `imgCopyObj` 副本工作并已释放副本，调用方（`ToolBlockControl.comboBox1_SelectedIndexChanged` 传入 `ToolImage[key]`）仍持有原图，不应被显示方释放。

```csharp
public void ShowImage(HObject Image)
{
    if (Image != null && HalconWindow.IsInitialized())
    {
        HalconWindow.ClearWindow();
        HObject imgCopyObj;
        HOperatorSet.CopyImage(Image, out imgCopyObj);
        HOperatorSet.GetImageSize(imgCopyObj, out HTuple width, out HTuple height);
        HalconWindow.SetPart(0, 0, (int)height - 1, (int)width - 1);
        HalconWindow.DispObj(imgCopyObj);
        imgCopyObj.Dispose();
        // 不再 Dispose(Image)：调用方持有，显示方不夺所有权
    }
}
```

### Fix 5：ToolBase 实现 IDisposable，统一释放端口 Value
**文件**：`HToolBase\ToolBase.cs`

**改动**：`ToolBase` 实现 `IDisposable`，提供 `public virtual void Dispose()` 释放所有 `Inputs`/`Outputs` 端口的 HObject Value 并清空字典。`ToolBlock.Dispose` 改为 `override` 并调用 `base.Dispose()`（见 Fix 2）。叶节点工具（ImageSourceTool 等）无需额外代码即自动释放端口 Value；如有其他独占资源可 override。

```csharp
public class ToolBase : IDisposable
{
    // ... 原有成员不变 ...

    public virtual void Dispose()
    {
        DisposePortValues(Inputs);
        DisposePortValues(Outputs);
        Inputs?.Clear();
        Outputs?.Clear();
        Parameters?.Clear();
        RootNode = null;
    }

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
}
```

`ToolBlock` 类声明改为 `public class ToolBlock : ToolBase`（IDisposable 已由基类实现），`Dispose` 改 `public override void Dispose()`（见 Fix 2）。`ProcessPanel.cs:199` 的 `toolModule.Dispose()` → `ToolModule.Dispose()` → `toolBlock.Dispose()` 链路不变，递归释放全部子工具端口 Value。

### Fix 6：CollectToolImage 的 return 改为 continue
**文件**：`HToolBase\Tools\ToolBlock.cs`（CollectToolImage，约 163-178 行）

```csharp
foreach (PortNode portNode in ports)
{
    if (portNode.Value == null) continue;          // 原: return; → 改 continue
    HObject TempImage;
    HOperatorSet.CopyImage((HObject)portNode.Value, out TempImage);
    ToolImage.Add(portNode.Text, (HObject)TempImage);
}
```

### Fix 7：ImageSourceTool.Run 用 try/finally 保释放，简化冗余拷贝
**文件**：`HToolBase\Tools\ImageSourceTool.cs`（Run，约 96-114 行）

**改动**：Fix 1 的 setter 已深拷贝，去掉冗余 `CopyImage`+`Clone`，直接 `OutputImage = image`（setter 拷贝）；用 try/finally 保证 `image` 在所有路径下释放。

```csharp
public override void Run()
{
    HObject image = null;
    try
    {
        HOperatorSet.ReadImage(out image, "C:\\Users\\Administrator\\Desktop\\pic\\图片丢失\\1\\A面大图\\EA0920-14684976-20260228153544-A面-OK-原图.jpg");
        OutputImage = image;   // setter 深拷贝，端口独占副本；image 由 finally 释放
    }
    catch { /* 保留原吞异常行为，避免阻塞流程 */ }
    finally
    {
        image?.Dispose();
    }
}
```

### Fix 8：ImageSourceTool.OutputImage getter 改读 Outputs
**文件**：`HToolBase\Tools\ImageSourceTool.cs`（约 21-30 行）

```csharp
public HObject OutputImage
{
    get { return GetPortValue<HObject>(Outputs, nameof(OutputImage)); }  // 原 Inputs → Outputs
    set { SetPortValue(Outputs, nameof(OutputImage), value); }
}
```

## Assumptions & Decisions

1. **范围**：仅 HToolBase 目录；repos/ 遗留代码不修改。
2. **setter 策略**：深拷贝 + 释放旧值（用户确认）。代价是每次端口传值多一次 `CopyObj`；对图像流量大的场景可后续优化为「仅在跨工具边界拷贝」，但当前优先正确性。
3. **CopyObj 覆盖性**：`HOperatorSet.CopyObj(obj, out copy, 1, -1)` 适用 HImage/HRegion/HXLD 全部 iconic 对象，覆盖 TypeName.IMAGE/REGION/LINE。
4. **降级**：setter 深拷贝失败时回退为存引用（catch），保证功能可用性优先于严格隔离；正常路径不会进入回退。
5. **ToolBase.Dispose 模式**：采用简单 `virtual Dispose()` 而非完整 `Dispose(bool)/GC.SuppressFinalize` 模式——HToolBase 当前无原生资源句柄，且 ToolBlock 已有非标准 Dispose，保持最小改动即可统一释放端口 Value。
6. **不修复项（附录）**：
   - repos/ `ProcessTool.cs` PortNode.Value setter 不释放旧值 → 每次重赋值泄漏（遗留代码，本次不动）。
   - repos/ `ProcessAttributes.DoConditionMode` finalImage 在 saveThread 与外层 finally 双释放竞态。
   - HToolBase `SaveToolParam` 对 HObject 端口值用 `DynamicItem` 直接存引用，JSON 序列化 HObject 会失败/异常——属序列化缺陷，非内存泄漏，不在本次范围。
   - 多处 `GC.Collect()` 滥用（repos/）属代码异味，不靠它修泄漏。

## Verification Steps

1. **编译**：全部改动后 `dotnet build`（或 VS 生成）0 error 0 warning（新增代码无未使用变量）。
2. **HALCON 对象计数诊断**（临时）：在 `ToolBlock.Run` 入口/出口各加一行 `Console.WriteLine($"[ObjCount] {HSystem.CountObj()}");`。连续运行同一 ToolBlock N≥20 次，`CountObj` 应趋于稳定（不随运行次数单调增长）。验证后移除诊断行。
3. **use-after-free 回归**：构造「ImageSourceTool → 某处理工具」连线，连续 Run 多次，确认目标工具输入端口不再因源端口重赋值而持有已释放 HObject（无 ObjectDisposedException，结果图像正确）。
4. **删除模块**：在 ProcessPanel 删除一个 ToolModule，再次运行流程，确认无异常；`HSystem.CountObj()` 较删除前不残留该模块的图像对象。
5. **删除端口**：在 ToolBlockControl 用 button6/button7 删除持有图像的输入/输出端口，确认 `CountObj` 下降、无异常。
6. **显示控件**：在 ToolBlockControl 下拉框反复切换 `ToolImage` 选项，确认无 ObjectDisposedException（验证 Fix 4）。
7. **CollectToolImage**：构造一个含空图像端口 + 一个有效图像端口的 ToolBlock，Run 后确认有效图像仍出现在下拉框（验证 Fix 6 的 return→continue）。
