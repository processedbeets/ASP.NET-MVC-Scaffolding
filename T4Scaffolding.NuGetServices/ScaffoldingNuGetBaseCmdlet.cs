using System;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.NuGetServices
{
    /// <summary>
    /// Duplicates the functionality of NuGet.VisualStudio.Cmdlets.NuGetBaseCmdlet so we can follow the same coding patterns without
    /// taking an actual binary dependency on a specific version of NuGet.VisualStudio.dll.
    /// --
    /// Since we need to issue a PowerShell command to determine the current NuGet "default project", we have to defer initialization
    /// of SolutionManager and other services until the cmdlet has entered the ProcessRecord phase.
    /// </summary>
    public abstract class ScaffoldingNuGetBaseCmdlet : PSCmdlet
    {
        private readonly Lazy<ISolutionManager> _solutionManager;
        private readonly Lazy<IVsPackageManagerFactory> _packageManagerFactory;
        private readonly Lazy<IPackageManager> _packageManager;

        protected ISolutionManager SolutionManager { get { return _solutionManager.Value; } }
        protected IPackageManager PackageManager { get { return _packageManager.Value; } }

        protected ScaffoldingNuGetBaseCmdlet(ISolutionManager solutionManager, IVsPackageManagerFactory vsPackageManagerFactory) 
        {
            // Command intrinsics (and hence DefaultProjectName) can't be accessed until the PSCmdlet enters the "ProcessRecord" phase,
            // so we have to defer evaluation of the following things until then. To support unit testing, it's possible to override 
            // their instantiation by passing a non-null instance to the constructor.

            _packageManagerFactory = new Lazy<IVsPackageManagerFactory>(() => {
                return vsPackageManagerFactory ?? new PowerShellPackageManagerFactory(InvokeCommand);
            });
            _packageManager = new Lazy<IPackageManager>(() => {
                return _packageManagerFactory.Value.CreatePackageManager();
            });
            _solutionManager = new Lazy<ISolutionManager>(() => {
                if (solutionManager != null)
                    return solutionManager;

                var getProjectResults = InvokeCommand.InvokeScript("(Get-Project).Name").ToList();
                var defaultProjectName = getProjectResults.Count == 1 ? (string)getProjectResults.Single().BaseObject : null;
                return new ScaffoldingSolutionManager(defaultProjectName);
            });
        }

        protected sealed override void ProcessRecord()
        {
            try {
                ProcessRecordCore();
            } catch (Exception ex) {
                WriteError(ex);
            }
        }

        protected abstract void ProcessRecordCore();

        protected void WriteError(string message)
        {
            if (!String.IsNullOrEmpty(message)) {
                WriteError(new Exception(message));
            }
        }

        protected void WriteError(Exception exception)
        {
            // Only unwrap target invocation exceptions
            if (exception is TargetInvocationException) {
                exception = exception.InnerException;
            }
            WriteError(new ErrorRecord(exception, String.Empty, ErrorCategory.NotSpecified, null));
        }
    }
}