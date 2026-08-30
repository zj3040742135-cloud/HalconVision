# 移除 DisplayItem 持久化 — 实施计划

## 摘要

移除上一任务中为 DisplayItems 添加的 `.vpp` 持久化逻辑。DisplayItems 改为纯运行时数据：工具在 `Run()` 中发布、HDisplayControl 渲染，但不随工程文件保存/加载。每次会话从默认配置开始（由工具 `Run()` 重新发布）。

## 当前状态

上一任务在 [ToolBase.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs) 中添加了 DisplayItems 的保存/加载：

- `SaveToolParam()` (L647-657)：返回字典中追加 `"DisplayItems"` 键，序列化配置（不含 Data）
- `LoadToolParam()` (L695-732)：读取 `"DisplayItems"` 键，重建 `DisplayItem`（仅配置）
- `ToJObjectList()` (L735-761)：仅供 DisplayItems 加载使用的私有助手（反序列化 JArray→List<JObject>）

经 grep 确认 `ToJObjectList` 仅被 DisplayItems 加载块调用（L699），无其它引用。

## 提议变更

**文件**: [ToolBase.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs)

### 1. SaveToolParam — 移除 DisplayItems 键

删除返回字典中的 DisplayItems 条目（L647-657），使 `OutPorts` 后直接结束字典。

### 2. LoadToolParam — 移除 DisplayItems 加载块

删除 L695-732 的 DisplayItems 反序列化块。

### 3. 移除 ToJObjectList 助手

删除 L735-761 的 `private static List<JObject> ToJObjectList(object value)` 方法（成为死代码）。

### 保留不动

- `DisplayItem` 类定义（运行时仍需使用）
- `ToolBase.DisplayItems` 字段（运行时聚合显示项）
- `AddDisplayRegion`/`AddDisplayXLD`/`UpdateDisplayData`/`RemoveDisplayItem`/`ClearDisplayItems` API（工具运行时发布用）
- `IsHObjectPort` 助手（属于 .vpp 膨胀修复，与本任务无关）

## 行为影响

- **保存**：`.vpp` 不再含 DisplayItems 配置（文件略小）
- **加载**：DisplayItems 字典为空，直到工具 `Run()` 重新发布
- **显示叠加层**：加载后首次打开 ToolBlockControl 时叠加层为空；Run 后由工具发布填充，使用代码中的默认配置（颜色/Draw/线宽）
- **向后兼容**：旧 `.vpp` 中残留的 DisplayItems 键被忽略（TryGetValue 找不到对应加载逻辑，自然跳过）

## 验证步骤

1. **编译**：`Hal.slnx` 通过 MSBuild 构建，0 error
2. **保存验证**：保存 .vpp 后用文本编辑器打开，确认无 `"DisplayItems"` 键
3. **加载验证**：重新打开 ToolBlockControl，确认无异常；Run 后叠加层正常显示（由工具 Run() 发布）
