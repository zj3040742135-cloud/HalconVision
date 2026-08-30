# 取消平移边界限制 + 修复平移中图像放大问题 计划

## Summary

用户反馈两点：
1. **平移的钳制边界要取消**（允许平移出图像边界，即使产生黑边，fit 状态下也能拖动）。
2. **平移过程中图像会被“放大”**（明显的视觉 Bug，越拖越大）。

已通过静态代码分析定位到第 2 条根因：**视口尺寸在每次 `GetPart`→`(int)`截断→`SetView`→`SetPart` 循环中不断丢失 1~2 像素**。HALCON 约定 `SetPart(r1,c1,r2,c2)` 中 r2/c2 为**包含边界**（像素总数 = r2-r1+1）；但代码里 `ImagePart.Width = c2-c1`（`Rectangle` 语义为**不含左边界**，像素总数 = Width），且每次 `double`→`int` 截断又向下取整 1。经过多次 MouseMove 后视口尺寸逐次偏小，导致**图像看似不断放大**（同样的窗口像素映射到更少的图像像素）。缩放也受同一 Bug 影响（但每次 WheelDelta=120 时一次缩 20%，1px 丢失不显眼）。

按两项要求给出修复：统一**以 `ImagePart` Rectangle 为视口状态真源**（不依赖 `GetPart` 截断返回值），维护 `double` 精度的 `_viewR1/_viewC1/_viewR2/_viewC2` 字段，平移直接加偏移不改尺寸 → **彻底消除放大 Bug**；平移取消 `ClampToBounds`；缩放仍保留最小/最大限制。

## Current State Analysis

### 平移放大 Bug 根因

