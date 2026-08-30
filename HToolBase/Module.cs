using HToolBase.Controls;
using HToolBase.Tools;
﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
namespace HToolBase
{
    public abstract class Module
    {
        public string ModelId { get; set; } = Guid.NewGuid().ToString();
        public System.Drawing.Point Position { get; set; }
        public Module Input { get; set; }
        public Module Output { get; set; }
        public List<Rectangle> Rects = new List<Rectangle>();
        public bool IsExecuted = false;
        public string Name { get; set; }
        public string Text { get; set; }
        public abstract void Run();
        public Rectangle Inputrectangle { get; set; }
        public Rectangle Outputrectangle { get; set; }
        public List<Rectangle> SetRect(System.Drawing.Point point, bool isStart)
        {
            this.Position = point;
            Rects.Clear();
            var mainRect = new Rectangle(
                x: point.X - 50,
                y: point.Y - 40,
                width: 80,
                height: 50
            );
            Rects.Add(mainRect);
            SetTerminalRects(mainRect, isStart);
            return Rects;
        }
        private void SetTerminalRects(Rectangle mainRect, bool isStart)
        {
            if (!isStart)
            {
                Inputrectangle = new Rectangle(
                    x: mainRect.X + mainRect.Width / 2 - 10,
                    y: mainRect.Y - 10,
                    width: 20,
                    height: 10
                );
                Rects.Add(Inputrectangle);
            }
            Outputrectangle = new Rectangle(
                x: mainRect.X + mainRect.Width / 2 - 10,
                y: mainRect.Y + mainRect.Height,
                width: 20,
                height: 10
            );
            Rects.Add(Outputrectangle);
        }
    }
    public class StartModule : Module
    {
        public StartModule()
        {
            Name = "开始";
            Text = "开始";
        }
        public override void Run()
        {
            IsExecuted = true;
        }
    }
    public class ToolModule : Module, IDisposable
    {
        ToolBlock toolBlock;
        public ToolModule()
        {
            this.Name = "ToolModule";
            toolBlock = new ToolBlock();
        }
        public override void Run()
        {
            toolBlock.Run();
            IsExecuted = true;
        }
        public void Show()
        {

            ToolBlockControl f=new ToolBlockControl();
            f.tool = this.toolBlock;
            f.Show();
            
        }
        public ToolBlock GetToolBlock()
        {
            return toolBlock;
        }
        /// <summary>
        /// 释放ToolModule关联的所有对象与属性：ToolBlock及其内部资源、基类引用
        /// </summary>
        public void Dispose()
        {
            // 1. 释放ToolBlock及其所有关联资源（窗体、工具、端口、连线、事件订阅）
            toolBlock?.Dispose();
            toolBlock = null;

            // 2. 断开基类持有的上下游模块引用，避免悬挂引用
            Input = null;
            Output = null;

            // 3. 清空基类集合与状态
            Rects?.Clear();
            IsExecuted = false;
        }
    }
}
