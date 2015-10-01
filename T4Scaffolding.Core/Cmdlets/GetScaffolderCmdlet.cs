using System;
using System.Linq;
using System.Management.Automation;
using T4Scaffolding.Core.Configuration;
using T4Scaffolding.Core.FileSystem;
using T4Scaffolding.Core.ScaffolderLocators;
using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "Scaffolder")]
    public class GetScaffolderCmdlet : ScaffoldingBaseCmdlet
    {
        private readonly Lazy<IScaffolderLocator> _scaffolderLocator;

        [Parameter(Position = 0, ValueFromPipelineByPropertyName = true)]
        public string Name { get; set; }
        
        [Parameter]
        public string Project { get; set; }

        [Parameter]
        public SwitchParameter IncludeHidden { get; set; }

        public GetScaffolderCmdlet() 
            : this(null, null, null)
        {
        }

        internal GetScaffolderCmdlet(ISolutionManager solutionManager, IVsPackageManagerFactory vsPackageManagerFactory, IScaffolderLocator scaffolderLocator)
            : base(solutionManager, vsPackageManagerFactory, null)
        {
            // Can't read the value of CommandInvoker until *after* the constructor finishes, hence lazy
            _scaffolderLocator = new Lazy<IScaffolderLocator>(
                () => scaffolderLocator ?? new Ps1ScaffolderLocator(CommandInvoker, PackageManager, null, new DefaultFileSystem(), new XmlScaffoldingConfigStore(SolutionManager))
            );
        }

        protected override void ProcessRecordCore()
        {
            var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
            if (project == null) {
                WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                return;
            }

            var scaffolders = from scaffolder in _scaffolderLocator.Value.GetScaffolders(project, Name, resolveDefaultNames: true)
                              where (scaffolder.ScaffolderAttribute == null)
                                    || (!scaffolder.ScaffolderAttribute.HideInConsole)
                                    || IncludeHidden.IsPresent
                              select scaffolder;
            WriteObject(scaffolders, enumerateCollection: true);
        }
    }
}
