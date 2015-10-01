using System.Management.Automation;
using T4Scaffolding.Core;
using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "ProjectLanguage")]
    public class GetProjectLanguageCmdlet : ScaffoldingBaseCmdlet
    {
        [Parameter]
        public string Project { get; set; }

        public GetProjectLanguageCmdlet() : this(null) { }
        internal GetProjectLanguageCmdlet(ISolutionManager solutionManager) : base(solutionManager, null, null)
        {
        }

        protected override void ProcessRecordCore()
        {
            var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
            if (project == null) {
                WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                return;
            }

            var result = project.GetCodeLanguage();
            if (result != null)
                WriteObject(result);
        }
    }
}
