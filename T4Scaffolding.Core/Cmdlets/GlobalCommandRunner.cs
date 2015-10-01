using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;

namespace T4Scaffolding.Cmdlets
{
    /// <summary>
    /// This is a temporary hack, erm, *workaround* until NuGet lets packages export PowerShell module members to the  
    /// Package Manager Console. Once that is resolved, this class will be removed. Don't call it from other packages!
    /// Marking as obsolete as a further warning.
    /// </summary>
    [Obsolete]
    public static class GlobalCommandRunner
    {
        private static readonly object _inCallbackLock = new object();
        public static void Run(string script, bool allowMultipleRuns)
        {
            var runspace = Runspace.DefaultRunspace;
            // When the user is interacting with the console (e.g., first installing the package), the runspace
            // is busy, so we need to wait until it becomes free
            EventHandler<RunspaceAvailabilityEventArgs> callback = null;
            callback = (sender, e) =>
            {
                lock (_inCallbackLock)
                {
                    if ((runspace != null) && (runspace.RunspaceAvailability == RunspaceAvailability.Available))
                    {
                        runspace.AvailabilityChanged -= callback;
                        RunScriptNow(runspace, script);
                        runspace = null; // Basic way of coordinating activity among the two types of callback
                    }
                }
            };
            runspace.AvailabilityChanged += callback;

            // When the user first opens Visual Studio, the runspace claims to be available but in fact is not,
            // so we wait a little (arbitrary) while and then try to run the script. In case this fails (perhaps we
            // didn't wait long enough), if allowMultipleRuns=true, we'll try again later when the runspace informs us it's free.
            ThreadPool.QueueUserWorkItem(e =>
            {
                System.Threading.Thread.Sleep(1000);
                lock (_inCallbackLock)
                {
                    if ((runspace != null) && (runspace.RunspaceAvailability == RunspaceAvailability.Available))
                    {
                        RunScriptNow(runspace, script);
                        if (!allowMultipleRuns)
                        {
                            runspace = null;
                        }
                    }
                }
            });
        }

        private static void RunScriptNow(Runspace runspace, string script)
        {
            using (var ps = PowerShell.Create())
            {
                ps.Runspace = runspace;
                ps.Commands = new PSCommand();
                ps.Commands.AddScript(script, false);
                ps.Invoke();
            }
        }
    }
}