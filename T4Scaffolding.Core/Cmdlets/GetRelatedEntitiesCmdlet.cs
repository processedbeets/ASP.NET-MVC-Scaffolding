using System;
using System.Management.Automation;
using T4Scaffolding.Core.ProjectTypeLocators;
using T4Scaffolding.Core.RelatedEntityLocators;
using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "RelatedEntities")]
    public class GetRelatedEntitiesCmdlet : ScaffoldingBaseCmdlet
    {
        private readonly IProjectTypeLocator _projectTypeLocator;

        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string Type { get; set; }

        [Parameter]
        public string Project { get; set; }

        public GetRelatedEntitiesCmdlet() : this(null, new EnvDTETypeLocator()) { }
        internal GetRelatedEntitiesCmdlet(ISolutionManager solutionManager, IProjectTypeLocator projectTypeLocator)
            : base(solutionManager, null, null)
        {
            _projectTypeLocator = projectTypeLocator;
        }

        protected override void ProcessRecordCore()
        {
            if (string.IsNullOrEmpty(Type)) throw new InvalidOperationException("Specify a value for 'Type'.");

            var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
            if (project == null) {
                WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                return;
            }

            var foundType = _projectTypeLocator.FindUniqueType(project, Type);
            var relatedEntityInfos = RelatedEntityLocation.CurrentRelatedEntityLocator.GetRelatedEntities(foundType, project, _projectTypeLocator);
            WriteObject(relatedEntityInfos, true);
        }
    }
}