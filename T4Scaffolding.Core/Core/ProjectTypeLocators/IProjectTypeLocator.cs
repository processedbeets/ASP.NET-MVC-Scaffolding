using System.Collections.Generic;
using EnvDTE;

namespace T4Scaffolding.Core.ProjectTypeLocators
{
    public interface IProjectTypeLocator
    {
        IEnumerable<CodeType> FindTypes(Project project, string typeName);
        CodeType FindUniqueType(Project project, string typeName);
        IEnumerable<CodeType> FindAllTypes(Project project);
    }
}