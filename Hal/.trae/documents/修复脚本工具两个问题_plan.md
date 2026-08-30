# 修复脚本工具两个问题 — 实现计划

## 一、仓库调研结论

### 问题 1：重新加载脚本后 ToolBlock 多出两个 SINGAL（Single）类型端口，值为"块级脚本执行成功"

**根因已定位**（见 [ToolBase.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs)）：

1. `ToolBase` 四个属性的 setter 统一调用 `SetPortValue(Outputs, propName, value)`：

   * `IsRunSuccess` (bool) → `SetPortValue(Outputs, nameof(IsRunSuccess), value)` Line 364

   * `Message` (string) → `SetPortValue(Outputs, nameof(Message), value)` Line 376

   * `TotalTime` (double) → `SetPortValue(Outputs, nameof(TotalTime), value)` Line 388

   * `Result` (string) → 写错了！`SetPortValue(Outputs, nameof(TotalTime), value)` Line 400

2. `SetPortValue` 辅助方法（Line 644-652）在 `!portDict.ContainsKey(propName)` 时会**自动创建端口**：
   `portDict[propName] = new PortNode()` — 无参构造函数**不设置** **`PortType`**，所以 enum 默认值为 0 = `TypeName.SINGAL`。

3. 属性读写方向也不一致：setter 写入 `Outputs`（Line 364/376/388/400），但 getter 从 `Inputs`（Line 359/372/384/396）读取 → 两者根本不同步，值永远读不到。

4. 块级脚本示例/执行中写 `tool.Message = "块级脚本执行成功"` 、`tool.IsRunSuccess = true` → 立刻在 Outputs 里创建 Message / IsRunSuccess 两个 SINGAL 幽灵端口 → 保存时被序列化 → 下次 `LoadToolParam` / `RestoreFromSaveData` 还原为持久化端口，所以每次运行+保存后就多出两个 Single 端口。

附带 Bug：`Result` setter 写的是 `nameof(TotalTime)`（Line 400），应该是 `nameof(Result)`。

### 问题 2：执行 `ToolBlock.Run()` 时 ToolBlockControl 图像窗口不更新

