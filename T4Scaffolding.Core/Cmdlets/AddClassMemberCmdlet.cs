using System.Management.Automation;
using EnvDTE;
using T4Scaffolding.Core.EnvDTE;

namespace T4Scaffolding.Cmdlets
{
    /// <summary>
    /// A simple wrapper around T4Scaffolding.Core.EnvDTE.EnvDTEExtensions.AddMemberFromSourceCode, since it's
    /// difficult to invoke extension methods from PowerShell (and even more difficult to pass the PowerShell
    /// COM object wrappers to them)
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "ClassMember")]
    public class AddClassMemberCmdlet : ScaffoldingBaseCmdlet
    {
        [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
        public PSObject CodeClass { get; set; }

        [Parameter(Mandatory = true, Position = 2, ValueFromPipelineByPropertyName = true)]
        public string SourceCode { get; set; }

        [Parameter]
        public object Position { get; set; }

        [Parameter]
        public int TextFormatOptions { get; set; }

        public AddClassMemberCmdlet() : base(null, null, null)
        {
            TextFormatOptions = (int)vsEPReplaceTextOptions.vsEPReplaceTextAutoformat; // Default if not specified
        }

        protected override void ProcessRecordCore()
        {
            var castCodeClass = CodeClass.BaseObject as CodeType;
            if (castCodeClass == null)
                WriteError("Unable to cast the supplied CodeClass value to the type EnvDTE.CodeType");
            else
                castCodeClass.AddMemberFromSourceCode(SourceCode, Position, (vsEPReplaceTextOptions)TextFormatOptions);
        }
    }
}