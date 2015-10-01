using System;
using System.Management.Automation;

namespace T4Scaffolding.NuGetServices.Services
{
    public class PowerShellPackageManager : IPackageManager
    {
        private readonly CommandInvocationIntrinsics _commandInvocationIntrinsics;

        public PowerShellPackageManager(CommandInvocationIntrinsics commandInvocationIntrinsics)
        {
            if (commandInvocationIntrinsics == null) throw new ArgumentNullException("commandInvocationIntrinsics");
            _commandInvocationIntrinsics = commandInvocationIntrinsics;
        }

        public IPackagePathResolver PathResolver
        {
            get
            {
                return new ScaffoldingPackagePathResolver();
            }
        }

        public IPackageRepository LocalRepository
        {
            get { return new PowerShellPackageRepository(_commandInvocationIntrinsics); }
        }
    }
}