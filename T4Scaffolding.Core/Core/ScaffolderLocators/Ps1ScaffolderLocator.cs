using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using EnvDTE;
using T4Scaffolding.Core.CommandInvokers;
using T4Scaffolding.Core.Configuration;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.NuGetServices.ExtensionMethods;

namespace T4Scaffolding.Core.ScaffolderLocators
{
    /// <summary>
    /// Finds scaffolders represented by *.ps1 files in any package's "tools" folder
    /// </summary>
    internal class Ps1ScaffolderLocator : IScaffolderLocator
    {
        private readonly IPowershellCommandInvoker _commandInvoker;
        private readonly IPackageManager _packageManager;
        private readonly IPackagePathResolver _pathResolver;
        private readonly FileSystem.IFileSystem _fileSystem;
        private readonly IScaffoldingConfigStore _configStore;

        public Ps1ScaffolderLocator(IPowershellCommandInvoker commandInvoker, IPackageManager packageManager, IPackagePathResolver pathResolver, FileSystem.IFileSystem fileSystem, IScaffoldingConfigStore configStore)
        {
            _commandInvoker = commandInvoker;
            _packageManager = packageManager;
            _pathResolver = pathResolver ?? packageManager.PathResolver;
            _fileSystem = fileSystem;
            _configStore = configStore;
        }

        public IEnumerable<ScaffolderInfo> GetScaffolders(Project project, string name, bool resolveDefaultNames)
        {
            var customScaffoldersFolder = project != null ? project.GetProjectItem(ScaffoldingConstants.CustomScaffoldersFolderPath) : null;
            var customScaffoldersFolders = customScaffoldersFolder != null ? new[] { customScaffoldersFolder } : Enumerable.Empty<ProjectItem>();

            var allPackages = _packageManager.LocalRepository.GetPackages();
            var packagesWithToolsPath = from p in allPackages
                                        let installPath = _pathResolver.GetInstallPath(p)
                                        where !string.IsNullOrEmpty(installPath)
                                        select new {
                                            Package = p,
                                            ToolsPath = Path.Combine(installPath, "tools")
                                        };

            // Only consider the single latest version of any given package
            var latestPackagesWithToolsPath = from packageGroup in packagesWithToolsPath.GroupBy(x => x.Package.Id)
                                              select packageGroup.OrderByDescending(x => x.Package.Version).First();

            // Note that we prefer to match actual scaffolder names (rather than resolving default names first)
            // so that you can't "override" a scaffolder by creating a default with the same name. Allowing that
            // could lead to unexpected behavior, especially if such a default was created by mistake.
            var actualMatches = latestPackagesWithToolsPath.SelectMany(x => FindScaffolders(x.Package, x.ToolsPath, name))
                                                           .Concat(customScaffoldersFolders.SelectMany(x => FindScaffolders(x, name)));
            if (actualMatches.Any())
                return actualMatches;

            // Since no scaffolders actually match "name", try resolving that as a default name and go again
            if (resolveDefaultNames) {
                // First look for a project-specific setting, and if not found, fall back on solution-wide settings
                IQueryable<DefaultScaffolderConfigEntry> resolvedNames = null;
                if (project != null)
                    resolvedNames = _configStore.GetProjectDefaultScaffolders(project).Where(x => x.DefaultName.Equals(name, StringComparison.OrdinalIgnoreCase));
                if ((resolvedNames == null) || (!resolvedNames.Any()))
                    resolvedNames = _configStore.GetSolutionDefaultScaffolders().Where(x => x.DefaultName.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (resolvedNames.Count() == 1) {
                    var resolvedName = resolvedNames.Single().ScaffolderName;
                    return latestPackagesWithToolsPath.SelectMany(x => FindScaffolders(x.Package, x.ToolsPath, resolvedName))
                                                      .Concat(customScaffoldersFolders.SelectMany(x => FindScaffolders(x, resolvedName)));
                }
            }

            return Enumerable.Empty<ScaffolderInfo>();
        }

        private IEnumerable<ScaffolderInfo> FindScaffolders(ProjectItem customScaffoldersFolder, string scaffolderName)
        {
            var filter = GetFilterForScaffolderName(scaffolderName);
            return from projectItem in customScaffoldersFolder.GetChildItemsIncludingSubfolders(_fileSystem, filter, VsConstants.VsProjectItemKindPhysicalFile)
                   let scaffolderInfo = GetScaffolderInfo(null, projectItem.GetFullPath())
                   where scaffolderInfo != null
                   select scaffolderInfo;
        }

        private IEnumerable<ScaffolderInfo> FindScaffolders(IPackage package, string toolsPath, string name)
        {
            if (!_fileSystem.DirectoryExists(toolsPath))
                return Enumerable.Empty<ScaffolderInfo>();

            var filter = GetFilterForScaffolderName(name);
            return from filename in _fileSystem.FindFiles(toolsPath, filter, true)
                   let scaffolderInfo = GetScaffolderInfo(package, filename)
                   where scaffolderInfo != null
                   select scaffolderInfo;
        }

        private ScaffolderInfo GetScaffolderInfo(IPackage package, string ps1Filename)
        {
            var commandInfo = _commandInvoker.GetCommand(ps1Filename, CommandTypes.ExternalScript) as ExternalScriptInfo;
            if ((commandInfo == null) || (commandInfo.ScriptBlock == null))
                return null;
            var scaffolderAttribute = commandInfo.ScriptBlock.Attributes.OfType<ScaffolderAttribute>().FirstOrDefault();
            if (scaffolderAttribute == null)
                return null;

            var packageName = package != null ? package.GetFullName() : null;
            return new ScaffolderInfo(Path.GetFileNameWithoutExtension(ps1Filename), packageName, ps1Filename, commandInfo, scaffolderAttribute);
        }

        private static string GetFilterForScaffolderName(string scaffolderName)
        {
            if (string.IsNullOrEmpty(scaffolderName))
                scaffolderName = "*";
            return scaffolderName + ".ps1";
        }
    }
}