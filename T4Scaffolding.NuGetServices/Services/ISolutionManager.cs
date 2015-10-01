using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using EnvDTE;

namespace T4Scaffolding.NuGetServices.Services
{
    public interface ISolutionManager
    {
        string SolutionDirectory { get; }

        string DefaultProjectName { get; }
        Project DefaultProject { get; }

        Project GetProject(string projectName);

        IEnumerable<Project> GetProjects();

        bool IsSolutionOpen { get; }
    }
}