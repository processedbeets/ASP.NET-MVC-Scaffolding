using System;
using System.Linq;
using System.Management.Automation;
using EnvDTE;
using T4Scaffolding.Core.EnvDTE;
using T4Scaffolding.Core.PrimaryKeyLocators;
using T4Scaffolding.Core.ProjectTypeLocators;
using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "PrimaryKey")]
    public class GetPrimaryKeyCmdlet : ScaffoldingBaseCmdlet
    {
        private readonly IProjectTypeLocator _projectTypeLocator;

        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string Type { get; set; }

        [Parameter]
        public string Project { get; set; }

        [Parameter]
        public SwitchParameter ErrorIfNotFound { get; set; }

        public GetPrimaryKeyCmdlet() : this(null, new EnvDTETypeLocator()) { }
        internal GetPrimaryKeyCmdlet(ISolutionManager solutionManager, IProjectTypeLocator projectTypeLocator)
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

            var foundClass = _projectTypeLocator.FindUniqueType(project, Type);
            var primaryKeyProperties = PrimaryKeyLocation.GetPrimaryKeys(foundClass).ToList();
            switch (primaryKeyProperties.Count)
            {
                case 0:
                    if (ErrorIfNotFound) {
                        WriteError(string.Format("Cannot find primary key property for type '{0}'. No properties appear to be primary keys.", foundClass.FullName));
                    }
                    break;
                case 1:
                    WriteObject(primaryKeyProperties.Single().Name);
                    break;
                default:
                    if (ErrorIfNotFound) {
                        var primaryKeyPropertyNames = string.Join(", ", primaryKeyProperties.Select(x => x.Name));
                        WriteError(string.Format("Cannot find primary key property for type '{0}'. Multiple properties appear to be primary keys: {1}", foundClass.FullName, primaryKeyPropertyNames));
                    }
                    break;
            }
        }
    }
}