using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace T4Scaffolding.Core.CommandInvokers
{
    public interface IPowershellCommandInvoker
    {
        CommandInfo GetCommand(string command, CommandTypes commandTypes);
        IEnumerable<PSObject> InvokeCaptureOutput(CommandInfo commandInfo, Hashtable args);
        void InvokePipeToOutput(CommandInfo commandInfo, Hashtable args, PipelineResultTypes pipelineResultTypes);
        IDictionary<string, object> BoundParameters { get; }
    }
}