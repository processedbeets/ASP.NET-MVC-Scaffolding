using System;

namespace T4Scaffolding.NuGetServices.Services
{
    public interface IPackage : IPackageMetadata
    {
    }

    public interface IPackageMetadata
    {
        string Id { get; }
        IComparable Version { get; }
    }
}