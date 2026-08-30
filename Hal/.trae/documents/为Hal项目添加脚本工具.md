# 为 Hal 项目添加脚本工具（ScriptTool）

## Summary

参照 `新建文件夹 (2)` 中的 `ScriptContral` 代码，为 HToolBase（Hal 项目的工具库）添加一个 **脚本工具 ScriptTool**：用户可在内置代码编辑器中编写 C# 代码，工具运行时动态编译并执行脚本，脚本可访问同 ToolBlock 内的其它工具与端口。

由于 Hal 的架构与原 `WindowsFormsApp5` 不同（Hal 里一切是 `ToolBase`，工具挂在 `ToolBlock.Tools` 下；原项目是扁平的 `ToolModel.processTools` + 一个全局 `ScriptContral` Form 替换默认运行），本次把脚本能力实现为 **一个标准工具** **`ScriptTool : ToolBase`**，而非替换运行循环的全局 Form。这样它能在 ToolBlock 中像其它工具一样被连线、运行、保存。**`ScriptTool有两种形式，1：以常规工具形式添加到ToolBlock.Tools中，支持ToolBlock工具相同的通过连线的方式添加输入和输出，脚本中能动态访问和修改这些值；此类型脚本工具不能访问其他兄弟工具；2：以固定的工具形式添加，现有的ToolBlockControl中以具有“脚本”按钮，此形式下工具不能直接添加输入和输出，但能直接访问ToolBlock工具下的所有共有属性和方法，包括其他兄弟工具及其属性和方法，同时ToolBlock提供两种运行函数，默认使用原有的运行函数，可在此脚本中切换为脚本模式运行，此模式下，不在使用ToolBlock中的运行函数`**

## Current State Analysis

### 参考代码（`新建文件夹 (2)`）

