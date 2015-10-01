using System;
using System.IO;
using System.Linq;

namespace T4Scaffolding.NuGetServices.Services
{
    public class ScaffoldingPackagePathResolver : IPackagePathResolver
    {
        // Until there's a defined API for enumerating package install directories, we have to 
        // pass in the root directory from our init.ps1 script. The alternative is replicating
        // masses of NuGet's config parsing logic, TFS filesystem, etc.

        private static string _packagesRoot;

        public string GetInstallPath(IPackage package)
        {
            if (string.IsNullOrEmpty(_packagesRoot))
                throw new InvalidOperationException("Cannot get install path because packages root directory has not been supplied");
            if (!Directory.Exists(_packagesRoot))
                throw new ArgumentException(string.Format("Cannot get install path: Cannot find directory '{0}'", _packagesRoot));

            return Path.Combine(_packagesRoot, GetPackageDirectory(package));
        }

        public string GetPackageDirectory(IPackage package)
        {
            return package.Id + "." + package.Version;
        }

        public static void SetPackagesRootDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("SetPackagesRootDirectory: path cannot be null or empty");
            _packagesRoot = path;
        }

        public static string InferSolutionDirectory() {
            // Normally we can find the solution directory using $dte.Solution.FullName, but if T4Scaffolding is being used as a preinstalled
            // NuGet package with a project template, it has to initialize *before* $dte.Solution exists. This is unfortunate. Work around
            // by inferring the solution folder from the solution package directory, which is known during initialization.

            return string.IsNullOrEmpty(_packagesRoot) ? null 
                                                       : Path.GetDirectoryName(_packagesRoot);
        }
    }
}