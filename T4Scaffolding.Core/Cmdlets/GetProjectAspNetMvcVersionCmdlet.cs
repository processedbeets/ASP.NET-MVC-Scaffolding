using System;
using System.Linq;
using System.Management.Automation;
using T4Scaffolding.Core;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.NuGetServices.ExtensionMethods;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "ProjectAspNetMvcVersion")]
    public class GetProjectAspNetMvcVersionCmdlet : ScaffoldingBaseCmdlet
    {
        [Parameter]
        public string Project { get; set; }

        public GetProjectAspNetMvcVersionCmdlet() : this(null) { }
        internal GetProjectAspNetMvcVersionCmdlet(ISolutionManager solutionManager) : base(solutionManager, null, null)
        {
        }

        protected override void ProcessRecordCore()
        {
            var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
            if (project == null) {
                WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                return;
            }

            var projectTypeGuids = project.GetProjectTypeGuids();
            if (projectTypeGuids.Contains(VsConstants.Mvc3ProjectTypeGuid, StringComparer.OrdinalIgnoreCase)) {
                WriteObject(3);
            } else if (projectTypeGuids.Contains(VsConstants.Mvc2ProjectTypeGuid, StringComparer.OrdinalIgnoreCase)) {
                WriteObject(2);
            } else if (projectTypeGuids.Contains(VsConstants.Mvc1ProjectTypeGuid, StringComparer.OrdinalIgnoreCase)) {
                WriteObject(1);
            }
        }
    }
}