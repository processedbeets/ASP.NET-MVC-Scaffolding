using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace T4Scaffolding.NuGetServices
{
    public class VsServiceProvider
    {
        public static IServiceProvider ServiceProvider
        {
            get { return Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider; }
        }
    }
}
