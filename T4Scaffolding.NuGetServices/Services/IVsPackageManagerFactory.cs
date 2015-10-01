namespace T4Scaffolding.NuGetServices.Services
{
    public interface IVsPackageManagerFactory
    {
        IPackageManager CreatePackageManager();
    }
}