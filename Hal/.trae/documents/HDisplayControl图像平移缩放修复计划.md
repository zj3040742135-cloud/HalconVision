# HDisplayControl 图像平移与缩放修复计划

## Summary

上一轮在 `HDisplayControl`（基于基础 `HWindowControl`）加入的手动平移不生效。经核查确认根因有二：
1. **`ShowImage` 全图 fit + 钳制到图像边界**：`SetPart(0,0,h-1,w-1)` 把视口设为整张图，钳制逻辑在「视口==图像」时把任何平移偏移抵消回原位 → 拖动毫无位移。
2. **基础 `HWindowControl` 无内置交互**，手动 `HalconWindow.SetPart` 与控件自身的 `ImagePart` 属性可能不同步，重绘时被 `ImagePart`（设计器初值 640×480）回写覆盖。

用户决定：**保留 `HWindowControl`**（不换 `HSmartWindowControl`），**同时实现平移 + 滚轮缩放**。本计划在 `HWindowControl` 上手写可靠的平移/缩放：统一通过 `SetView()` 同步 `SetPart` 与 `ImagePart` 并重绘；加入以光标为锚点的滚轮缩放，缩放后视口 < 图像，平移即有空间；保留钳制（不出黑边），fit 状态下平移为无操作（符合预期，需先放大）。

## Current State Analysis

