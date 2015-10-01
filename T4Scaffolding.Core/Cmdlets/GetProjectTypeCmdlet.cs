using System;
using System.Collections.Generic;
using System.Management.Automation;
using EnvDTE;
using T4Scaffolding.Core.ProjectTypeLocators;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.NuGetServices.Threading;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "ProjectType")]
    public class GetProjectTypeCmdlet : ScaffoldingBaseCmdlet
    {
        private readonly IProjectTypeLocator _projectTypeLocator;

        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string Type { get; set; }

        [Parameter]
        public string Project { get; set; }

        [Parameter]
        public SwitchParameter BlockUi { get; set; }

        [Parameter]
        public SwitchParameter AllowMultiple { get; set; }

        public GetProjectTypeCmdlet() : this(null, new EnvDTETypeLocator()) { }
        internal GetProjectTypeCmdlet(ISolutionManager solutionManager, IProjectTypeLocator projectTypeLocator)
            : base(solutionManager, null, null)
        {
            _projectTypeLocator = projectTypeLocator;
        }

        protected override void ProcessRecordCore()
        {
            var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
            if (project == null) {
                WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                return;
            }

            Func<IEnumerable<CodeType>> codeTypeFunc = AllowMultiple.IsPresent 
                ? () => _projectTypeLocator.FindTypes(project, Type)
                : (Func<IEnumerable<CodeType>>)(() => new[] { _projectTypeLocator.FindUniqueType(project, Type) });
            var codeType = codeTypeFunc();
            if (codeType != null)
                WriteObject(codeType, true);
        }
    }
}