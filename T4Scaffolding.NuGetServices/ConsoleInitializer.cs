using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using NuGetConsole;

namespace T4Scaffolding.NuGetServices
{
    internal static class ConsoleInitializer
    {
        public static void EnsureRunspaceOnCurrentThread()
        {
            IConsoleInitializer consoleInitializer = GetConsoleInitializer();
            if (consoleInitializer != null)
                consoleInitializer.Initialize().Result();
        }

        private static IConsoleInitializer GetConsoleInitializer()
        {
            try {
                var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
                return componentModel.GetService<IConsoleInitializer>();
            }
            catch {
                // e.g., if you're running a version of NuGet that doesn't expose an IConsoleInitializer
                return null;
            }
        }
    }
}