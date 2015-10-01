using System.Linq;
using EnvDTE;

namespace T4Scaffolding.Core.Configuration
{
    public interface IScaffoldingConfigStore
    {
        IQueryable<DefaultScaffolderConfigEntry> GetProjectDefaultScaffolders(Project project);
        IQueryable<DefaultScaffolderConfigEntry> GetSolutionDefaultScaffolders();

        void SetProjectDefaultScaffolder(Project project, string defaultName, string scaffolderName, bool doNotOverwriteExistingSetting);
        void SetSolutionDefaultScaffolder(string defaultName, string scaffolderName, bool doNotOverwriteExistingSetting);
    }
}