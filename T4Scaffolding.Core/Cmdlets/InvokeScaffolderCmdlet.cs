using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Runtime.Remoting.Messaging;
using EnvDTE;
using T4Scaffolding.Core;
using T4Scaffolding.Core.CommandInvokers;
using T4Scaffolding.Core.Configuration;
using T4Scaffolding.Core.FileSystem;
using T4Scaffolding.Core.ScaffolderLocators;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.NuGetServices.ExtensionMethods;
using T4Scaffolding.NuGetServices.Threading;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet("Invoke", "Scaffolder")]
    public class InvokeScaffolderCmdlet : ScaffoldingBaseCmdlet, IDynamicParameters
    {
        private readonly Lazy<IScaffolderLocator> _scaffolderLocator;
        private static readonly IEnumerable<string> StaticParameterNames = (from prop in typeof (InvokeScaffolderCmdlet).GetProperties()
                                                                           where prop.GetCustomAttributes(typeof (ParameterAttribute), true).Any()
                                                                           select prop.Name).ToList();
        private readonly Lazy<IList<string>> _staticAndCommonParameterNames;

        public InvokeScaffolderCmdlet() 
            : this(null, null, null, null)
        {
        }

        internal InvokeScaffolderCmdlet(ISolutionManager solutionManager, IVsPackageManagerFactory vsPackageManagerFactory, IScaffolderLocator scaffolderLocator, IPowershellCommandInvoker commandInvoker)
            : base(solutionManager, vsPackageManagerFactory, commandInvoker)
        {
            // Can't read the value of CommandInvoker until *after* the constructor finishes, hence lazy
            _scaffolderLocator = new Lazy<IScaffolderLocator>(
                () => scaffolderLocator ?? new Ps1ScaffolderLocator(CommandInvoker, PackageManager, null, new DefaultFileSystem(), new XmlScaffoldingConfigStore(SolutionManager))
            );
            _staticAndCommonParameterNames = new Lazy<IList<string>>(
                () => CommandInvoker.GetCommand("Invoke-Scaffolder", CommandTypes.Cmdlet).Parameters.Select(x => x.Key).ToList()
            );
        }

        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        public string Scaffolder { get; set; }

        [Parameter]
        public string Project { get; set; }

        [Parameter]
        public object[] OverrideTemplateFolders { get; set; }  // Using object[] because it's hard to pass any other type of collection from PowerShell

        [Parameter]
        public SwitchParameter BlockUi { get; set; }
        
        [Parameter]
        public SwitchParameter PipeOutput { get; set; }

        protected override void ProcessRecordCore()
        {
            if (string.IsNullOrEmpty(Scaffolder))
                throw new InvalidOperationException(string.Format("Please specify a value for the 'Scaffolder' parameter."));


            var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
            if (project == null) {
                WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                return;
            }

            var scaffolders = _scaffolderLocator.Value.GetScaffolders(project, Scaffolder, true) ?? new List<ScaffolderInfo>();

            switch (scaffolders.Count()) {
                case 0:
                    WriteError(string.Format("Cannot find scaffolder '{0}'", Scaffolder));
                    break;
                case 1:
                    if (BlockUi.IsPresent) 
                        OperationDispatcher.RunOnUiThread(() => InvokeScaffolderCore(project, scaffolders.Single()));
                    else 
                        InvokeScaffolderCore(project, scaffolders.Single());
                    break;
                default:
                    WriteError(string.Format("More than one scaffolder matches the name '{0}'", Scaffolder));
                    break;
            }

        }

        private void InvokeScaffolderCore(Project project, ScaffolderInfo scaffolder)
        {
            var scaffolderParams = new Hashtable(CommandInvoker.BoundParameters
                .Where(x => !StaticParameterNames.Contains(x.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(x => x.Key, x => x.Value));
            AddTemplateFoldersParameterIfPossible(scaffolder, scaffolderParams, project);
            AddParameterIfAcceptedByScaffolder(scaffolder.Command, scaffolderParams, "Project", project.Name);

            using (var stack = new CallStackDepthCounter()) {
                // Don't pipe output when the scaffolder is invoked directly from the console. 
                // Only pipe it for inner invocations (e.g., when a scaffolder is invoked by another script)
                // This behaviour can be overridden using the PipeOutput switch
                var pipelineType = (stack.Depth > 0) || PipeOutput.IsPresent ? PipelineResultTypes.Output | PipelineResultTypes.Error : PipelineResultTypes.Error;
                CommandInvoker.InvokePipeToOutput(scaffolder.Command, scaffolderParams, pipelineType);
            }
        }

        private void AddTemplateFoldersParameterIfPossible(ScaffolderInfo scaffolder, Hashtable paramsTable, Project project)
        {
            // Specified overrides are the top priority folders to search
            List<string> templateFolders = new List<string>();
            if (OverrideTemplateFolders != null)
                templateFolders.AddRange(OverrideTemplateFolders.Select(x => x.ToString()));

            // If not found in these, the scaffolder should look in its own custom templates folder plus the same folder under "tools" that it actually lives in
            templateFolders.Add(GetCustomTemplateFolder(scaffolder.Name, project));
            templateFolders.Add(Path.GetDirectoryName(scaffolder.Location));

            AddParameterIfAcceptedByScaffolder(scaffolder.Command, paramsTable, "TemplateFolders", templateFolders.Where(x => !string.IsNullOrEmpty(x)).ToArray());
        }

        private static string GetCustomTemplateFolder(string scaffolderName, Project project)
        {
            var customTemplateProjectPath = Path.Combine(ScaffoldingConstants.CustomScaffoldersFolderPath, scaffolderName);
            var projectItem = project.GetProjectItem(customTemplateProjectPath);
            return projectItem != null ? projectItem.GetFullPath() : null;
        }

        private static void AddParameterIfAcceptedByScaffolder<TParameter>(CommandInfo scaffolderCommandInfo, Hashtable paramsTable, string parameterName, TParameter parameterValue)
        {
            if (scaffolderCommandInfo == null) throw new ArgumentNullException("scaffolderCommandInfo");
            if (paramsTable == null) throw new ArgumentNullException("paramsTable");

            ParameterMetadata parameterMetadata;
            if (scaffolderCommandInfo.Parameters.TryGetValue(parameterName, out parameterMetadata)) {
                if (parameterMetadata.ParameterType.IsAssignableFrom(typeof(TParameter))) {
                    paramsTable[parameterName] = parameterValue;
                }
            } 
        }

        public object GetDynamicParameters()
        {
            var dynamicParams = new RuntimeDefinedParameterDictionary();
            try {
                if (!string.IsNullOrEmpty(Scaffolder)) {
                    var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
                    var scaffolders = _scaffolderLocator.Value.GetScaffolders(project, Scaffolder, true);
                    if ((scaffolders != null) && (scaffolders.Count() == 1)) {
                        foreach (var parameter in scaffolders.Single().Command.Parameters) {
                            if (!_staticAndCommonParameterNames.Value.Contains(parameter.Value.Name, StringComparer.OrdinalIgnoreCase))
                                dynamicParams.Add(parameter.Value.Name, new RuntimeDefinedParameter(parameter.Value.Name, parameter.Value.ParameterType, parameter.Value.Attributes));
                        }
                    }
                }
            } catch { /* If there's any problem parsing the script, we just don't show dynamic parameters */ }
            return dynamicParams;
        }
    }
}