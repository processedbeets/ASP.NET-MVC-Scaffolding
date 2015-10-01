using System.CodeDom.Compiler;
using System.Collections;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using EnvDTE;
using Microsoft.VisualStudio.TextTemplating;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using T4Scaffolding.Core;
using T4Scaffolding.Core.FileSystem;
using T4Scaffolding.Core.Templating;
using T4Scaffolding.NuGetServices;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.NuGetServices.ExtensionMethods;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet("Invoke", "ScaffoldTemplate")]
    public class InvokeScaffoldTemplateCmdlet : ScaffoldingBaseCmdlet
    {
        // Only used for testing since the VsServiceProvider doesn't provide a T4Engine when running tests in VS2012
        internal static ITextTemplatingEngine _t4Engine;

        private readonly IFileSystem _fileSystem;

        [Parameter(Mandatory = true)]
        public string Template { get; set; }

        [Parameter(Mandatory = true)]
        public Hashtable Model { get; set; }

        [Parameter]
        public string OutputPath { get; set; }

        [Parameter]
        public string Project { get; set; }

        [Parameter]
        public SwitchParameter Force { get; set; }

        public InvokeScaffoldTemplateCmdlet() : this(null, new DefaultFileSystem()) { }
        internal InvokeScaffoldTemplateCmdlet(ISolutionManager solutionManager, IFileSystem fileSystem) : base(solutionManager, null, null)
        {
            _fileSystem = fileSystem;
        }

        protected override void ProcessRecordCore()
        {
            if ((!string.IsNullOrEmpty(OutputPath)) && Path.IsPathRooted(OutputPath)) {
                WriteError(string.Format("Invalid OutputPath '{0}' - must be a relative path, e.g., Models\\Person.cs", OutputPath ?? ""));
                return;
            }

            var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
            if (project == null) {
                WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                return;
            }

            TemplateRenderingResult result = GetTemplateOutput(project, _fileSystem, Template, OutputPath, Model, Force);

            if (result.SkipBecauseFileExists) {
                WriteWarning(string.Format("{0} already exists! Pass -Force to overwrite. Skipping...", result.ProjectRelativeOutputPathWithExtension));
            } else if ((result.Errors != null) && result.Errors.HasErrors) {
                WriteObject(result.Errors.Cast<CompilerError>().OrderBy(x => x.Line));
                WriteError("Failed to render template");
            } else if (string.IsNullOrEmpty(result.Content)) {
                WriteWarning(string.Format("Template '{0}' produced no output. Skipping...", Template));
            } else {
                if (result.ExistingProjectItemToOverwrite != null) {
                    // Overwrite existing item and report its path
                    OverwriteProjectItemTextContent(result.ExistingProjectItemToOverwrite, result.Content);
                    WriteObject(result.ProjectRelativeOutputPathWithExtension);
                } else if (!string.IsNullOrEmpty(result.ProjectRelativeOutputPathWithExtension)) {
                    // Create new item and report its path
                    AddTextFileToProject(project, _fileSystem, result.ProjectRelativeOutputPathWithExtension, result.Content);
                    WriteObject(result.ProjectRelativeOutputPathWithExtension);
                } else {
                    // Just return the template's text output
                    WriteObject(result.Content);
                }                
            }
        }

        private void OverwriteProjectItemTextContent(ProjectItem item, string text)
        {
            var path = item.GetFullPath();
            SolutionManager.EnsureCheckedOutIfExists(path);
            _fileSystem.WriteAllText(path, text);
        }

        private static void AddTextFileToProject(Project project, IFileSystem fileSystem, string projectRelativePath, string text)
        {
            // Need to be sure the folder exists first
            var outputDirPath = Path.GetDirectoryName(projectRelativePath);
            var projectDir = project.GetOrCreateProjectItems(outputDirPath, true /* create */, fileSystem);
            var diskPath = Path.Combine(project.GetFullPath(), projectRelativePath);
            fileSystem.WriteAllText(diskPath, text);
            projectDir.AddFromFile(diskPath);
        }

        private static TemplateRenderingResult GetTemplateOutput(Project project, IFileSystem fileSystem, string template, string projectRelativeOutputPath, Hashtable model, bool force)
        {
            var templateFullPath = FindTemplateAssertExists(project, fileSystem, template);
            var templateContent = fileSystem.ReadAllText(templateFullPath);
            templateContent = PreprocessTemplateContent(templateContent);

            // Create the T4 host and engine
            using (var host = new DynamicTextTemplatingEngineHost { TemplateFile = templateFullPath }) {
                var t4Engine = GetT4Engine();

                // Make it possible to reference the same assemblies that your project references
                // using <@ Assembly @> directives. 
                host.AddFindableAssembly(FindProjectAssemblyIfExists(project));
                foreach (dynamic reference in ((dynamic)project.Object).References) {
                    if ((!string.IsNullOrEmpty(reference.Path)) && (!reference.AutoReferenced))
                        host.AddFindableAssembly(reference.Path);
                }

                string projectRelativeOutputPathWithExtension = null;
                ProjectItem existingOutputProjectItem = null;
                if (!string.IsNullOrEmpty(projectRelativeOutputPath)) {
                    // Respect the <#@ Output Extension="..." #> directive
                    projectRelativeOutputPathWithExtension = projectRelativeOutputPath + GetOutputExtension(host, t4Engine, templateContent);

                    // Resolve the output path and ensure it doesn't already exist (unless "Force" is set)                
                    var outputDiskPath = Path.Combine(project.GetFullPath(), projectRelativeOutputPathWithExtension);
                    existingOutputProjectItem = project.GetProjectItem(projectRelativeOutputPathWithExtension);
                    if (existingOutputProjectItem != null)
                        outputDiskPath = existingOutputProjectItem.GetFullPath();
                    if ((!force) && fileSystem.FileExists(outputDiskPath)) {
                        return new TemplateRenderingResult(projectRelativeOutputPathWithExtension) { SkipBecauseFileExists = true };
                    }
                }

                // Convert the incoming Hashtable to a dynamic object with properties for each of the Hashtable entries
                host.Model = DynamicViewModel.FromObject(model);

                // Run the text transformation      
                var templateOutput = t4Engine.ProcessTemplate(templateContent, host);
                return new TemplateRenderingResult(projectRelativeOutputPathWithExtension) {
                    Content = templateOutput, 
                    Errors = host.Errors, 
                    ExistingProjectItemToOverwrite = existingOutputProjectItem, 
                };
            }
        }

        public static ITextTemplatingEngine GetT4Engine()
        {
            var serviceProvider = VsServiceProvider.ServiceProvider;
            var t4Components = (ITextTemplatingComponents)serviceProvider.GetService(typeof(STextTemplating));
            if (t4Components != null) {
                return t4Components.Engine;
            } else {
                return _t4Engine;
            }
        }

        private static string PreprocessTemplateContent(string templateContent)
        {
            // To eliminate the direct reference to Microsoft.VisualStudio.TextTemplating.10.0.dll, we need to eliminate the
            // DynamicTransform base class which previously provided a "Model" property. As a replacement for this, automatically
            // append a T4 "class feature" to the template file that contains a "Model" property, and automatically remove any
            // inheritance reference to "DynamicTransform"
            templateContent = templateContent ?? string.Empty;

            // Eliminate Inherits="DynamicTransform" from template directive
            templateContent = Regex.Replace(templateContent, @"\<\#\@\s*\bTemplate\b(.*?\b)?Inherits=""DynamicTransform""\s*(.*?)\#\>", @"<#@ Template $1 $2 #>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Append "Model" property
            var language = DetermineLanguageForTemplate(templateContent);
            templateContent += ModelPropertyClassFeatures.ModelPropertySourceForLanguage[language];

            return templateContent;
        }

        private static string DetermineLanguageForTemplate(string templateContent)
        {
            // Only support VB and C#
            if (Regex.IsMatch(templateContent, @"\<\#\@\s*\bTemplate\b(.*?\b)?Language=""VB[^""]*""(.*?)\#\>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
                return "vb";
            return "cs";
        }

        private static string FindProjectAssemblyIfExists(Project project)
        {
            if ((project.ConfigurationManager != null) 
                && (project.ConfigurationManager.ActiveConfiguration != null)
                && (project.ConfigurationManager.ActiveConfiguration.Properties != null)
                && (project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath") != null)
                && (!string.IsNullOrEmpty((string)project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value))
                && (project.Properties.Item("OutputFileName") != null)
                && (!string.IsNullOrEmpty((string)project.Properties.Item("OutputFileName").Value))
                ) {
                var outputDir = Path.Combine(project.GetFullPath(), (string)project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value);
                var assemblyLocation = Path.Combine(outputDir, (string)project.Properties.Item("OutputFileName").Value);
                if (File.Exists(assemblyLocation))
                    return assemblyLocation;
            }
            return null;
        }

        private static string GetOutputExtension(DynamicTextTemplatingEngineHost host, ITextTemplatingEngine t4Engine, string templateContent)
        {
            string ignoredLanguage;
            string[] ignoreReferences;                
            t4Engine.PreprocessTemplate(templateContent, host, "DummyClass", "DummyNamespace", out ignoredLanguage, out ignoreReferences);
            return host.FileExtension ?? string.Empty;
        }

        private static string FindTemplateAssertExists(Project project, IFileSystem fileSystem, string template)
        {
            string templateFullPath = template;
            if (!Path.IsPathRooted(templateFullPath)) {
                var templateProjectItem = project.GetProjectItem(templateFullPath);
                if (templateProjectItem != null)
                    templateFullPath = templateProjectItem.GetFullPath();
            }
            if (!fileSystem.FileExists(templateFullPath)) {
                throw new FileNotFoundException(string.Format("Cannot find template at '{0}'", templateFullPath));
            }
            return templateFullPath;
        }

        private class TemplateRenderingResult
        {
            public TemplateRenderingResult(string projectRelativeOutputPathWithExtension)
            {
                ProjectRelativeOutputPathWithExtension = projectRelativeOutputPathWithExtension;
            }

            public string ProjectRelativeOutputPathWithExtension { get; private set; }
            public string Content { get; set; }
            public ProjectItem ExistingProjectItemToOverwrite { get; set; }
            public CompilerErrorCollection Errors { get; set; }
            public bool SkipBecauseFileExists { get; set; }
        }
    }
}
