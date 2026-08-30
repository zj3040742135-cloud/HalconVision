using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HToolBase
{
    public class ProcessManager
    {
        private ProcessManager() { }
        private static ProcessManager processManager;
        private static readonly object _lock=new object();
        public Dictionary<string,ProcessPanel> Processes = new Dictionary<string,ProcessPanel>();
        public ProcessPanel currentProcess=new ProcessPanel();
        public int ProcessIndex = -1;
        public static ProcessManager instance()
        {
            lock (_lock)
            {
                if (processManager == null)
                {
                    processManager = new ProcessManager();
                }
                return processManager;

            }
        }
    }
}
