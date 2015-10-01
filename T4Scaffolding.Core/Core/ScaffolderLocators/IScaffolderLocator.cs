using System.Collections.Generic;
using EnvDTE;

namespace T4Scaffolding.Core.ScaffolderLocators
{
    public interface IScaffolderLocator
    {
        IEnumerable<ScaffolderInfo> GetScaffolders(Project project, string name, bool resolveDefaultNames);
    }
}