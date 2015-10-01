namespace T4Scaffolding.NuGetServices.Services
{
    public interface IPackagePathResolver
    {
        string GetInstallPath(IPackage package);
    }
}