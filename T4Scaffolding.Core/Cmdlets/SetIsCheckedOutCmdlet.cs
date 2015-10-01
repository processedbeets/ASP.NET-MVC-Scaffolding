using System;
using System.Management.Automation;
using T4Scaffolding.Core;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.NuGetServices.ExtensionMethods;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Set, "IsCheckedOut")]
    public class SetIsCheckedOutCmdlet : ScaffoldingBaseCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        public string Path { get; set; }

        [Parameter]
        public string Project { get; set; }

        public SetIsCheckedOutCmdlet() : this(null)
        {
        }

        public SetIsCheckedOutCmdlet(ISolutionManager solutionManager) : base(solutionManager, null, null)
        {
        }

        protected override void ProcessRecordCore()
        {
            if (string.IsNullOrEmpty(Path))
                throw new InvalidOperationException("Specify a value for 'Path'");

            // Resolve project-relative paths, as long as we know what project to look inside
            if (!System.IO.Path.IsPathRooted(Path)) {
                var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
                if (project == null) {
                    WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                    return;
                }

                var projectDir = System.IO.Path.GetDirectoryName(project.GetFullPath());
                Path = System.IO.Path.Combine(projectDir, Path);
            }

            SolutionManager.EnsureCheckedOutIfExists(Path);
        }
    }
}
