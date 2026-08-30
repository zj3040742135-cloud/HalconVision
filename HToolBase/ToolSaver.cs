using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HToolBase
{
    /// <summary>
    /// 端口保存数据（可序列化）
    /// </summary>
    public class PortSaveData
    {
        public string Name { get; set; }
        public string PortType { get; set; }
        public DynamicItem Value { get; set; }
    }

    /// <summary>
    /// 连线保存数据（可序列化，用工具名和端口名引用）
    /// </summary>
    public class ConnectionSaveData
    {
        public string FromToolName { get; set; }
        public string FromPortName { get; set; }
        public string ToToolName { get; set; }
        public string ToPortName { get; set; }
        public string PortType { get; set; }
    }

    /// <summary>
    /// ToolBlock完整保存数据（包含工具、ToolBlock自身端口、连线）
    /// </summary>
    public class ToolSaveData
    {
        public List<Dictionary<string, DynamicItem>> Tools { get; set; } = new List<Dictionary<string, DynamicItem>>();
        public List<PortSaveData> ToolInputs { get; set; } = new List<PortSaveData>();
        public List<PortSaveData> ToolOutputs { get; set; } = new List<PortSaveData>();
        public List<ConnectionSaveData> Connections { get; set; } = new List<ConnectionSaveData>();

        // 形态2：ToolBlock 块级脚本（文本 + 运行模式开关）。纯字符串/标量，无 HObject 膨胀问题。
        public string BlockScriptText { get; set; } = string.Empty;
        public bool UseScriptRun { get; set; } = false;
    }
}
