using System.Collections.Generic;
using EnvDTE;
using T4Scaffolding.Core.ProjectTypeLocators;

namespace T4Scaffolding.Core.RelatedEntityLocators
{
    public interface IRelatedEntityLocator
    {
        IEnumerable<RelatedEntityInfo> GetRelatedEntities(CodeType codeType, Project project, IProjectTypeLocator projectTypeLocator);
    }
}
