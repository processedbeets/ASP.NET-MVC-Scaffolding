using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using T4Scaffolding.Core.CommandInvokers;
using T4Scaffolding.NuGetServices;
using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.Cmdlets
{
    public abstract class ScaffoldingBaseCmdlet : ScaffoldingNuGetBaseCmdlet
    {
        private readonly Lazy<IPowershellCommandInvoker> _commandInvoker;        

        protected IPowershellCommandInvoker CommandInvoker { get { return _commandInvoker.Value; } }        

        protected ScaffoldingBaseCmdlet()
            : this(null, null, null)
        {
        }

        internal ScaffoldingBaseCmdlet(ISolutionManager solutionManager, IVsPackageManagerFactory vsPackageManagerFactory, IPowershellCommandInvoker commandInvoker)
            : base(solutionManager, vsPackageManagerFactory)
        {
            // Command intrinsics can't be accessed until the PSCmdlet enters the "ProcessRecord" phase,
            // so we have to defer evaluation of the following things until then. To support unit testing,
            // it's possible to override their instantiation by passing a non-null instance to the constructor.

            _commandInvoker = new Lazy<IPowershellCommandInvoker>(() => {
                return commandInvoker ?? new DefaultPowershellCommandInvoker(InvokeCommand, MyInvocation);
            });
        }

        internal void Execute()
        {
            BeginProcessing();
            ProcessRecord();
            EndProcessing();
        }

        protected IEnumerable<TResult> InvokeCmdletCaptureOutput<TResult>(string cmdletName, Hashtable args)
        {
            var cmdlet = CommandInvoker.GetCommand(cmdletName, CommandTypes.Cmdlet);
            return CommandInvoker.InvokeCaptureOutput(cmdlet, args).Select(x => x.BaseObject).OfType<TResult>();
        }
    }
}