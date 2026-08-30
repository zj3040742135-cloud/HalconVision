using HToolBase.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HToolBase
{
    public struct Terminal
    {
        public ToolBlock FromToolBlock;
       public PortNode FromPort;
       public PortNode ToPort;
    }

    public class ToolBlockTerminal
    {
        ToolBlock ToolBlock;
        public Dictionary<string, Terminal> terminals=new Dictionary<string, Terminal>();
        ToolBlockTerminal(ToolBlock toolBlock)
        {
            ToolBlock=toolBlock;
        }
        public Dictionary<string, PortNode> GetInputValue()
        {
            Dictionary<string, PortNode> ports= new Dictionary<string, PortNode>();
            foreach (var value in terminals.Values) 
            {
                PortNode FromPort=  value.FromToolBlock.Outputs.Cast<PortNode>().FirstOrDefault(n => n == value.FromPort);
                PortNode ToPort = this.ToolBlock.Inputs.Cast<PortNode>().FirstOrDefault(n => n == value.ToPort);
                ToPort.Value = FromPort.Value;
                ports.Add(ToPort.Text,ToPort);
            }
            return ports;
        }
        public void AddToolBlockInput(string Name,ToolBlock FromToolBlock,PortNode FromPort)
        {
            if(terminals.ContainsKey(Name))
            {
                terminals.Remove(Name);
            }
            Terminal terminal = new Terminal();
            terminal.FromToolBlock = FromToolBlock;
            terminal.FromPort = FromPort;
            this.ToolBlock.AddInput(Name, FromPort.PortType);
            terminal.ToPort = this.ToolBlock.Inputs[Name];
            terminals.Add(Name, terminal);
        }
    }
}
