using System;
using System.Linq;
using System.Management.Automation;
using T4Scaffolding.Core.Configuration;
using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "DefaultScaffolder")]
    public class GetDefaultScaffolderCmdlet : ScaffoldingBaseCmdlet
    {
        private readonly Lazy<IScaffoldingConfigStore> _configStore;

        [Parameter(Position = 0, ValueFromPipelineByPropertyName = true)]
        public string Name { get; set; }

        [Parameter]
        public string Project { get; set; }

        public GetDefaultScaffolderCmdlet() : this(null, null, null)
        {
        }

        internal GetDefaultScaffolderCmdlet(ISolutionManager solutionManager, IVsPackageManagerFactory packageManagerFactory, IScaffoldingConfigStore configStore)
            : base(solutionManager, packageManagerFactory, null)
        {
            _configStore = new Lazy<IScaffoldingConfigStore>(() => {
                return configStore ?? new XmlScaffoldingConfigStore(SolutionManager);
            });
        }

        protected override void ProcessRecordCore()
        {
            var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
            if (project == null) {
                WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                return;
            }

            // First get solution entries
            var results = (from entry in _configStore.Value.GetSolutionDefaultScaffolders()
                           where string.IsNullOrEmpty(Name) || entry.DefaultName.Equals(Name, StringComparison.OrdinalIgnoreCase)
                           select entry).ToDictionary(x => x.DefaultName, StringComparer.OrdinalIgnoreCase);

            // Then overlay the project entries
            var projectEntries = (from entry in _configStore.Value.GetProjectDefaultScaffolders(project)
                                  where string.IsNullOrEmpty(Name) || entry.DefaultName.Equals(Name, StringComparison.OrdinalIgnoreCase)
                                  select entry).ToDictionary(x => x.DefaultName, StringComparer.OrdinalIgnoreCase);
            foreach (var projectEntry in projectEntries) {
                results[projectEntry.Key] = projectEntry.Value;
            }

            WriteObject(results.Select(x => x.Value), true);
        }
    }
}
