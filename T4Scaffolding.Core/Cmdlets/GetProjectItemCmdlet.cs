using System.Management.Automation;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.NuGetServices.ExtensionMethods;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "ProjectItem")]
    public class GetProjectItemCmdlet : ScaffoldingBaseCmdlet
    {
        public GetProjectItemCmdlet() : this(null) { }
        internal GetProjectItemCmdlet(ISolutionManager solutionManager) : base(solutionManager, null, null)
        {
        }

        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        public string Path { get; set; }

        [Parameter]
        public string Project { get; set; }

        protected override void ProcessRecordCore()
        {
            var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
            if (project == null) {
                WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                return;
            }

            var result = project.GetProjectItem(Path);
            if (result != null)
                WriteObject(result);
        }
    }
}
