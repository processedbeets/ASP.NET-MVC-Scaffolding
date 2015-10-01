using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace T4Scaffolding.Core.CommandInvokers
{
    internal class DefaultPowershellCommandInvoker : IPowershellCommandInvoker
    {
        private readonly CommandInvocationIntrinsics _invokeCommand;
        private readonly InvocationInfo _myInvocation;

        public DefaultPowershellCommandInvoker(CommandInvocationIntrinsics invokeCommand, InvocationInfo myInvocation)
        {
            _invokeCommand = invokeCommand;
            _myInvocation = myInvocation;
        }

        public IEnumerable<PSObject> InvokeCaptureOutput(CommandInfo commandInfo, Hashtable args)
        {
            if (commandInfo == null) throw new ArgumentNullException("commandInfo");
            return _invokeCommand.InvokeScript("param($c, $a) return . $c @a", true, PipelineResultTypes.None, null, commandInfo, args);
        }

        public void InvokePipeToOutput(CommandInfo commandInfo, Hashtable args, PipelineResultTypes pipelineResultTypes)
        {
            if (commandInfo == null) throw new ArgumentNullException("commandInfo");
            _invokeCommand.InvokeScript("param($c, $a) . $c @a", true, pipelineResultTypes, null, commandInfo, args);
        }

        public CommandInfo GetCommand(string command, CommandTypes commandTypes)
        {
            // Annoying special-case hack needed until NuGet can call init.ps1 in the same scope as the console.
            // This gets called implicitly while resolving dynamic params, and that call happens in a scope where
            // there is more than one copy of the T4Scaffolding module loaded. This leads to an ambiguous match error.
            // We can avoid this by finding that specific command using another Get-Command call.
            // Without this hack, you don't get tab-completion on scaffolder parameter names after a VS restart.
            if (string.Equals(command, "Invoke-Scaffolder", StringComparison.OrdinalIgnoreCase)) {
                var commandInfoPsObject = _invokeCommand.InvokeScript("Get-Command T4Scaffolding\\Invoke-Scaffolder");
                return (CommandInfo)commandInfoPsObject.First().BaseObject;
            }

            return _invokeCommand.GetCommand(command, commandTypes);
        }

        public IDictionary<string, object> BoundParameters
        {
            get { return _myInvocation.BoundParameters; }
        }
    }
}