* [ScriptContral.cs](file:///c:/Users/Administrator/Desktop/WindowsFormsApp5/新建文件夹%20\(2\)/ScriptContral.cs) — 一个 `Form`，自带 RichTextBox 代码编辑器，含行号、语法高亮、智能联想（成员/关键字/已创建对象）、外部脚本加载/保存、"执行脚本"按钮。编译用 `ScriptExecutor.Instance`（单例，但该类不在文件夹内，需新建）。

* [ScriptContral.Designer.cs](file:///c:/Users/Administrator/Desktop/WindowsFormsApp5/新建文件夹%20\(2\)/ScriptContral.Designer.cs) — 极简 Designer（UI 实际在 `.cs` 的 `InitEditorWithLineNumbers` 里代码构建）。

* 脚本约定：用户写一个 `public class ToolScript { public ToolScript(ToolModel _tool){...} public string Run(){...} }`，`ScriptExecutor` 编译后用反射实例化（传入 `ToolModel`）并调用 `Run()`，返回字符串。

* 依赖旧项目类型：`ToolModel`、`ProcessTool`、`ImageFileTool`、`BlobTool`、`OneImageTool`、`FindLineTool` — Hal 中均不存在，需映射为 Hal 的等价类型。

### Hal 项目架构（已确认）

* [ToolBase.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs) — 工具基类：`Inputs`/`Outputs`（`Dictionary<string,PortNode>`）、`DisplayItems`、`RootNode`、`ToolName`、`Run()`、`ShowWin()`、`SaveToolParam()`/`LoadToolParam()`、`AddInput/AddOutput`。**无** **`Parent`** **引用**。

* [ToolBlock.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs) — `ToolCollection : Dictionary<string,ToolBase>`（`Add`/`Load`/`Remove`，`Add` 会按类型计数器重命名为 `XxxTool1`）；`ToolBlock.Run()` 遍历 `Tools.Values` 调用 `tool.Run()`；`ShowWin()` 打开 `ToolBlockControl`。

* [ImageSourceTool.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ImageSourceTool.cs) — `internal class ImageSourceTool:ToolBase`（**internal**，外部程序集不可见）。[BlobTool.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/BlobTool.cs) — `public class BlobTool:ToolBase`，`ShowWin()` 打开 `BlobToolForm`。

* [AddToolForm.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Controls/AddToolForm.cs) + [Designer](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Controls/AddToolForm.Designer.cs) — 工具树硬编码在 Designer：`图像源→ImageSourceTool`、`查找→BlobTool`、`通用工具→ToolBlock`。双击节点 → `CreateInstanceByFullName("HToolBase.Tools."+ToolName)` → `ToolBlock.Tools.Add(...)`。

* [ToolTreeviewControl.cs:580](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Controls/ToolTreeviewControl.cs#L580) — 双击"工具"根下节点 → `ToolBlock.Tools[e.Node.Text].ShowWin()`。

* [HToolBase.csproj](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/HToolBase.csproj) — `Library`，`net472`，已引用 `Microsoft.CSharp`、`halcondotnet`、`Newtonsoft.Json`、`System.Windows.Forms`，项目引用 `HAttribute`。

### 关键差异与决策

| 项       | 原参考                      | Hal 方案                                                                                                    |
| ------- | ------------------------ | --------------------------------------------------------------------------------------------------------- |
| 宿主形态    | 全局 Form 替换运行             | `ScriptTool : ToolBase`，作为工具挂在 `ToolBlock.Tools` 中                                                        |
| 脚本上下文   | `ToolModel tool`         | `ToolBlock tool`（父 ToolBlock，通过新增的 `ToolBase.Parent` 取得）                                                  |
| 兄弟工具访问  | `tool.processTools[...]` | `tool.Tools[...]`，端口 `Outputs["..."].Value`                                                               |
| 编译器     | `ScriptExecutor` 单例（缺失）  | 新建 `ScriptExecutor`（**每 ScriptTool 一个实例**，避免多脚本互相覆盖）                                                      |
| 程序集引用   | 硬编码 halcondotnet 绝对路径    | 用 `typeof(HObject).Assembly.Location` 等动态解析                                                               |
| 工具类型可见性 | —                        | `ImageSourceTool` 为 internal，脚本不能强转；**统一用** **`ToolBase`** **+** **`Outputs["..."].Value`** **访问**（不改可见性） |

## Proposed Changes

### 1. 新增 `HToolBase\Tools\ScriptTool.cs`

脚本工具本体，继承 `ToolBase`，命名空间 `HToolBase.Tools`（与 `ImageSourceTool`/`BlobTool`/`ToolBlock` 同级，供 `AddToolForm` 反射创建）。

要点：

* 构造函数：`RootNode.Text="ScriptTool"`、`ToolName="ScriptTool"`、`AddOutput("Result", TypeName.STRING)`、`RootNode.ImageIndex=0`。

* 持有 `ScriptExecutor _executor = new ScriptExecutor();` 与 `string _scriptText`。

* 持有 `bool _isRunning` 重入守卫，防止脚本调用 `tool.Tools["ScriptTool1"].Run()` 引发无限递归。

* `ShowWin()`：`_form?.Dispose(); _form = new ScriptToolForm(this); _form.Show();`（与 `BlobToolForm` 一致用非模态）。

* `Run()`：空脚本→`IsRunSuccess=false; Message="脚本为空";` 返回；`_isRunning` 守卫；编译（`_executor.CompileScript(_scriptText, out var errors)`）失败→写入 Message；成功→`_executor.RunCompiledScript(this.Parent)` 取返回串写入 `Outputs["Result"].Value` 与 `Message`，`IsRunSuccess=true`；catch 异常→`IsRunSuccess=false`。

* `ScriptText` 属性（get/set，set 时清空 `_executor` 缓存以触发重编译）。

* `override SaveToolParam()`：`var d = base.SaveToolParam(); d["Script"] = new DynamicItem(_scriptText); return d;`

* `override LoadToolParam(Dictionary)`：`base.LoadToolParam(dict); if dict.TryGetValue("Script", out var it) _scriptText = Convert.ToString(it.Value) ?? "";`

* `internal ScriptExecutor Executor => _executor;`（供 Form 复用同一编译器实例，保留编译缓存）。

### 2. 新增 `HToolBase\Controls\ScriptExecutor.cs`

动态编译/执行器，命名空间 `HToolBase.Controls`。**非单例**（每 ScriptTool 一个实例）。

API：

* `bool HasCompiledScript { get; }`

* `bool CompileScript(string script, out List<string> errors)` — 用 `CSharpCodeProvider` + `CompilerParameters{GenerateInMemory=true}`；引用程序集动态解析：`System.dll`、`System.Core.dll`、`System.Windows.Forms.dll`、`typeof(HObject).Assembly.Location`（halcondotnet）、`typeof(ToolBase).Assembly.Location`（HToolBase）、`typeof(FieldInfoTagAttribute).Assembly.Location`（HAttribute）、`typeof(Newtonsoft.Json.JsonConvert).Assembly.Location`；编译后缓存 `Assembly` 与 `Type`（查找含 `public string Run()` 的 `ToolScript` 类，或首个含 `Run()` 的公共类）；错误收集到 `errors`。

* `string RunCompiledScript(ToolBlock parent)` — `_compiledType` 为空返回 `"未编译脚本"`；`Activator.CreateInstance(type, parent)` 构造（构造函数签名 `ToolScript(ToolBlock)`）；`GetMethod("Run").Invoke(...)` 返回 string；失败返回异常信息串。

### 3. 新增 `HToolBase\Controls\ScriptToolForm.cs` + `.Designer.cs` + `.resx`

从 [ScriptContral.cs](file:///c:/Users/Administrator/Desktop/WindowsFormsApp5/新建文件夹%20\(2\)/ScriptContral.cs) 移植，类名改 `ScriptToolForm : Form`，命名空间 `HToolBase.Controls`。**保留全部 IDE 能力**（行号、语法高亮、智能联想、外部脚本加载/保存、输出框、状态栏）。

适配改动：

* 字段 `_toolModel`/`_processTools` → `ScriptTool _tool` + `ToolBlock _toolBlock = _tool.Parent;` + `List<ToolBase> _tools = _toolBlock.Tools.Values.ToList();`。

* 构造函数 `ScriptToolForm(ScriptTool tool)`：存 `_tool`；`_executor = _tool.Executor`（复用编译缓存）；初值取 `_tool.ScriptText`（不再用静态 `_lastScriptContent`，每个脚本工具独立）。

* `ExecuteScript_Click` / `RunCurrentScriptExternally_Click` / `RunExternalScript_Click`：调用 `_executor.CompileScript(...)` / `_executor.RunCompiledScript(_toolBlock)`；删除原 `ExecuteScript_Click` 里硬编码 halcondotnet 路径的回退编译块（`ScriptExecutor` 已统一处理）。

* `FormClosing`：`_tool.ScriptText = CodeEditor.Text;`（写回工具，随 .vpp 持久化）。

* 类型缓存 `CacheCommonTypes()`：`ToolModel→ToolBlock`、`ProcessTool→ToolBase`、`ImageFileTool→ImageSourceTool`、删除 `OneImageTool`/`FindLineTool`（不存在）；新增 `HOperatorSet`、`PortNode`、`TypeName`、`HObject`、`HRegion`、`HXLD`。

* 联想词 `_autoCompleteItems` 初值：移除旧工具名，加入 `ToolBlock,ToolBase,ImageSourceTool,BlobTool,Tools,Inputs,Outputs,Value,Run,HOperatorSet,HObject` 等。

* `GetCreatedObjectNames()` → `_toolBlock.Tools.Keys.ToList()`。

* `GetObjectType`：`_toolModel.processTools` → `_toolBlock.Tools`；`objectName=="tool"` 返回 `_toolBlock.GetType()`。

* 示例脚本 `LoadExampleScript()`：改为下面"Hal 适配示例"。

* `.Designer.cs`：照搬原文件结构（仅 `Text="ScriptEditorForm"`、`components`、`Dispose`），UI 由 `InitEditorWithLineNumbers()` 代码构建。`.resx` 为空标准 resx。

#### Hal 适配示例脚本（`LoadExampleScript` 内容）

```csharp
using System;
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
            // 运行指定兄弟工具（避免运行脚本工具自身，否则递归）
            ToolBase img = tool.Tools["ImageSourceTool1"];
            img.Run();

            // 读取端口值
            HObject image = (HObject)img.Outputs["OutputImage"].Value;

            // 写入其它工具输入端口
            ToolBase blob = tool.Tools["BlobTool1"];
            blob.Inputs["InputImage"].Value = image;
            blob.Run();

            return "脚本执行成功";
        }
        catch (Exception ex)
        {
            return "脚本执行失败: " + ex.Message;
        }
    }
}
```

### 4. 修改 `HToolBase\ToolBase.cs` — 增加 Parent 

在 `ToolBase` 类内（`public TreeNode RootNode;` 附近）新增：

```csharp
/// <summary>所属父 ToolBlock（由 ToolCollection.Add/Load 设置）。
/// 脚本工具等需要访问兄弟工具的场景使用；普通工具可不关心。</summary>
public ToolBlock Parent { get; set; }
```

影响面极小：仅新增一个自动属性，现有工具不引用它，行为不变。

### 5. 修改 `HToolBase\Tools\ToolBlock.cs` — 设置 Parent

* `ToolCollection` 类内新增 `internal ToolBlock Owner { get; set; }`。

* `ToolBlock` 构造函数体（`Tools.Clear();` 之后）加 `Tools.Owner = this;`。

* `ToolCollection.Add`（[第55行附近](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L39)）在 `base.Add` 前加 `value.Parent = Owner;`。

* `ToolCollection.Load`（[第58行](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L58)）在 `base.Add` 前加 `value.Parent = Owner;`。

### 6. 修改 `HToolBase\Controls\AddToolForm.Designer.cs` — 注册工具树

在 `InitializeComponent` 的 TreeNode 序列里新增一个"脚本"分类，子节点 `ScriptTool`，并加入 `treeView1.Nodes.AddRange`。由于 `CreateInstanceByFullName("HToolBase.Tools."+ToolName)` 用节点文本作类名，节点 Text 必须为 `ScriptTool`。

### 7. 修改 `HToolBase\HToolBase.csproj` — 登记新文件

`<ItemGroup>` 中新增：

```xml
<Compile Include="Tools\ScriptTool.cs" />
<Compile Include="Controls\ScriptExecutor.cs" />
<Compile Include="Controls\ScriptToolForm.cs"><SubType>Form</SubType></Compile>
<Compile Include="Controls\ScriptToolForm.Designer.cs"><DependentUpon>ScriptToolForm.cs</DependentUpon></Compile>
```

并在 EmbeddedResource 段加 `<EmbeddedResource Include="Controls\ScriptToolForm.resx"><DependentUpon>ScriptToolForm.cs</DependentUpon></EmbeddedResource>`。

## Assumptions & Decisions

1. **形态选择**：实现为 `ScriptTool : ToolBase`（而非替换运行循环的全局 Form）。这是 Hal 工具化架构的唯一合理解读；原参考的 `_isUsingScript` 模式属于旧项目扁平模型，不移植。
2. **兄弟工具访问**：通过新增 `ToolBase.Parent`（`ToolBlock`）实现；`ToolCollection.Add/Load` 负责赋值。最小侵入，仅一个自动属性 + 三处赋值。
3. **类型可见性**：`ImageSourceTool` 为 `internal`，脚本不能强转。**不改可见性**，示例与联想统一用 `ToolBase` + `Outputs["..."].Value` 访问（`ToolBase`/`ToolBlock`/`PortNode`/`TypeName`/`HObject`/`HOperatorSet` 均为 public，足够）。若日后需在脚本里强转具体工具类型，再把该工具类改 `public`（一行）。
4. **编译器实例**：每 `ScriptTool` 一个 `ScriptExecutor`（非单例），支持同一项目多个脚本工具各自独立编译；Form 复用工具的 `_executor` 以保留编译缓存。
5. **重入安全**：`ScriptTool._isRunning` 守卫防止脚本误调自身 `Run()` 导致栈溢出。
6. **持久化**：脚本文本经 `SaveToolParam`/`LoadToolParam` 的 `"Script"` 键随 .vpp 保存（纯字符串，不涉及 HObject 膨胀问题，与现有 HObject 端口跳过逻辑不冲突）。Form 关闭时写回 `_tool.ScriptText`。
7. **运行语义**：`ToolBlock.Run()` 仍会自动调用 `ScriptTool.Run()`；脚本内若再调兄弟工具 `Run()` 可能造成重复执行——属用户脚本职责（与原参考一致），文档以示例注释提醒。
8. **编辑器能力**：完整移植原 IDE（行号/高亮/联想/外部脚本 IO），不做简化，符合"按照新建文件夹2中的代码"。

## Verification Steps

1. 用 `Hal.slnx` 生成，确认 0 error（新增文件 + 4 处改动均能编译）。
2. 运行 Hal → 打开 ToolBlock 编辑器 → 添加工具 → 看到"脚本/ScriptTool"节点 → 双击添加 → ToolTreeviewControl 出现 `ScriptTool1` 节点（图标 ImageIndex=0）。
3. 双击 `ScriptTool1` → 弹出 `ScriptToolForm`，编辑器加载默认示例脚本；行号/高亮/联想（输入 `tool.` 弹出 `Tools` 等成员）正常。
4. 先添加一个 `ImageSourceTool`（`ImageSourceTool1`），再回 ScriptToolForm 点"执行脚本" → 输出框显示"脚本执行成功"，状态栏更新；若脚本有编译错误，输出框列错误行。
5. 关闭 Form → 重开 → 脚本文本保留（来自 `_tool.ScriptText`）。
6. 关闭 ToolBlock 编辑器（触发 `SaveTools`）→ 检查 .vpp JSON 含 `"Script"` 键与脚本文本；重新打开工程 → ScriptTool 脚本恢复。
7. 在 ToolBlock 顶层运行（`ToolModule.Run()` → `toolBlock.Run()`）→ `ScriptTool1.Run()` 被调用 → `Outputs["Result"]` 与 `IsRunSuccess`/`Message` 正确设置。
8. 同一 ToolBlock 放两个 ScriptTool，各自编辑不同脚本并运行 → 互不干扰（验证非单例编译器）。
9. 脚本中尝试 `tool.Tools["ScriptTool1"].Run()` → 被重入守卫拦截，不栈溢出。

