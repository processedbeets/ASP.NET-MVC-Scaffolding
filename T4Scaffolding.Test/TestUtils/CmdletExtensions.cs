using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.VisualStudio.Test;
using T4Scaffolding.Cmdlets;

namespace T4Scaffolding.Test.TestUtils
{
    // Copied from NuPack.VisualStudio.Test because it's marked "interal" there
    internal static class CmdletExtensions
    {
        public static IEnumerable<T> GetResults<T>(this ScaffoldingBaseCmdlet cmdlet)
        {
            return GetResults(cmdlet).Cast<T>();
        }

        public static IEnumerable<object> GetResults(this ScaffoldingBaseCmdlet cmdlet)
        {
            var result = new List<object>();
            cmdlet.CommandRuntime = new MockCommandRuntime(result);
            try {
                cmdlet.Execute();
                return result;
            } catch(Exception ex) {
                ex.Data["CmdletOutput"] = result;
                throw;
            }
        }
    }
}