using HalconDotNet;
using HToolBase;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HToolBase.Controls
{
    public partial class HDisplayControl : UserControl
    {
        HWindow HalconWindow;
        bool _isPanning = false;
        Point _lastMousePosition = new Point();
        // 当前显示的图像：由 ShowImage 拷贝持有，平移/缩放时直接复用，避免每次拖动重复 CopyImage
        HObject image = new HObject();
        // 多叠加层列表：每个 DisplayItem 独立配置颜色/Draw/线宽/可见性。
        // SetOverlays 替换全部；SetView 每次重绘遍历渲染。Data 由本控件独占副本。
        private List<DisplayItem> _overlays = new List<DisplayItem>();
        // 图像原始尺寸（像素），用于缩放最大缩小限制与坐标换算
        int _imgWidth = 0, _imgHeight = 0;
        // 视口状态真源：double 精度，避免 int 截断累积误差。长度 == 图像像素数（Rectangle 不含边界语义）。
        // ShowImage/平移/缩放都写这四个值，不再依赖 GetPart 截断返回值，消除「平移过程中图像放大」Bug。
        double _viewR1 = 0, _viewC1 = 0, _viewR2 = 0, _viewC2 = 0;

        public HDisplayControl()
        {
            InitializeComponent();
            AddEvent();
            HalconWindow = this.hWindow.HalconWindow;
            this.hWindow.BorderColor = Color.Gray;
        }
        private void AddEvent()
        {
            hWindow.MouseDown  += HWindow_MouseDown;
            hWindow.MouseMove  += HWindow_MouseMove;
            hWindow.MouseUp    += HWindow_MouseUp;
            hWindow.MouseWheel += HWindow_MouseWheel;
            hWindow.MouseEnter += HWindow_MouseEnter;
        }
        private void ClearEvent()
        {
            // 必须与 AddEvent 一一对应取消，避免控件释放后悬挂回调
            hWindow.MouseDown  -= HWindow_MouseDown;
            hWindow.MouseMove  -= HWindow_MouseMove;
            hWindow.MouseUp    -= HWindow_MouseUp;
            hWindow.MouseWheel -= HWindow_MouseWheel;
            hWindow.MouseEnter -= HWindow_MouseEnter;
        }

        private void HWindow_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && image != null && image.IsInitialized())
            {
                // 防御性同步：若 _view 未初始化(外部直接 SetPart 过或首次按下)，从 GetPart 读取初值并正确回算长度。
                // 正常情况下 ShowImage 已初始化 _view，不会进入此分支。
                if (_viewR2 - _viewR1 <= 0 || _viewC2 - _viewC1 <= 0)
                {
                    HalconWindow.GetPart(out int r1, out int c1, out int r2, out int c2);
                    // SetPart r2/c2 含边界 → Rectangle 像素数 = r2-r1+1；_view 采用不含边界的长度语义。
                    _viewR1 = r1; _viewC1 = c1; _viewR2 = r2 + 1; _viewC2 = c2 + 1;
                }
                _isPanning = true;
                _lastMousePosition = e.Location;
                Cursor = Cursors.Hand;
                hWindow.Capture = true;  // 光标移出控件仍持续平移
            }
        }
        private void HWindow_MouseMove(object sender, MouseEventArgs e)
        {
            // 始终更新底部坐标显示（图像坐标系）
            UpdateCoordinate(e.Location);

            if (!_isPanning) return;
            if (!HalconWindow.IsInitialized() || image == null || !image.IsInitialized()) return;

            int deltaX = e.X - _lastMousePosition.X;
            int deltaY = e.Y - _lastMousePosition.Y;
            if (deltaX == 0 && deltaY == 0) return;

            int viewW = hWindow.Width, viewH = hWindow.Height;
            if (viewW <= 0 || viewH <= 0) return;

            // 用 _view* 计算 scale（double→无截断损失），平移只改偏移、不改长度——彻底消除放大 Bug。
            double scaleR = (_viewR2 - _viewR1) / viewH;
            double scaleC = (_viewC2 - _viewC1) / viewW;
            double dR = deltaY * scaleR;
            double dC = deltaX * scaleC;

            // 取消边界限制，允许平移出图像（黑边无所谓）；视口四角整体加偏移，长度严格不变
            _viewR1 -= dR; _viewR2 -= dR;
            _viewC1 -= dC; _viewC2 -= dC;

            SetView();                 // 同步 SetPart + ImagePart + 重绘
            _lastMousePosition = e.Location;
        }
        private void HWindow_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                Cursor = Cursors.Default;
                hWindow.Capture = false;
            }
        }

        private void HWindow_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!HalconWindow.IsInitialized() || image == null || !image.IsInitialized()) return;
            if (hWindow.Width <= 0 || hWindow.Height <= 0 || _imgWidth <= 0 || _imgHeight <= 0) return;

            double viewH = _viewR2 - _viewR1, viewW = _viewC2 - _viewC1;
            if (viewH <= 0 || viewW <= 0) return;

            // 光标图像坐标（_view* 语义一致，无 int 差）——以光标为锚点缩放
            double imgR = _viewR1 + (double)e.Y / hWindow.Height * viewH;
            double imgC = _viewC1 + (double)e.X / hWindow.Width  * viewW;

            double f = e.Delta > 0 ? 0.8 : 1.25;
            double newR1 = imgR - (imgR - _viewR1) * f;
            double newR2 = imgR + (_viewR2 - imgR) * f;
            double newC1 = imgC - (imgC - _viewC1) * f;
            double newC2 = imgC + (_viewC2 - imgC) * f;
            double newViewH = newR2 - newR1, newViewW = newC2 - newC1;

            // 保留尺寸限制：最小 8px 防退化，最大缩放到 fit（再小无意义）
            if (newViewH < 8 || newViewW < 8) return;
            if (newViewH >= _imgHeight) { newR1 = 0; newR2 = _imgHeight; }
            if (newViewW >= _imgWidth)  { newC1 = 0; newC2 = _imgWidth;  }

            // 不再对位置 ClampToBounds（平移已无边界；允许出黑边）
            SetView(newR1, newC1, newR2, newC2);
        }

        private void HWindow_MouseEnter(object sender, EventArgs e)
        {
            // WinForms 鼠标滚轮只派发给焦点控件，进入控件时聚焦以接收 Wheel
            hWindow.Focus();
        }

        /// <summary>更新 _view* 字段并立即重绘（指定参数版）。</summary>
        private void SetView(double r1, double c1, double r2, double c2)
        {
            _viewR1 = r1; _viewC1 = c1; _viewR2 = r2; _viewC2 = c2;
            SetView();
        }

        /// <summary>按当前 _view* 状态同步 SetPart + ImagePart 并重绘。
        /// 正确处理 SetPart(含边界) ↔ ImagePart(不含边界) 的像素语义差，避免 int 截断累积误差。</summary>
        private void SetView()
        {
            if (!HalconWindow.IsInitialized() || image == null || !image.IsInitialized()) return;
            // Floor 取整：保证 SetPart 含边界像素覆盖整个 _view 跨度，且不向负方向偏出（(int)负数会朝 0 截断）。
            int ir1 = (int)Math.Floor(_viewR1);
            int ic1 = (int)Math.Floor(_viewC1);
            int ir2 = (int)Math.Floor(_viewR2);
            int ic2 = (int)Math.Floor(_viewC2);
            HalconWindow.SetPart(ir1, ic1, ir2, ic2);
            // ImagePart 采用 Rectangle 语义：Width/Height 为像素总数 = _view 跨度
            int w = (int)Math.Max(1, Math.Round(_viewC2 - _viewC1));
            int h = (int)Math.Max(1, Math.Round(_viewR2 - _viewR1));
            hWindow.ImagePart = new Rectangle(ic1, ir1, w, h);
            HalconWindow.ClearWindow();
            HalconWindow.DispObj(image);
            // 渲染所有可见叠加层：每个独立设置颜色/Draw/线宽。
            // HXLD/HXLDCont 均继承自 HObject，DispObj 直接支持；SetDraw 仅对 region 生效，XLD 忽略。
            if (_overlays != null)
            {
                foreach (var o in _overlays)
                {
                    if (!o.Visible || o.Data == null || !o.Data.IsInitialized()) continue;
                    HalconWindow.SetLineWidth(o.LineWidth);
                    HalconWindow.SetColor(o.Color);
                    if (o.Type == TypeName.REGION)
                        HalconWindow.SetDraw(o.Draw);
                    HalconWindow.DispObj(o.Data);
                }
            }
        }

        /// <summary>把 [a,b] 区间平移到不超出 [0, max]（区间长度不变）。区间比范围大时贴满 [0,max]。
        /// 暂留作工具函数（当前平移无边界，不调用；将来若加「回图像中心」可复用）。</summary>
        private static void ClampToBounds(ref double a, ref double b, int max)
        {
            if (max < 0) { a = 0; b = 0; return; }
            double len = b - a;
            if (len > max) { a = 0; b = max; return; }
            if (a < 0) { a = 0; b = len; }
            if (b > max) { b = max; a = max - len; }
        }

        /// <summary>在底部 textBox 显示鼠标当前位置对应的图像坐标（行/列）。
        /// 改读 _view*（double 精度、Rectangle 语义），无需 GetPart 截断值。</summary>
        private void UpdateCoordinate(Point controlPt)
        {
            if (image == null || !image.IsInitialized()) return;
            if (!HalconWindow.IsInitialized() || hWindow.Width <= 0 || hWindow.Height <= 0) return;
            try
            {
                double imgR = _viewR1 + (double)controlPt.Y / hWindow.Height * (_viewR2 - _viewR1);
                double imgC = _viewC1 + (double)controlPt.X / hWindow.Width  * (_viewC2 - _viewC1);
                textBox1.Text = $"行：{imgR:F1}  列：{imgC:F1}";
            }
            catch { }
        }

        public void ShowImage(HObject Image)
        {
            if (Image != null && Image.IsInitialized() && HalconWindow.IsInitialized())
            {
                HalconWindow.ClearWindow();
                image?.Dispose();
                HOperatorSet.CopyImage(Image, out image);
                HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
                _imgWidth = width.I;
                _imgHeight = height.I;
                // fit 到整图：_view 采用 Rectangle 不含边界语义，R2/C2 = 图像像素总数
                SetView(0, 0, _imgHeight, _imgWidth);
            }
        }
        [Obsolete("使用 SetOverlays(IEnumerable<DisplayItem>) 替代，支持多叠加层与独立配置。本方法转发为单个 region 叠加层。")]
        public void SetRegion(HObject Region, string Draw = "margin", string color = "red")
        {
            if (HalconWindow.IsInitialized() && Region != null && Region.IsInitialized())
            {
                var item = new DisplayItem
                {
                    Name = "LegacyRegion",
                    Type = TypeName.REGION,
                    Color = color,
                    Draw = Draw,
                    LineWidth = 1.0,
                    Data = Region        // setter 深拷贝
                };
                SetOverlays(new[] { item });
            }
        }

        /// <summary>替换全部叠加层（深拷贝 Data，断开与 ToolBlock 持有引用的共享）。
        /// 传 null 或空集合则清空所有叠加层并重绘。</summary>
        public void SetOverlays(IEnumerable<DisplayItem> items)
        {
            // 释放旧叠加层 Data
            if (_overlays != null)
            {
                foreach (var o in _overlays)
                {
                    if (o.Data is HObject h && h.IsInitialized()) { try { h.Dispose(); } catch { } }
                }
                _overlays.Clear();
            }
            if (items != null)
            {
                foreach (var src in items)
                {
                    if (src == null) continue;
                    // 克隆配置 + 深拷贝 Data（SetDataRaw 跳过 setter 二次拷贝）
                    var copy = src.CloneConfig();
                    if (src.Data is HObject d && d.IsInitialized())
                    {
                        HOperatorSet.CopyObj(d, out HObject c, 1, -1);
                        copy.SetDataRaw(c);
                    }
                    _overlays.Add(copy);
                }
            }
            SetView();
        }
    }
}