当前 `HWindow_MouseMove`（[HDisplayControl.cs:72](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Controls\HDisplayControl.cs#L72)）关键路径：

```csharp
HalconWindow.GetPart(out int r1, out int c1, out int r2, out int c2);   // 截断 int
...
double newR1 = r1 - deltaY * scaleR;
double newR2 = r2 - deltaY * scaleR;   // 长度看似等于 r2-r1...
SetView(newR1, newC1, newR2, newC2);   // SetView 再次 (int) 截断
```

`SetView`（[HDisplayControl.cs:149](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Controls\HDisplayControl.cs#L149)）：
```csharp
int ir1 = (int)r1, ... ir2 = (int)r2, ir2 = (int)r2;   // ← (int)截断：1024.9→1024
HalconWindow.SetPart(ir1, ic1, ir2, ic2);
hWindow.ImagePart = new Rectangle(ic1, ir1, Math.Max(1, ic2 - ic1), Math.Max(1, ir2 - ir1));
//         ^ Rectangle.Width = ic2 - ic1，而 SetPart(c2 含边界) 实际像素 = ic2-ic1+1 → 少 1 像素
```

每次 MouseMove 两次截断 + Rectangle Width-1 语义差：典型 1024×768 窗口连续 ~20 次 MouseMove 视口缩掉 1~2%，肉眼明显感到“放大”。

### 边界钳制（需取消）

`HWindow_MouseMove:89-90` 调 `ClampToBounds`；`HWindow_MouseWheel:136-137` 也调。用户要求取消**平移**的钳制；**缩放**仍需保留：最小视口 8px 防退化、最大缩放到 fit（再缩小会把 1 张图只占窗口一小块无意义）。

## Proposed Changes

**仅改 [HDisplayControl.cs](file:///c:\Users\Administrator\source\repos\HVisoion\HToolBase\Controls\HDisplayControl.cs)**。

### 改动 1：新增 `double` 精度视口字段（状态真源）

新增字段：
```csharp
// 视口状态真源：double 精度，避免 int 截断累积误差。长度 == 图像像素数，
// 不随 SetPart/GetPart 的 int 语义变化。ShowImage/平移/缩放都写这四个值。
double _viewR1 = 0, _viewC1 = 0, _viewR2 = 0, _viewC2 = 0;
```

### 改动 2：`SetView` 以 `_view*` 为状态源，正确同步 HALCON 语义

修复 SetPart(含边界) ↔ ImagePart(不含边界) 的差 1：
```csharp
private void SetView()   // 直接读 _viewR1/_viewC1/_viewR2/_viewC2
{
    if (!HalconWindow.IsInitialized() || image == null || !image.IsInitialized()) return;
    // SetPart: r2/c2 含边界，故末尾像素 = floor 到最接近的整数边界
    int ir1 = (int)Math.Floor(_viewR1);
    int ic1 = (int)Math.Floor(_viewC1);
    int ir2 = (int)Math.Floor(_viewR2);
    int ic2 = (int)Math.Floor(_viewC2);
    HalconWindow.SetPart(ir1, ic1, ir2, ic2);
    // ImagePart: Rectangle.Width 为像素数(不含边界)，= 视口跨度
    int w = (int)Math.Max(1, Math.Round(_viewC2 - _viewC1));
    int h = (int)Math.Max(1, Math.Round(_viewR2 - _viewR1));
    hWindow.ImagePart = new Rectangle(ic1, ir1, w, h);
    HalconWindow.ClearWindow();
    HalconWindow.DispObj(image);
}
```

说明：旧签名 `SetView(r1,c1,r2,c2)` 改为先赋值 `_view*` 再 `SetView()`，或改 `private void SetView(double r1,double c1,double r2,double c2){ _viewR1=r1;...; SetView(); }`。两种等价，代码采用后者易搜索：

```csharp
private void SetView(double r1, double c1, double r2, double c2)
{
    _viewR1 = r1; _viewC1 = c1; _viewR2 = r2; _viewC2 = c2;
    SetView();
}
private void SetView()
{ /* 如上 */ }
```

### 改动 3：`HWindow_MouseMove` 直接偏移视口四角（不改尺寸） → 消除放大

```csharp
private void HWindow_MouseMove(object sender, MouseEventArgs e)
{
    UpdateCoordinate(e.Location);
    if (!_isPanning) return;
    if (!HalconWindow.IsInitialized() || image == null || !image.IsInitialized()) return;

    int deltaX = e.X - _lastMousePosition.X;
    int deltaY = e.Y - _lastMousePosition.Y;
    if (deltaX == 0 && deltaY == 0) return;

    int viewW = hWindow.Width, viewH = hWindow.Height;
    if (viewW <= 0 || viewH <= 0) return;

    // 用 _view* 计算 scale（double → 无截断损失），平移只改偏移、不改长度
    double scaleR = (_viewR2 - _viewR1) / viewH;
    double scaleC = (_viewC2 - _viewC1) / viewW;
    double dR = deltaY * scaleR;
    double dC = deltaX * scaleC;

    // ✅ 取消边界限制，允许平移出图像（黑边无所谓）
    _viewR1 -= dR; _viewR2 -= dR;
    _viewC1 -= dC; _viewC2 -= dC;

    SetView();                 // 同步 SetPart + ImagePart + 重绘
    _lastMousePosition = e.Location;
}
```

### 改动 4：`HWindow_MouseDown` 先同步一次 `_view*`（防滚轮后第一次拖动跳变）

滚轮改视口走自己的 Math（已改读 _view* 见改动 5），不会不一致；但历史上若外部直接改 SetPart，首次 `MouseDown` 可先同步一次（可选防御）：
```csharp
private void HWindow_MouseDown(object sender, MouseEventArgs e)
{
    if (e.Button == MouseButtons.Left && image != null && image.IsInitialized())
    {
        // 防御性同步：若 _view 未初始化(设计器默认)，从 GetPart 读取初值并正确回算长度
        if (_viewR2 - _viewR1 <= 0 || _viewC2 - _viewC1 <= 0)
        {
            HalconWindow.GetPart(out int r1, out int c1, out int r2, out int c2);
            // SetPart 含边界 → 像素数 = r2-r1+1，_view 以长度语义记录
            _viewR1 = r1; _viewC1 = c1; _viewR2 = r2 + 1; _viewC2 = c2 + 1;
        }
        _isPanning = true;
        _lastMousePosition = e.Location;
        Cursor = Cursors.Hand;
        hWindow.Capture = true;
    }
}
```

> 说明：以上 `+1` 是 HALCON SetPart(含边界) → Rectangle 跨度的修正。也可在 ShowImage 里以 `_viewR2 = _imgHeight; _viewC2 = _imgWidth;` 直接赋正确长度，不需要等 MouseDown。

### 改动 5：`HWindow_MouseWheel` 改读 `_view*`，保留缩放限制，取消平移钳制即可

```csharp
private void HWindow_MouseWheel(object sender, MouseEventArgs e)
{
    if (!HalconWindow.IsInitialized() || image == null || !image.IsInitialized()) return;
    if (hWindow.Width <= 0 || hWindow.Height <= 0 || _imgWidth <= 0 || _imgHeight <= 0) return;

    double viewH = _viewR2 - _viewR1, viewW = _viewC2 - _viewC1;
    if (viewH <= 0 || viewW <= 0) return;

    // 光标图像坐标（_view* 语义一致，无 int 差）
    double imgR = _viewR1 + (double)e.Y / hWindow.Height * viewH;
    double imgC = _viewC1 + (double)e.X / hWindow.Width  * viewW;

    double f = e.Delta > 0 ? 0.8 : 1.25;
    double newR1 = imgR - (imgR - _viewR1) * f;
    double newR2 = imgR + (_viewR2 - imgR) * f;
    double newC1 = imgC - (imgC - _viewC1) * f;
    double newC2 = imgC + (_viewC2 - imgC) * f;
    double newViewH = newR2 - newR1, newViewW = newC2 - newC1;

    // 保留：最小放大 8px；最大缩放到 fit（再小无意义，仍可平移查看图像全貌 + 黑边）
    if (newViewH < 8 || newViewW < 8) return;
    if (newViewH >= _imgHeight) { newR1 = 0; newR2 = _imgHeight; }
    if (newViewW >= _imgWidth)  { newC1 = 0; newC2 = _imgWidth;  }

    // 注意：不再对 *位置* ClampToBounds（平移已无边界）；缩放到 fit 位置自然在 0..size
    SetView(newR1, newC1, newR2, newC2);
}
```

### 改动 6：`ShowImage` 用 `_viewR2 = _imgHeight; _viewC2 = _imgWidth;`（Rectangle 不含边界）

```csharp
_imgWidth = width.I;
_imgHeight = height.I;
_viewR1 = 0; _viewC1 = 0; _viewR2 = _imgHeight; _viewC2 = _imgWidth;
SetView();
```

### 改动 7：`UpdateCoordinate` 改读 `_view*`（无需 `GetPart` 截断）

```csharp
double imgR = _viewR1 + (double)controlPt.Y / hWindow.Height * (_viewR2 - _viewR1);
double imgC = _viewC1 + (double)controlPt.X / hWindow.Width  * (_viewC2 - _viewC1);
textBox1.Text = $"行：{imgR:F1}  列：{imgC:F1}";
```

### 改动 8：`ClampToBounds` 暂时保留但不再调用

平移取消后 `ClampToBounds` 无调用点，**暂时不删**（以后可能加「回图像中心」按钮需要）。如需清理可后续移除，本次保留作兜底工具函数。

## Assumptions & Decisions

1. **取消平移边界限制**（用户要求）：允许拖动出黑边；fit 状态下整图可在窗口里任意挪。
2. **`_view*` double 为视口状态真源**：不再依赖 `GetPart` 读状态，避免 int 截断累积 + SetPart/ImagePart 语义差 1，从根上修复放大。`_viewR2 - _viewR1 == 像素数`（不含边界，Rectangle 一致）；`SetPart` 用 `Floor(_viewR2)-1` 或 `Floor(_viewR2)` 均可（内部用 `Floor`，取保守的含边界）。
3. **缩放仍有约束**：最小视口 8px，最大缩放到 fit；仅**位置**钳制取消，**尺寸**限制保留。
4. **HALCON SetPart 舍入**：采用 `Math.Floor` 取整到最近的完整像素边界，避免 `(int)` 对负数朝零截断导致的视口尺寸偶尔变大/变小。
5. **范围外**：Resize 时重新 fit、双击 fit 按钮等功能不在本次范围。

## Verification Steps

1. **编译**：`MSBuild HToolBase.csproj /p:Configuration=Debug /p:Platform=x64`，0 error。
2. **平移放大回归**：选图 → 连续左右/上下拖动 >100 次（快速滑动半分钟），图像显示**完全不变大/不变小**（可通过坐标栏验证：左上角同样的窗口像素始终对应同一图像坐标）。
3. **取消边界**：fit 状态下拖动，整图应能在窗口中任意移动（出现黑边），不再原地不动。
4. **缩放仍有限**：滚轮后滚缩小到 fit 即止；前滚放大到视口 <8px 时停止，不崩溃。
5. **缩放+平移无漂移**：放大若干级后任意方向连续拖动 30s，放大级别保持不变（无持续放大/缩小）。
6. **坐标一致**：平移/缩放后，窗口同一像素位置的行/列显示值一致（用鼠标光标压在一条固定纹路边上来回拖坐标值保持 ±0.1px 内）。
7. **重绘同步**：切窗、最小化恢复后，画面状态与切前一致（ImagePart 同步正确）。