**根因已定位**（见 [ToolBlockControl.cs button2\_Click](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Controls/ToolBlockControl.cs#L370-L385)）：

* 当前只有 **手动点击 ToolBlockControl 的运行按钮（button2）** 时，才会在 `ToolBlock.Run()` 之后紧接着执行 UI 刷新逻辑：

  1. 清空并重填 `comboBox1.Items`（ToolImage 键，Line 373-380）
  2. `comboBox1.SelectedIndex = 0` 触发 `comboBox1_SelectedIndexChanged` → `hDisplayControl1.ShowImage`
  3. `RefreshOverlayGrid()`（叠加层 DataGridView，Line 383）
  4. `ApplyOverlays()`（调用 `hDisplayControl1.SetOverlays`，Line 384 / 431-435）

* 但 `ToolBlock.Run()` 可以从**外部任意位置直接调用**（例如 `ProcessPanel` 里的运行按钮、父 ToolBlock、单元测试、块级脚本自身想重跑兄弟工具等），此时 ToolBlock **完全没有事件通知订阅者**，导致：

  * comboBox1 仍显示旧键（或为空）

  * hDisplayControl1 不调用 `ShowImage` / `SetOverlays` → 图像窗口死不更新

  * overlayGridView 不刷新

因此需要：`ToolBlock` 在 Run 结束后发送事件，`ToolBlockControl`（以及任何订阅者）在事件里刷新显示。

***

## 二、修改文件与模块

| 文件                                                                             | 修改类型 | 说明                                                                                                                                                                                                                     |
| ------------------------------------------------------------------------------ | ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `HToolBase/ToolBase.cs`                                                        | 修改   | ① IsRunSuccess/Message/TotalTime/Result 改为私有字段（不再走 SetPortValue → 不创建幽灵端口）；② SetPortValue 去掉"自动创建端口"行为（不存在就跳过，不再隐式 new PortNode）；③ 修复 Result setter 写错 TotalTime 的 bug                                                 |
| `HToolBase/Tools/ToolBlock.cs`                                                 | 修改   | ① ToolBlock 增加 `RunCompleted` 事件（`public event EventHandler RunCompleted`）；② Run() 末尾（默认迭代路径+脚本路径 2 处）触发该事件；③ RestoreFromSaveData 中增加兼容：从旧 .vpp 加载的端口列表里过滤掉 IsRunSuccess/Message/TotalTime/Result 这些 SINGAL 幽灵端口，避免被还原 |
| `HToolBase/Controls/ToolBlockControl.cs`                                       | 修改   | ① 构造函数订阅 ToolBlock.RunCompleted；② 把 button2\_Click 中 Run() 之后的 4 步刷新逻辑抽成独立方法 `RefreshDisplayAfterRun()`；③ button2\_Click 与 RunCompleted 处理器都调用它；④ Dispose(bool) 中取消订阅 RunCompleted                                     |
| `HToolBase/Controls/ToolBlockControl.Designer.cs`                              | 不修改  | 已有控件与事件订阅不变                                                                                                                                                                                                            |
| `HToolBase/ToolSaver.cs`                                                       | 不修改  | ToolSaveData 结构不变                                                                                                                                                                                                      |
| `HToolBase/Controls/ScriptExecutor.cs` / `ScriptToolForm.cs` / `ScriptTool.cs` | 不修改  | 它们通过属性赋值触发修复，无需变更                                                                                                                                                                                                      |

***

## 三、详细修改步骤

### Step 1：改造 ToolBase（ToolBase.cs）

1.1 **为四个属性增加 backing fields**（在 IsRunSuccess 属性之前）：

```
private bool _isRunSuccess;
private string _message = string.Empty;
private double _totalTime;
private string _result = string.Empty;
```

1.2 **重写四个属性 getter/setter**：直接读写 backing field，**不再调用 GetPortValue/SetPortValue**。

* IsRunSuccess：get => \_isRunSuccess; set => \_isRunSuccess = value;

* Message：get => \_message ?? string.Empty; set => \_message = value ?? string.Empty;

* TotalTime：get => \_totalTime; set => \_totalTime = value;

* Result：get => \_result ?? string.Empty; set => \_result = value ?? string.Empty; （同时修复写错 TotalTime 的 bug）

1.3 **SetPortValue 去掉"自动创建端口"副作用**：

```
protected void SetPortValue<T>(Dictionary<string, PortNode> portDict, string propName, T value)
{
    if (!portDict.TryGetValue(propName, out var port))
        return;   // 端口不存在→跳过，不隐式创建（避免 SINGAL 幽灵端口）
    port.Value = value;
}
```

保留 `GetPortValue` 现状（不存在返回 default，无副作用）。

1.4 **PortNode 无参构造函数（可选防御）**：考虑到仍有代码可能直接 `new PortNode()`，为避免未来再出现 default=SINGAL 陷阱，在无参构造里显式把 `PortType = TypeName.SINGAL` 去掉（或保持但加注释）。若改动有风险可跳过，主修复已在 Step 1.2-1.3 切断幽灵端口创建路径。

### Step 2：ToolBlock 增加 RunCompleted 事件 + 旧端口兼容（ToolBlock.cs）

2.1 在 ToolBlock 字段区新增：

```
public event EventHandler RunCompleted;
```

2.2 **提取触发点**：在 Run() 方法末尾（两个执行路径尾部）触发事件，确保异常路径不触发。

* 默认迭代路径：Run() Line 173-191 末尾增加 `OnRunCompleted();`

* 脚本模式路径：RunViaBlockScript() Line 194-224 的 `finally` 之后 / `try` 成功分支末端（Message 设置之后、return 之前）或在 finally 外判断成功后触发。**统一做法**：在 Run() 方法末尾（在 if(UseScriptRun) 分支之后的最后一行）直接 `OnRunCompleted()`，因为两条路径走到 Run() 末尾都意味着本次 Run 结束。

2.3 新增 `protected virtual void OnRunCompleted()` 方法：

```
protected virtual void OnRunCompleted()
{
    RunCompleted?.Invoke(this, EventArgs.Empty);
}
```

2.4 **兼容旧 .vpp 幽灵端口**：RestoreFromSaveData() 步骤 3、4（ToolInputs/ToolOutputs 加载）循环中，或 AddInput/AddOutput 中增加过滤：

* 在 `RestoreFromSaveData` 加载步骤 3（foreach ToolInputs）与步骤 4（foreach ToolOutputs）里，对 `portData.Name` 若属于 `{ "IsRunSuccess", "Message", "TotalTime", "Result" }` 且 `portData.PortType == nameof(TypeName.SINGAL)`，则 `continue;` 跳过，不再 AddInput/AddOutput。这样旧的幽灵端口不会再被还原。

* 注意：如果用户**确实**有意创建了同名端口，需要提示——但鉴于这四个名字是 C# 属性名且恰好匹配自动创建的 SINGAL 类型，误报概率极低。更保守可选"仅当 PortType=SINGAL 时才跳过"。

### Step 3：ToolBlockControl 订阅 RunCompleted + 抽刷新方法（ToolBlockControl.cs）

3.1 **抽取刷新方法**：把 button2\_Click 中 `ToolBlock.Run();` **之后**的代码（Line 373-384，共 4 步）抽成独立方法：

```
private void RefreshDisplayAfterRun()
{
    this.comboBox1.Items.Clear();
    foreach (string s in ToolBlock.ToolImage.Keys)
        this.comboBox1.Items.Add(s);
    if (this.comboBox1.Items.Count > 0)
        this.comboBox1.SelectedIndex = 0;  // 触发 SelectedIndexChanged → ShowImage
    RefreshOverlayGrid();
    ApplyOverlays();
}
```

然后 button2\_Click 变为：

```
private void button2_Click(object sender, EventArgs e)
{
    ToolBlock.Run();
    RefreshDisplayAfterRun();
}
```

3.2 **构造函数中订阅 RunCompleted**（Line 38 之后的构造函数内）：

```
this.ToolBlock.RunCompleted += ToolBlock_RunCompleted;
```

3.3 新增事件处理器：

```
private void ToolBlock_RunCompleted(object sender, EventArgs e)
{
    if (this.IsDisposed || !this.IsHandleCreated) return;
    // 如果当前正处于 button2_Click 中（同一线程），避免重复刷新——button2_Click 已手动调用。
    // 此处用 BeginInvoke 封送到 UI 线程（外部调用 ToolBlock.Run 可能来自非 UI 线程）
    if (this.InvokeRequired)
    {
        this.BeginInvoke(new Action(RefreshDisplayAfterRun));
    }
    else
    {
        RefreshDisplayAfterRun();
    }
}
```

*注意：button2\_Click 里 ToolBlock.Run() 是同步调用会立刻触发 RunCompleted → 这里和 button2\_Click 手动 Refresh 会重复执行两次。重复刷新无功能问题（只是性能），但可以在 ToolBlock.Run 内部直接 OnRunCompleted 调用前标记，或在 RefreshDisplayAfterRun 开头判 comboBox1.Items.Count 已匹配 ToolImage.Count 就跳过（可选优化）。主方案先保证正确性，后续再优化。*

3.4 **取消订阅（Dispose）**：在 ToolBlockControl.Dispose(bool disposing) 的 `if (disposing)` 块内，现有事件取消订阅代码之后追加：

```
if (ToolBlock != null)
{
    ToolBlock.RunCompleted -= ToolBlock_RunCompleted;
}
```

位置：ToolBlockControl.Designer.cs Line 14-47 的 `Dispose(bool disposing)` override 中。

### Step 4：构建验证

使用 MSBuild 构建 Hal.slnx，确认 0 errors。

***

## 四、依赖与注意事项

1. **ToolBase 属性语义变更**：IsRunSuccess/Message/TotalTime/Result 四个属性从"经 Inputs/Outputs 端口同步"改为"纯 backing field"。这意味着：

   * 如果有任何连线（Connection）把 IsRunSuccess / Message 等作为端口连接到其他工具 → 此变更后它们不再是自动创建的端口，连线会失败（LookupPort 返回 null）。

   * 但根据项目现状，工具之间的数据端口（Image、Single、String）都是在工具构造函数中通过显式 `AddInput` / `AddOutput` 创建的；运行状态（IsRunSuccess/Message/TotalTime/Result）属于内部状态字段，**历史上没有任何工具会把它们当作连线端口**——从代码搜索来看（IsRunSuccess/Message 仅出现在属性定义、脚本示例、以及 ImageSourceTool 注释的 Line 75 里 `this.Outputs["IsRunSuccess"].Value = ...` 这种测试性注释被注释掉的代码），实际没有使用。语义变更安全。

2. **线程安全**：如果外部从非 UI 线程调用 `ToolBlock.Run()`，RunCompleted 事件会在那个线程触发，所以 ToolBlock\_RunCompleted 必须 `InvokeRequired + BeginInvoke`，否则跨线程访问控件会抛 InvalidOperationException。Step 3.3 已覆盖。

3. **事件重入**：ToolBlock.Run() 内部如果触发脚本 → 脚本内再调用 ToolBlock.Run()，重入守卫（`_isBlockScriptRunning`、`_isRunning`）会拦截第二次 Run，不会导致重入的 RunCompleted。但事件处理器里不要再次调用 ToolBlock.Run()，否则可能死循环。

4. **向后兼容旧 .vpp**：Step 2.4 的过滤器仅丢弃 IsRunSuccess/Message/TotalTime/Result + SINGAL 组合，对用户有意创建的同名且类型正确的端口不会受影响（实际几乎不存在这种命名冲突的情况）。

5. **SetPortValue 行为收缩**：现有显式创建了端口的工具（ImageSourceTool / BlobTool 等）调用 `SetPortValue(Outputs, nameof(OutputImage), value)` 时，端口已经存在（`AddOutput(nameof(OutputImage), TypeName.IMAGE)` 先创建好），所以 Step 1.3 "不存在就跳过"不会影响这些工具。搜索全部 SetPortValue 调用点（Grep 结果）仅有：

   * ImageSourceTool.OutputImage setter（Line 29）

   * ImageSourceTool.Width setter（Line 40）

   * ImageSourceTool.Height setter（Line 51）

   * BlobTool.InputImage setter（Line 23）

   * 以上四个都是工具已在构造中通过 AddOutput 显式创建端口，因此安全。

***

## 五、风险与兜底

| 风险                                     | 影响                                   | 处理                                                                                                       |
| -------------------------------------- | ------------------------------------ | -------------------------------------------------------------------------------------------------------- |
| SetPortValue 行为收缩导致已有调用点不工作            | 工具 Output 端口值不被写入 → 下游工具读到空/旧值       | 在 Step 4 构建通过后，通过运行 ImageSourceTool + BlobTool 的实际 Workflow 验证（UI 上添加并 Run），确认图像/数据正确传递                  |
| IsRunSuccess 等属性不再经端口同步                | 若某历史工具确实通过端口读写 Message 等则失效          | 代码内全局搜索 `Inputs\["Message"\]` / `Outputs\["IsRunSuccess"\]` 等；已确认仅有 ImageSourceTool Line 75 的测试性注释代码，非激活 |
| RunCompleted 重复刷新（button2 手动 + 事件）导致闪烁 | 轻微性能浪费，无功能问题                         | 可加 `int _refreshSuppressed` 或在 ToolBlock.RunCompleted 标记，但属于非关键优化，可后续处理                                  |
| 旧 .vpp 过滤器误删用户有意创建的同名 SINGAL 端口        | 用户数据丢失（极端罕见）                         | 严格按 "Name 属于 {四个属性名} **且** PortType=SINGAL" 过滤，降低误杀率                                                     |
| ToolBlock.Run 完成后外部未订阅者不知道             | ProcessPanel 里显示 ToolBlock 结果可能也需要刷新 | 不在本次用户需求范围内；未来可在 ProcessPanel 同样订阅 RunCompleted                                                          |

