using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TextTemplating;
using T4Scaffolding.Core.Templating;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class TemplatingTest
    {
        [TestMethod]
        public void ShouldBeAbleToReadDynamicModelProperties()
        {
            // Integration test to check that the T4 template host can read properties from the DynamicViewModel

            dynamic model = new DynamicViewModel();
            model.Name = "foo";
            model.Baz = 1;
            model.A = typeof(string);
            model.Values = new List<string> { "a", "b", "c" };

            var result = ProcessTemplate("simpleTemplate", model);
            Assert.AreEqual("The name is foo. There are 3 values.", result);
        }

        static string ProcessTemplate(string exampleTemplateName, DynamicViewModel model)
        {
            var templateQualifiedFileName = ExampleTemplates.GetPath(exampleTemplateName);

            if (!File.Exists(templateQualifiedFileName))
                throw new FileNotFoundException("File not found: " + exampleTemplateName);

            // Read the text template.
            string input = File.ReadAllText(templateQualifiedFileName);

            // Eliminate Inherits="DynamicTransform" from template directive
            input = Regex.Replace(input, @"\<\#\@\s*\bTemplate\b(.*?\b)?Inherits=""DynamicTransform""\s*(.*?)\#\>", @"<#@ Template $1 $2 #>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Append "Model" property (all example templates are C#)
            input += ModelPropertyClassFeatures.ModelPropertySourceForLanguage["cs"];

            // Transform the text template.
            using (var host = new DynamicTextTemplatingEngineHost {
                TemplateFile = templateQualifiedFileName,
                Model = model
            }) {
                string output = new Engine().ProcessTemplate(input, host);
                if (host.Errors.HasErrors)
                    throw new TemplateProcessingErrorException(host.Errors);
                return output;
            }
        }

        private class TemplateProcessingErrorException : Exception
        {
            public CompilerErrorCollection Errors { get; private set; }

            public TemplateProcessingErrorException(CompilerErrorCollection errors)
                : base(FormatMessage(errors))
            {
                Errors = errors;
            }

            private static string FormatMessage(CompilerErrorCollection errors)
            {
                var message = "One or more template processing errors occurred: " + Environment.NewLine;
                message += string.Join(Environment.NewLine, errors.Cast<CompilerError>().Select(x => x.ToString()));
                return message;
            }
        }
   
    }
}