- 控件：`HDisplayControl.Designer.cs:37` 声明 `private HalconDotNet.HWindowControl hWindow;`，`ImagePart = new Rectangle(0,0,640,480)`（设计器初值）。
- `ShowImage`（[HDisplayControl.cs:126](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Controls\HDisplayControl.cs#L126)）：`SetPart(0,0,_imgHeight-1,_imgWidth-1)` 全图 fit，未同步 `hWindow.ImagePart`。
- 平移 `HWindow_MouseMove`（[HDisplayControl.cs:54](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Controls\HDisplayControl.cs#L54)）：数学正确，但钳制在 fit 时抵消全部位移；且直接调 `HalconWindow.SetPart`+`DispObj`，未同步 `ImagePart`。
- `ShowImage` 调用链确认：[ToolBlockControl.cs:468](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Controls\ToolBlockControl.cs#L468) `hDisplayControl1.ShowImage(ToolBlock.ToolImage[...])`，故 `image` 字段会被填充，平移守卫可通过。
- 无缩放、无鼠标捕获、无滚轮焦点处理。
- HALCON 引用为 24.11（[HToolBase.csproj:54](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\HToolBase.csproj)），`HWindowControl.ImagePart`/`MouseWheel` 等均可用。

## Proposed Changes

**仅改一个文件**：`HToolBase\Controls\HDisplayControl.cs`。设计器不动（仍用 `HWindowControl`）。

### 改动 1：统一视口更新 `SetView()` + `Redisplay()`
新增私有方法，所有视口变更（ShowImage / 平移 / 缩放）都走它，确保 `SetPart` 与控件 `ImagePart` 同步并立即重绘：

```csharp
/// <summary>设置视口并重绘。同步 HWindowControl.ImagePart，避免控件重绘时回写旧 part。</summary>
private void SetView(double r1, double c1, double r2, double c2)
{
    if (!HalconWindow.IsInitialized() || image == null || !image.IsInitialized()) return;
    int ir1 = (int)r1, ic1 = (int)c1, ir2 = (int)r2, ic2 = (int)c2;
    HalconWindow.SetPart(ir1, ic1, ir2, ic2);
    // ImagePart: X=列, Y=行, Width=列数, Height=行数；与 SetPart 同步，防重绘回写
    hWindow.ImagePart = new Rectangle(ic1, ir1, Math.Max(1, ic2 - ic1), Math.Max(1, ir2 - ir1));
    HalconWindow.ClearWindow();
    HalconWindow.DispObj(image);
}
```

`Redisplay()` 不再单独需要——`SetView` 末尾即重绘。原 `HWindow_MouseMove` 中裸 `ClearWindow`+`DispObj` 改为调 `SetView`。

### 改动 2：`ShowImage` 用 `SetView` fit 并同步 ImagePart
```csharp
HalconWindow.ClearWindow();
image?.Dispose();
HOperatorSet.CopyImage(Image, out image);
HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
_imgWidth = width.I; _imgHeight = height.I;
SetView(0, 0, _imgHeight - 1, _imgWidth - 1);   // 全图 fit，同步 ImagePart
```

### 改动 3：平移改用 `SetView`，钳制逻辑保留
`HWindow_MouseMove` 平移段：数学（`scaleR/scaleC`、抓取式 `newR1 = r1 - deltaY*scaleR`）不变；钳制保留（视口 < 图像时允许在边界内平移，视口 == 图像时无操作）；末尾 `HalconWindow.SetPart+ClearWindow+DispObj` 改为 `SetView(newR1,newC1,newR2,newC2)`。
- **鼠标捕获**：`HWindow_MouseDown` 置 `hWindow.Capture = true`；`HWindow_MouseUp` 置 `hWindow.Capture = false`，保证光标移出控件仍持续平移。

> 说明：fit 状态下整图可见、无平移空间，拖动无位移属预期；滚轮放大后视口 < 图像，平移即在边界内生效。

### 改动 4：滚轮缩放（以光标为锚点）
新增 `HWindow_MouseWheel`，订阅 `hWindow.MouseWheel`：

```csharp
private void HWindow_MouseWheel(object sender, MouseEventArgs e)
{
    if (!HalconWindow.IsInitialized() || image == null || !image.IsInitialized()) return;
    if (hWindow.Width <= 0 || hWindow.Height <= 0) return;

    HalconWindow.GetPart(out int r1, out int c1, out int r2, out int c2);
    double viewH = r2 - r1, viewW = c2 - c1;
    if (viewH <= 0 || viewW <= 0) return;

    // 光标对应的图像坐标（缩放锚点）
    double imgR = r1 + (double)e.Y / hWindow.Height * viewH;
    double imgC = c1 + (double)e.X / hWindow.Width * viewW;

    // 滚轮前滚=放大（视口缩小），后滚=缩小（视口放大）
    double f = e.Delta > 0 ? 0.8 : 1.25;
    double newR1 = imgR - (imgR - r1) * f;
    double newR2 = imgR + (r2 - imgR) * f;
    double newC1 = imgC - (imgC - c1) * f;
    double newC2 = imgC + (c2 - imgC) * f;
    double newViewH = newR2 - newR1, newViewW = newC2 - newC1;

    // 限制最大放大（视口不小于 8 像素，避免退化）
    if (newViewH < 8 || newViewW < 8) return;

    // 限制最大缩小（不超出图像，超出则 fit）
    if (newViewH >= _imgHeight) { newR1 = 0; newR2 = _imgHeight - 1; }
    if (newViewW >= _imgWidth)  { newC1 = 0; newC2 = _imgWidth - 1; }

    // 位置钳制：视口 < 图像时保持在边界内（不出黑边）
    ClampToBounds(ref newR1, ref newR2, _imgHeight - 1);
    ClampToBounds(ref newC1, ref newC2, _imgWidth - 1);

    SetView(newR1, newC1, newR2, newC2);
}

// 把 [a,b] 区间平移到不超出 [0, max]（区间长度不变）
private static void ClampToBounds(ref double a, ref double b, int max)
{
    double len = b - a;
    if (len > max) { a = 0; b = max; return; }   // 区间比范围大：贴满
    if (a < 0) { a = 0; b = len; }
    if (b > max) { b = max; a = max - len; }
}
```

平移段的钳制也改用同一 `ClampToBounds`（替换原内联四段 if），统一逻辑。

### 改动 5：滚轮焦点 + 事件订阅补全
- 新增 `HWindow_MouseEnter`：`hWindow.Focus();`（WinForms 鼠标滚轮只派发给焦点控件，须在进入控件时聚焦）。
- `AddEvent()` 增订 `MouseWheel`、`MouseEnter`；`ClearEvent()` 一一对应取消（原已补 `MouseUp`，再补这两项）：

```csharp
private void AddEvent()
{
    hWindow.MouseDown += HWindow_MouseDown;
    hWindow.MouseMove += HWindow_MouseMove;
    hWindow.MouseUp   += HWindow_MouseUp;
    hWindow.MouseWheel+= HWindow_MouseWheel;
    hWindow.MouseEnter+= HWindow_MouseEnter;
}
private void ClearEvent()
{
    hWindow.MouseDown  -= HWindow_MouseDown;
    hWindow.MouseMove  -= HWindow_MouseMove;
    hWindow.MouseUp    -= HWindow_MouseUp;
    hWindow.MouseWheel -= HWindow_MouseWheel;
    hWindow.MouseEnter -= HWindow_MouseEnter;
}
```

### 改动 6：`UpdateCoordinate` 不变
已基于 `GetPart` 换算，缩放后坐标自动跟随，无需改动。

## Assumptions & Decisions

1. **保留 `HWindowControl`**（用户确认），不换 `HSmartWindowControl`。
2. **平移 + 滚轮缩放**（用户确认）。
3. **fit 状态下平移无位移属预期**：整图可见时无平移空间；滚轮放大后平移生效。若需 fit 状态也能拖动（出黑边），可后续移除 `ClampToBounds`。
4. **`SetView` 同步 `ImagePart`**：根因之一是 `SetPart` 与控件 `ImagePart` 不同步被回写；统一同步以保障重绘可靠。若运行时仍发现重绘不刷新，备选是挂钩 `hWindow.Paint` 调 `DispObj`（本计划先不引入，避免闪烁）。
5. **缩放锚点为光标位置**：放大/缩小时光标下的图像点保持不动，符合主流看图器习惯。
6. **最小视口 8 像素**：防止无限放大退化；最大缩小到 fit（不出黑边）。
7. **鼠标捕获**：`hWindow.Capture` 在 Down/Up 切换，保证拖出控件边界仍平移。
8. **范围外**：控件 resize 时的重绘适配不在本次范围（仅平移+缩放）。

## Verification Steps

1. **编译**：`MSBuild HToolBase.csproj /p:Configuration=Debug /p:Platform=x64`，0 error。
2. **平移**：在 ToolBlockControl 下拉选一张图 → 滚轮前滚放大 2~3 次 → 左键拖动，图像随光标平移，且不出现黑边（钳制生效）。光标移出控件仍能拖动（Capture 生效）。
3. **缩放**：滚轮前滚以光标为中心放大，光标下像素保持不动；后滚缩小，缩到 fit 为止（不再缩小、不出黑边）。
4. **fit 状态平移**：刚加载（fit）时拖动无位移（预期）；放大后拖动有位移。
5. **坐标显示**：底部 `textBox1` 行/列随光标移动与缩放级别正确变化。
6. **重绘同步**：平移/缩放后切换窗口再切回，画面保持（验证 `ImagePart` 同步未被回写覆盖）。
7. **资源**：重复「选图→放大→平移」多次，无异常；`image` 仅在 `ShowImage` 时 Dispose 旧值再 CopyImage，无句柄泄漏。
