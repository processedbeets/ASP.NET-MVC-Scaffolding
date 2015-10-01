using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using T4Scaffolding.Core;
using T4Scaffolding.Core.FileSystem;
using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Find, "ScaffolderTemplate")]
    public class FindScaffolderTemplateCmdlet : ScaffoldingBaseCmdlet
    {
        private readonly IFileSystem _fileSystem;

        public FindScaffolderTemplateCmdlet()
            : this(null, new DefaultFileSystem()) { }

        internal FindScaffolderTemplateCmdlet(ISolutionManager solutionManager, IFileSystem fileSystem)
            : base(solutionManager, null, null)
        {
            if (fileSystem == null) throw new ArgumentNullException("fileSystem");
            _fileSystem = fileSystem;
        }

        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        public string Template { get; set; }

        [Parameter(Mandatory = true, Position = 0)]
        public object[] TemplateFolders { get; set; } // Using object[] because it's hard to pass any other type of collection from PowerShell

        [Parameter]
        public string Project { get; set; }

        [Parameter]
        public string CodeLanguage { get; set; }

        [Parameter]
        public SwitchParameter ErrorIfNotFound { get; set; }

        protected override void ProcessRecordCore()
        {
            if ((TemplateFolders == null) || (!TemplateFolders.Any())) throw new InvalidOperationException("Provide at least one TemplateSource");
            if (string.IsNullOrEmpty(Template)) throw new InvalidOperationException("'Template' parameter cannot be null or empty");

            // Determine the code language
            if (string.IsNullOrEmpty(CodeLanguage)) {
                var project = SolutionManager.GetProject(string.IsNullOrEmpty(Project) ? SolutionManager.DefaultProjectName : Project);
                if (project == null) {
                    WriteError(string.Format("Could not find project '{0}'", Project ?? string.Empty));
                    return;
                }
                CodeLanguage = project.GetCodeLanguage();
                if (string.IsNullOrEmpty(CodeLanguage))
                    throw new InvalidOperationException(string.Format("Cannot determine code language for default project '{0}'. Try specifying the code language via the -CodeLanguage parameter.", project.Name));
            }

            var templateFilename = string.Format("{0}.{1}.t4", Template, CodeLanguage);
            List<string> foldersSearched = new List<string>();

            string matchingTemplate = (from folderPath in TemplateFolders
                                      select GetMatchingTemplateFromSpecificFolder(folderPath.ToString(), templateFilename, foldersSearched))
                                      .FirstOrDefault(x => !string.IsNullOrEmpty(x));

            if (!string.IsNullOrEmpty(matchingTemplate)) {
                WriteVerbose(string.Format("Found template '{0}' at '{1}'", Template, matchingTemplate));
                WriteObject(matchingTemplate);
            } else {
                var message = string.Format("Could not find template '{0}' below folders ['{1}']", templateFilename, string.Join(", ", foldersSearched));
                if (ErrorIfNotFound.IsPresent) WriteError(message); else WriteVerbose(message);
            }
        }

        private string GetMatchingTemplateFromSpecificFolder(string templatesFolder, string templateFilename, List<string> foldersSearched)
        {
            var matchingTemplates = _fileSystem.FindFiles(templatesFolder, templateFilename, true).ToList();
            foldersSearched.Add(templatesFolder);

            // Reject ambiguous matches
            if (matchingTemplates.Count > 1)
                throw new InvalidOperationException(string.Format("Multiple templates associated in folder '{0}' match the filename '{1}'", templatesFolder, templateFilename));

            return matchingTemplates.SingleOrDefault();
        }
    }
}
