using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Linq;

namespace T4Scaffolding.Cmdlets
{
    /// <summary>
    /// A wrapper around Find-ScaffolderTemplate and Invoke-ScaffolderTemplate to simplify many of the scaffolder PowerShell scripts
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "ProjectItemViaTemplate")]
    public class AddProjectItemViaTemplateCmdlet : ScaffoldingBaseCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        public string OutputPath { get; set; }

        [Parameter(Mandatory = true)]
        public string Template { get; set; }

        [Parameter(Mandatory = true)]
        public Hashtable Model { get; set; }

        [Parameter(Mandatory = true)]
        public object[] TemplateFolders { get; set; } // Using object[] because it's hard to pass any other type of collection from PowerShell

        [Parameter]
        public string SuccessMessage { get; set; }

        [Parameter]
        public string Project { get; set; }

        [Parameter]
        public string CodeLanguage { get; set; }

        [Parameter]
        public SwitchParameter Force { get; set; }

        public AddProjectItemViaTemplateCmdlet() : base(null, null, null)
        {
            SuccessMessage = "Added item at {0}";
        }

        protected override void ProcessRecordCore()
        {
            // Find the template, or bail out
            var templateFilename = InvokeCmdletCaptureOutput<string>("Find-ScaffolderTemplate", new Hashtable {
                { "Template", Template },
                { "TemplateFolders", TemplateFolders },
                { "Project", Project },
                { "CodeLanguage", CodeLanguage },
                { "ErrorIfNotFound", SwitchParameter.Present }
            }).FirstOrDefault();
            if (string.IsNullOrEmpty(templateFilename))
                return;

            // Invoke the template
            var invokeTemplateArgs = new Hashtable {
                { "Template", templateFilename },
                { "Model", Model },
                { "Project", Project },
                { "OutputPath", OutputPath }
            };
            if (Force.IsPresent)
                invokeTemplateArgs.Add("Force", SwitchParameter.Present);
            var invokeTemplateResult = InvokeCmdletCaptureOutput<object>("Invoke-ScaffoldTemplate", invokeTemplateArgs).ToList();

            // Either display a message for success, or pipe the result to the output or error stream
            if (invokeTemplateResult.Any()) {
                if ((invokeTemplateResult.Count == 1) && invokeTemplateResult.All(x => x is string)) {
                    // Success
                    var message = string.Format(SuccessMessage, (string)invokeTemplateResult.Single() ?? string.Empty);
                    InvokeCmdletCaptureOutput<object>("Write-Host", new Hashtable { { "Object", message } });
                } else if (invokeTemplateResult.All(x => x is CompilerError)) {
                    // Failure: Compiler error
                    foreach (CompilerError compilerError in invokeTemplateResult) {
                        WriteError(compilerError.ToString());
                    }
                } else {
                    // Unexpected output
                    WriteObject(invokeTemplateResult, true);
                }
            }
        }
    }
}