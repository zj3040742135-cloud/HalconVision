using HalconDotNet;
using HAttribute;
using HToolBase.Tools;
using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;

namespace HToolBase.Controls
{
    /// <summary>
    /// 脚本宿主抽象：统一 Form1（ScriptTool，端口型）与 Form2（ToolBlock 块级脚本）两种形态。
    /// ScriptToolForm 仅依赖此接口，不关心宿主具体类型。
    /// </summary>
    public interface IScriptHost
    {
        /// <summary>脚本代码文本（get/set 持久化）</summary>
        string ScriptText { get; set; }
        /// <summary>本宿主专属的编译器实例（保留编译缓存）</summary>
        ScriptExecutor Executor { get; }
        /// <summary>传给脚本类构造函数的实参（Form1=ScriptTool 自身；Form2=ToolBlock 自身）</summary>
        object ScriptArgument { get; }
        /// <summary>窗体标题</summary>
        string HostTitle { get; }
        /// <summary>智能联想中的"已创建对象名"列表（Form1=端口名；Form2=兄弟工具名）</summary>
        IEnumerable<string> GetCreatedNames();
        /// <summary>默认示例脚本（按宿主类型给出对应的 using/构造签名）</summary>
        string GetExampleScript();

        // —— Form2（块级）专用：替换 ToolBlock.Run ——
        /// <summary>是否支持切换"脚本运行模式"（仅 Form2=true）</summary>
        bool CanSwitchRunMode { get; }
        /// <summary>是否启用脚本运行模式（替换 ToolBlock 默认 Run）</summary>
        bool UseScriptRun { get; set; }

        // —— Form1（端口型）专用：端口增删 ——
        /// <summary>是否支持端口增删（仅 Form1=true）</summary>
        bool CanManagePorts { get; }
        void AddPort(bool isInput, TypeName type);
        void RemovePort(string portName, bool isInput);
        IEnumerable<PortNode> GetPorts(bool isInput);
    }

    /// <summary>
    /// C# 脚本动态编译/执行器。**非单例**——每个 ScriptTool / ToolBlock 各持一个实例，
    /// 避免多个脚本互相覆盖编译结果。引用程序集路径全部动态解析，去硬编码。
    /// 脚本约定：定义一个公共类（通常名 ToolScript），含构造函数 ToolScript(宿主类型 arg)
    /// 与 public string Run() 方法；Run() 返回串作为执行结果。
    /// </summary>
    public class ScriptExecutor
    {
        private Assembly _compiledAssembly;
        private Type _compiledType;
        private string _lastCompiledScript;

        public bool HasCompiledScript => _compiledType != null;

        /// <summary>强制重新编译（用于编辑器"执行脚本"按钮，需展示最新错误）。</summary>
        public bool CompileScript(string script, out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(script))
            {
                errors.Add("脚本内容为空");
                return false;
            }

            _compiledType = null;
            _compiledAssembly = null;
            _lastCompiledScript = script;

            using (var provider = new CSharpCodeProvider())
            {
                var parameters = new CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false,
                    TreatWarningsAsErrors = false
                };
                // 引用程序集：动态解析路径，避免硬编码绝对路径
                parameters.ReferencedAssemblies.Add("System.dll");
                parameters.ReferencedAssemblies.Add("System.Core.dll");
                parameters.ReferencedAssemblies.Add("System.Windows.Forms.dll");
                parameters.ReferencedAssemblies.Add("System.Drawing.dll");
                parameters.ReferencedAssemblies.Add(typeof(HObject).Assembly.Location);                  // halcondotnet
                parameters.ReferencedAssemblies.Add(typeof(ToolBase).Assembly.Location);                  // HToolBase
                parameters.ReferencedAssemblies.Add(typeof(FieldInfoTagAttribute).Assembly.Location);     // HAttribute
                parameters.ReferencedAssemblies.Add(typeof(Newtonsoft.Json.JsonConvert).Assembly.Location);

                CompilerResults result;
                try
                {
                    result = provider.CompileAssemblyFromSource(parameters, script);
                }
                catch (Exception ex)
                {
                    errors.Add("编译器异常: " + ex.Message);
                    return false;
                }

                if (result.Errors.HasErrors)
                {
                    foreach (CompilerError err in result.Errors)
                        errors.Add($"行{err.Line}: {err.ErrorText}");
                    return false;
                }

                _compiledAssembly = result.CompiledAssembly;
            }

            // 查找含 public string Run() 的公共类（优先名 ToolScript）
            Type found = null;
            foreach (var t in _compiledAssembly.GetTypes())
            {
                if (!t.IsPublic) continue;
                var m = t.GetMethod("Run", Type.EmptyTypes);
                if (m != null && m.ReturnType == typeof(string))
                {
                    found = t;
                    if (t.Name == "ToolScript") break;
                }
            }
            if (found == null)
            {
                errors.Add("未找到含 public string Run() 方法的公共类（类名通常为 ToolScript）");
                return false;
            }
            _compiledType = found;
            return true;
        }

        /// <summary>仅当脚本文本变化时才重新编译（用于 Run() 高频调用，复用编译缓存）。</summary>
        public bool CompileIfChanged(string script, out List<string> errors)
        {
            errors = new List<string>();
            if (_compiledType != null && _lastCompiledScript == script)
                return true;
            return CompileScript(script, out errors);
        }

        /// <summary>用缓存编译结果执行脚本，传入宿主实参。失败返回错误串。</summary>
        public string RunCompiledScript(object arg)
        {
            if (_compiledType == null)
                return "未编译脚本";
            try
            {
                var instance = Activator.CreateInstance(_compiledType, arg);
                var method = _compiledType.GetMethod("Run", Type.EmptyTypes);
                if (method == null)
                    return "脚本缺少 public string Run() 方法";
                var result = method.Invoke(instance, null);
                return result?.ToString() ?? "";
            }
            catch (TargetInvocationException tie)
            {
                return "脚本执行异常: " + (tie.InnerException?.Message ?? tie.Message);
            }
            catch (Exception ex)
            {
                return "脚本执行异常: " + ex.Message;
            }
        }

        /// <summary>清空编译缓存（脚本文本变更后强制下次重编译）。</summary>
        public void Invalidate()
        {
            _compiledType = null;
            _compiledAssembly = null;
            _lastCompiledScript = null;
        }
    }
}
