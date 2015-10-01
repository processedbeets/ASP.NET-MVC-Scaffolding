namespace T4Scaffolding.NuGetServices.Services
{
    public interface IPackageManager
    {
        IPackagePathResolver PathResolver { get; }
        IPackageRepository LocalRepository { get; }
    }
}