# 修复 .vpp 文件占用空间异常大 — 实施计划

## 摘要

`.vpp` 文件异常膨胀的根因是：**工具输出端口的 HObject 值（IMAGE/REGION/LINE 类型）被序列化进 JSON 文件**。`HObject` 内部封装 HALCON 非托管内存（图像像素/区域/XLD 轮廓数据），Newtonsoft.Json 序列化时将其转换为海量 base64 字符串（截图中 `aFvYF9...` 起始的 ~150KB 数据块即为一个 HImage 的像素序列化结果）。这些数据是 `Run()` 运行时产物，每次执行都会重新生成，完全不需要持久化。修复后 `.vpp` 文件将从数百 KB 缩减到仅数 KB（仅保存端口名称、类型、工具参数等配置数据）。

## 根因分析

### 问题点 1：`ToolBase.SaveToolParam()` (ToolBase.cs:606-624)
```csharp
// 每个端口（包括 IMAGE/REGION/LINE 类型）的 Value 都被直接序列化
Value = port.Value != null ? new DynamicItem(port.Value) : null
```
- `TypeName.IMAGE` 端口的 Value 是 `HObject`（封装 HImage 像素数据）
- `TypeName.REGION` 端口的 Value 是 `HObject`（封装 HRegion 像素数据）
- `TypeName.LINE` 端口的 Value 是 `HObject`（封装 HXLD 轮廓数据）
- Newtonsoft.Json 对 `HObject` 的序列化产生巨大 base64 字符串

### 问题点 2：`ToolBlock.BuildSaveData()` (ToolBlock.cs:557-575)
- 同样的序列化模式，ToolBlock 自身端口的 HObject 值也被写入 .vpp

### 补充：`this.GetType()` 序列化不是根因
`ToolBase.cs:630` 处的 `this.GetType()` 由 Newtonsoft 的 TypeConverter 处理，序列化为小字符串（`"HToolBase.Tools.ImageSourceTool, HToolBase, Version=1.0.0.0..."`），不是主要问题。

### 现有安全性
- `LoadToolParam` (ToolBase.cs:648+) 和 `RestoreFromSaveData` (ToolBlock.cs:600+) 已正确处理 `portData.Value == null` 的情况（有 `if (portData.Value != null)` 守卫），无需修改加载逻辑。

## 用户已确认

通过截图确认了问题现象：`.vpp` 文件中存在 `"data": "aFvYF9..."` 起始的巨大 base64 字符串，定位在 `OutPorts[0].Value` 字段下。

## 提议变更

### 1. ToolBase.SaveToolParam() — 跳过 HObject 端口值

**文件**: [ToolBase.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs#L603-L647)

**修改逻辑**：在保存端口 Value 前检查 `port.PortType`，对 `IMAGE`/`REGION`/`LINE` 类型设 `Value = null`（仅保存端口名与类型配置，不保存运行时数据）。

```csharp
foreach (PortNode port in this.Inputs.Values)
{
    // HObject 类型端口（IMAGE/REGION/LINE）的 Value 是运行时数据，不持久化
    // 由 Run() 重新生成。跳过这些类型以避免 .vpp 文件膨胀
    bool isHObjectPort = port.PortType == TypeName.IMAGE
                      || port.PortType == TypeName.REGION
                      || port.PortType == TypeName.LINE;

    saveData.ToolInputs.Add(new PortSaveData
    {
        Name = port.PortName,
        PortType = port.PortType.ToString(),
        Value = (!isHObjectPort && port.Value != null) ? new DynamicItem(port.Value) : null
    });
}
```

同样的逻辑应用于 Outputs 保存（line 617-624）。

### 2. ToolBlock.BuildSaveData() — 同样的跳过逻辑

**文件**: [ToolBlock.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/Tools/ToolBlock.cs#L556-L576)

对 `ToolInputs` 和 `ToolOutputs` 的保存循环应用完全相同的 `isHObjectPort` 检查。

### 3. 辅助方法：`TypeNameHelper.IsHObjectPort`

**文件**: [ToolBase.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs)

在 `TypeNameHelper` 类中添加便捷判断方法，供两处保存逻辑共用：

```csharp
/// <summary>判断端口类型是否为 HObject 类型（IMAGE/REGION/LINE）。
/// 这些类型的 Value 是运行时数据，不应序列化到 .vpp 文件。</summary>
public static bool IsHObjectPort(this TypeName typeName) =>
    typeName == TypeName.IMAGE || typeName == TypeName.REGION || typeName == TypeName.LINE;
```

### 4. (可选) `this.GetType()` 改 AssemblyQualifiedName

**文件**: [ToolBase.cs](file:///c:/Users/Administrator/source/repos/HVisoion/HToolBase/ToolBase.cs#L630)

将 `new DynamicItem(this.GetType())` 改为 `new DynamicItem(this.GetType().AssemblyQualifiedName)`，使意图更明确（直接存字符串，不依赖 TypeConverter），加载端 `Type.GetType(string)` 兼容。非必须，锦上添花。

## 影响范围

| 变更 | 文件 | 影响 |
|------|------|------|
| 跳过 HObject 端口值保存 | ToolBase.cs × 2处 | 工具自身 SaveToolParam 的 Inputs/OutPorts |
| 跳过 HObject 端口值保存 | ToolBlock.cs × 2处 | ToolBlock.BuildSaveData 的 Inputs/OutPorts |
| 新增 IsHObjectPort 助手 | ToolBase.cs TypeNameHelper | 两处保存逻辑共用 |

### 向后兼容
- **旧 .vpp 文件**：加载时 `portData.Value` 非空但包含 HObject JSON。现有 `LoadToolParam`/`RestoreFromSaveData` 的 `GetRealValue()` 调用会尝试 `Convert.ChangeType`，对 HObject 类型会失败（返回 null 或抛异常被现有 try-catch 捕获）。**HObject 数据实际上从未被正确加载过**——这部分数据本来就是死数据。修改后新旧文件加载行为一致（不恢复 HObject 值，由 Run() 重新生成）。
- **端口配置不受影响**：Name、PortType 字段照常保存/加载。

### 工具参数不受影响
- `SINGAL`（double）、`STRING`、`BOOL`、`R1`/`R2`/`CIRCLE` 等标量端口值**照常保存**——这些是工具参数配置，数据量小，不影响文件大小。

## 验证步骤

1. **编译**：`Hal.slnx` 通过 MSBuild 构建，0 error
2. **保存验证**：创建含 ImageSourceTool（IMAGE 输出端口）的 ToolBlock，保存 .vpp。验证：
   - 文件大小从数百 KB 缩减到数 KB
   - JSON 中 `OutPorts[0].Value` 为 null（或不存在）
   - 端口 Name/PortType 字段仍存在
3. **加载验证**：关闭并重新打开 ToolBlockControl，验证：
   - 端口正确恢复（Name、PortType 正确）
   - 运行 Run() 后 IMAGE 端口值正常填充
   - 工具参数端口（SINGAL/STRING 等）值正确恢复
4. **功能验证**：连线、嵌套 ToolBlock、显示叠加层配置保存/加载行为不变
5. **嵌套验证**：嵌套 ToolBlock 的 .vpp（嵌入父级）同样缩减