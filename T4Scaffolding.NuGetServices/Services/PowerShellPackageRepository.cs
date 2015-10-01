using System;
using System.Linq;
using System.Management.Automation;

namespace T4Scaffolding.NuGetServices.Services
{
    public class PowerShellPackageRepository : IPackageRepository
    {
        private readonly CommandInvocationIntrinsics _commandInvocationIntrinsics;

        public PowerShellPackageRepository(CommandInvocationIntrinsics commandInvocationIntrinsics)
        {
            if (commandInvocationIntrinsics == null) throw new ArgumentNullException("commandInvocationIntrinsics");
            _commandInvocationIntrinsics = commandInvocationIntrinsics;
        }

        public IQueryable<IPackage> GetPackages()
        {
            var getPackageResults = _commandInvocationIntrinsics.InvokeScript("Get-Package");
            return (from package in getPackageResults
                    select new PowerShellPackage {
                        Id = (string)package.Properties.First(x => x.Name == "Id").Value,
                        Version = (IComparable)package.Properties.First(x => x.Name == "Version").Value
                    }).AsQueryable();
        }

        private class PowerShellPackage : IPackage
        {
            public string Id { get; set; }
            public IComparable Version { get; set; }
        }
    }
}