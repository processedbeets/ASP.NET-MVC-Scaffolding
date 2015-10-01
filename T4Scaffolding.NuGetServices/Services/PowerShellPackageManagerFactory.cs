using System;
using System.Management.Automation;

namespace T4Scaffolding.NuGetServices.Services
{
    public class PowerShellPackageManagerFactory : IVsPackageManagerFactory
    {
        private readonly CommandInvocationIntrinsics _commandInvocationIntrinsics;

        public PowerShellPackageManagerFactory(CommandInvocationIntrinsics commandInvocationIntrinsics)
        {
            if (commandInvocationIntrinsics == null) throw new ArgumentNullException("commandInvocationIntrinsics");
            _commandInvocationIntrinsics = commandInvocationIntrinsics;
        }
        
        public IPackageManager CreatePackageManager()
        {
            return new PowerShellPackageManager(_commandInvocationIntrinsics);
        }
    }
}