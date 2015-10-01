using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.NuGetServices.ExtensionMethods
{
    internal static class PackageExtensions
    {
        public static string GetFullName(this IPackageMetadata package)
        {
            return package.Id + " " + package.Version;
        }
    }
}