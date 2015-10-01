using System;
using System.Linq;
using System.Management.Automation;
using EnvDTE;
using T4Scaffolding.Core.Configuration;
using T4Scaffolding.Core.FileSystem;
using T4Scaffolding.Core.ScaffolderLocators;
using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Set, "DefaultScaffolder")]
    public class SetDefaultScaffolderCmdlet : ScaffoldingBaseCmdlet
    {
        private readonly Lazy<IScaffoldingConfigStore> _configStore;
        private readonly Lazy<IScaffolderLocator> _scaffolderLocator;

        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string Name { get; set; }

        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string Scaffolder { get; set; }

        [Parameter]
        public string Project { get; set; }

        [Parameter]
        public SwitchParameter SolutionWide { get; set; }

        [Parameter]
        public SwitchParameter DoNotOverwriteExistingSetting { get; set; }

        public SetDefaultScaffolderCmdlet() : this(null, null, null, null)
        {
        }

        internal SetDefaultScaffolderCmdlet(ISolutionManager solutionManager, IVsPackageManagerFactory packageManagerFactory, IScaffoldingConfigStore configStore, IScaffolderLocator scaffolderLocator)
            : base(solutionManager, packageManagerFactory, null)
        {
            _configStore = new Lazy<IScaffoldingConfigStore>(() => {
                return configStore ?? new XmlScaffoldingConfigStore(SolutionManager);
            });

            // Can't read the value of CommandInvoker until *after* the constructor finishes, hence lazy
            _scaffolderLocator = new Lazy<IScaffolderLocator>(
                () => scaffolderLocator ?? new Ps1ScaffolderLocator(CommandInvoker, PackageManager, null, new DefaultFileSystem(), _configStore.Value)
            );
        }

        protected override void ProcessRecordCore()
        {
            if (string.IsNullOrEmpty(Name)) throw new InvalidOperationException("No value was provided for the 'Name' parameter");
            if (string.IsNullOrEmpty(Scaffolder)) throw new InvalidOperationException("No value was provided for the 'Scaffolder' parameter");
            if (SolutionWide.ToBool() && !string.IsNullOrEmpty(Project)) throw new InvalidOperationException("Do not specify any -Project parameter when writing solution-wide settings");

            Project project = null;
            if (!SolutionWide.ToBool()) {
                project = string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProject : SolutionManager.GetProject(Project);
                if (project == null) {
                    WriteError(string.Format("Could not find project '{0}'", Project ?? ""));
                    return;
                }
            }

            // Ensure this is the name of a *default*, not a scaffolder
            var clashesWithActualScaffolder = SolutionWide.ToBool() ? SolutionManager.GetProjects().Any(proj => _scaffolderLocator.Value.GetScaffolders(proj, Name, false).Any()) 
                                                                    : _scaffolderLocator.Value.GetScaffolders(project, Name, false).Any();
            if (clashesWithActualScaffolder)
                throw new InvalidOperationException(string.Format("Cannot use the default name '{0}' because this clashes with an actual scaffolder name", Name));

            var scaffoldersMatchingRequest = _scaffolderLocator.Value.GetScaffolders(project, Scaffolder, false).ToList();
            switch (scaffoldersMatchingRequest.Count) {
                case 0: throw new InvalidOperationException(string.Format("Could not find scaffolder '{0}'", Scaffolder));
                case 1: SetDefaultInternal(project, scaffoldersMatchingRequest.Single().Name); break;
                default: throw new InvalidOperationException(string.Format("Ambiguous match: Multiple scaffolders match the name '{0}'", Scaffolder));
            }
        }

        private void SetDefaultInternal(Project project, string scaffolderName)
        {
            if (project != null) {
                _configStore.Value.SetProjectDefaultScaffolder(project, Name, scaffolderName, DoNotOverwriteExistingSetting.ToBool());
            } else {
                _configStore.Value.SetSolutionDefaultScaffolder(Name, scaffolderName, DoNotOverwriteExistingSetting.ToBool());
            }
        }
    }
}
