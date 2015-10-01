using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Management.Automation;
using System.Linq;
using EnvDTE;
using T4Scaffolding.Core.EnvDTE;

namespace T4Scaffolding.Cmdlets
{
    /// <summary>
    /// A wrapper around Find-ScaffolderTemplate, Invoke-ScaffolderTemplate, and Add-ClassMember to simplify many of the scaffolder PowerShell scripts
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "ClassMemberViaTemplate")]
    public class AddClassMemberViaTemplateCmdlet : ScaffoldingBaseCmdlet
    {
        [Parameter]
        public string Name { get; set; }

        [Parameter(Mandatory = true)]
        public PSObject CodeClass { get; set; }

        [Parameter(Mandatory = true)]
        public string Template { get; set; }

        [Parameter(Mandatory = true)]
        public Hashtable Model { get; set; }

        [Parameter]
        public string SuccessMessage { get; set; }

        [Parameter(Mandatory = true)]
        public object[] TemplateFolders { get; set; } // Using object[] because it's hard to pass any other type of collection from PowerShell

        [Parameter]
        public string Project { get; set; }

        [Parameter]
        public string CodeLanguage { get; set; }

        [Parameter]
        public SwitchParameter Force { get; set; }

        public AddClassMemberViaTemplateCmdlet() : base(null, null, null)
        {
            SuccessMessage = "Added member {0} to {1}";
        }

        protected override void ProcessRecordCore()
        {
            // If the member already exists, and we don't have -Force, bail out
            var castCodeClass = CodeClass.BaseObject as CodeType;
            if (castCodeClass == null) {
                throw new InvalidOperationException("Unable to cast the supplied CodeClass value to the type EnvDTE.CodeType");
            }
            if ((!string.IsNullOrEmpty(Name)) && !Force.IsPresent) {
                if (((CodeType)castCodeClass).VisibleMembers().OfType<CodeElement>().Any(x => x.Name.Equals(Name, StringComparison.Ordinal))) {
                    WriteWarning(string.Format("{0} already has a member called '{1}'. Skipping...", castCodeClass.Name, Name));
                    return;
                }
            }

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
                { "Project", Project }
            };
            var invokeTemplateResult = InvokeCmdletCaptureOutput<object>("Invoke-ScaffoldTemplate", invokeTemplateArgs).ToList();

            // Either display a message for success, or pipe the result to the output or error stream
            if (invokeTemplateResult.Any()) {
                if ((invokeTemplateResult.Count == 1) && invokeTemplateResult.All(x => x is string)) {
                    // Success: Now add this as a class member
                    var memberSourceCode = (string)invokeTemplateResult.Single();
                    InvokeCmdletCaptureOutput<object>("Add-ClassMember", new Hashtable {
                        { "CodeClass", CodeClass },
                        { "SourceCode", memberSourceCode },
                        { "TextFormatOptions", default(vsEPReplaceTextOptions) }
                    });

                    var message = string.Format(SuccessMessage, Name ?? string.Empty, castCodeClass.Name);
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