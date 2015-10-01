using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using T4Scaffolding.NuGetServices.ExtensionMethods;

namespace T4Scaffolding.NuGetServices.Services
{
    public class ScaffoldingSolutionManager : ISolutionManager
    {
        private readonly DTE _dte;
        private readonly string _defaultProjectName;
        private Dictionary<string, Project> _projectCache;

        public ScaffoldingSolutionManager(string defaultProjectName)
        {
            _defaultProjectName = defaultProjectName;
            _dte= (DTE)Package.GetGlobalService(typeof(DTE)) ?? (DTE)Marshal.GetActiveObject("visualstudio.dte");
            if (_dte == null) throw new InvalidOperationException("XXX Cannot get an instance of EnvDTE.DTE");
        }

        public Project GetProject(string projectName)
        {
            if (IsSolutionOpen) {
                EnsureProjectCache();
                Project project;
                _projectCache.TryGetValue(projectName, out project);
                return project;
            } else {
                return null;
            }
        }

        public IEnumerable<Project> GetProjects()
        {
            if (IsSolutionOpen) {
                EnsureProjectCache();
                return _projectCache.Values;
            } else {
                return Enumerable.Empty<Project>();
            }
        }

        public string SolutionDirectory
        {
            get {
                if (!IsSolutionOpen || String.IsNullOrEmpty(_dte.Solution.FullName)) {
                    return null;
                }

                return Path.GetDirectoryName(_dte.Solution.FullName);
            }
        }

        public string DefaultProjectName
        {
            get { return _defaultProjectName; }
        }

        public Project DefaultProject
        {
            get {
                if (String.IsNullOrEmpty(DefaultProjectName)) {
                    return null;
                }
                Project project = GetProject(DefaultProjectName);
                Debug.Assert(project != null, "Invalid default project");
                return project;
            }
        }

        public bool IsSolutionOpen
        {
            get { return _dte.Solution != null && _dte.Solution.IsOpen; }
        }

        private void EnsureProjectCache()
        {
            if (IsSolutionOpen && _projectCache == null) {
                // Initialize the cache
                var allProjects = _dte.Solution.GetAllProjects();
                _projectCache = allProjects.ToDictionary(project => project.Name, StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}