using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace T4Scaffolding.Test.TestUtils
{
    static class ExampleScripts
    {
        public static ExternalScriptInfo MakeExternalScriptInfo(Runspace powershellRunspace, string exampleScriptName)
        {
            var outputDirectory = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)).LocalPath;
            var exampleScriptPath = Path.Combine(outputDirectory, "ExampleScripts\\" + exampleScriptName + ".ps1");
            var cmd = string.Format("Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned; Get-Command \"{0}\"", exampleScriptPath);
            using (Pipeline pipeline = powershellRunspace.CreatePipeline(cmd))
            {
                var results = pipeline.Invoke();
                return results.First().BaseObject as ExternalScriptInfo;
            }
        }
    }
}
