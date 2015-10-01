using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvDTE;
using T4Scaffolding.Core.FileSystem;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.NuGetServices.ExtensionMethods;

namespace T4Scaffolding.Core
{
    public static class VsProjectExtensions
    {
        public static ProjectItems GetOrCreateProjectItems(this Project project, string path, bool create, IFileSystem fileSystem)
        {
            if (create) {
                // Ensure the dir exists on disk
                var fullDiskPath = Path.Combine(project.GetFullPath(), path);
                if (!fileSystem.DirectoryExists(fullDiskPath))
                    fileSystem.CreateDirectory(fullDiskPath);
            }

            return project.GetProjectItems(path, createIfNotExists: create);
        }

        public static IEnumerable<ProjectItem> GetChildItemsIncludingSubfolders(this ProjectItem folder, IFileSystem fileSystem, string filter, params string[] kinds)
        {
            if (folder == null)
                return Enumerable.Empty<ProjectItem>();

            var physicalFolderPath = folder.GetFullPath();
            var matchingPhysicalFiles = fileSystem.FindFiles(physicalFolderPath, filter, true);

            return from physicalFile in matchingPhysicalFiles
                   let projectItem = FindProjectItemMatchingPhysicalFile(folder.ContainingProject, physicalFile)
                   where (projectItem != null) && (kinds.Contains(projectItem.Kind))
                   select projectItem;
        }

        private static ProjectItem FindProjectItemMatchingPhysicalFile(Project project, string physicalFilePath)
        {
            var projectPhysicalRoot = project.GetFullPath();
            if (string.IsNullOrEmpty(projectPhysicalRoot))
                return null;

            if (projectPhysicalRoot[projectPhysicalRoot.Length - 1] != Path.DirectorySeparatorChar)
                projectPhysicalRoot += Path.DirectorySeparatorChar;

            if (!physicalFilePath.StartsWith(projectPhysicalRoot, StringComparison.Ordinal)) // Shouldn't happen, but bail out here if something weird is going on
                return null;

            var relativeFilePath = physicalFilePath.Substring(projectPhysicalRoot.Length);
            return project.GetProjectItem(relativeFilePath);
        }

        public static string GetCodeLanguage(this Project project)
        {
            switch (project.Kind) {
                case VsConstants.CsharpProjectTypeGuid:
                    return "cs";
                case VsConstants.VbProjectTypeGuid:
                    return "vb";
                default:
                    return null;
            }
        }

        public static string GetFullPath(this ProjectItem projectItem)
        {
            return projectItem.GetPropertyValue<string>("FullPath");
        }

        public static T GetPropertyValue<T>(this ProjectItem projectItem, string propertyName)
        {
            try
            {
                Property property = projectItem.Properties.Item(propertyName);
                if (property != null)
                {
                    // REVIEW: Should this cast or convert?
                    return (T)property.Value;
                }
            }
            catch (ArgumentException)
            {

            }
            return default(T);
        }

        public static void EnsureCheckedOutIfExists(this ISolutionManager solutionManager, string fullPath)
        {
            if (File.Exists(fullPath)) {
                var project = solutionManager.GetProjects().FirstOrDefault();
                if ((project != null) && (project.DTE != null) && (project.DTE.SourceControl != null)) {
                    var sourceControl = project.DTE.SourceControl;
                    if (sourceControl.IsItemUnderSCC(fullPath) && !sourceControl.IsItemCheckedOut(fullPath)) {
                        sourceControl.CheckOutItem(fullPath);
                    }
                }
            }
        }
    }
}
