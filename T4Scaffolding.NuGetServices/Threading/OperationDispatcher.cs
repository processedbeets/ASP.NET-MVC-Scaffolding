using System;
using Microsoft.VisualStudio.Shell;

namespace T4Scaffolding.NuGetServices.Threading
{
    /// <summary>
    /// DTE operations are very slow when run outside the VS UI thread
    /// To speed things up, this class lets us switch back to the UI thread temporarily
    /// </summary>
    public static class OperationDispatcher
    {
        public static void RunOnUiThread(Action action)
        {
            RunOnUiThread((Func<Object>)(() => {
                action();
                return null;
            }));
        }

        public static T RunOnUiThread<T>(Func<T> func)
        {
            // We must manually transfer the CallStackDepthCounter value across the thread boundary, because
            // CallContext logical data isn't preserved when using Microsoft.VisualStudio.Shell.ThreadHelper
            var callStackData = CallStackDepthCounter.Data;
            return ThreadHelper.Generic.Invoke(() => {
                CallStackDepthCounter.Data = callStackData;
                ConsoleInitializer.EnsureRunspaceOnCurrentThread(); // In NuGet 1.2 and later, the UI thread doesn't necessarily have a PowerShell Runspace until we call this
                return func();
            });
        }
    }
}