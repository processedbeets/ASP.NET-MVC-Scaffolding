using System.Linq;

namespace T4Scaffolding.NuGetServices.Services
{
    public interface IPackageRepository
    {
        IQueryable<IPackage> GetPackages();
    }
}