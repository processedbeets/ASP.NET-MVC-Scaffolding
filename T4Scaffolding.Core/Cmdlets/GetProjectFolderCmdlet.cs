using System.Management.Automation;
using T4Scaffolding.Core;
using T4Scaffolding.Core.FileSystem;
using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "ProjectFolder")]
    public class GetProjectFolderCmdlet : ScaffoldingBaseCmdlet
    {
        private readonly IFileSystem _fileSystem;

        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        public string Path { get; set; }

        [Parameter]
        public string Project { get; set; }

        [Parameter]
        public SwitchParameter Create { get; set; }

        
        public GetProjectFolderCmdlet() 
            : this(null, new DefaultFileSystem()) { }

        internal GetProjectFolderCmdlet(ISolutionManager solutionManager, IFileSystem fileSystem) 
            : base(solutionManager, null, null)
        {
            _fileSystem = fileSystem;
        }

        protected override void ProcessRecordCore()
        {
            var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
            if (project == null) {
                WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                return;
            }
            var result = project.GetOrCreateProjectItems(Path, Create.ToBool(), _fileSystem);
            if (result != null)
                WriteObject(result);
        }
    }
}
