using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace T4Scaffolding.Test.TestUtils
{
    internal static class ExampleTemplates
    {
        public static string GetPath(string templateName)
        {
            var outputDirectory = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)).LocalPath;
            return Path.Combine(outputDirectory, "ExampleTemplates\\" + templateName + ".t4");
        }

        public static string GetContents(string templateName)
        {
            return File.ReadAllText(GetPath(templateName));
        }
    }
}
