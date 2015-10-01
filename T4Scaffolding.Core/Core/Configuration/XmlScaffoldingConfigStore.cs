using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using EnvDTE;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.NuGetServices.ExtensionMethods;

namespace T4Scaffolding.Core.Configuration
{
    class XmlScaffoldingConfigStore : IScaffoldingConfigStore
    {
        private readonly ISolutionManager _solutionManager;
        private const string ConfigXmlFilename = "scaffolding.config";
        private readonly string _solutionConfigFilename;
        
        public XmlScaffoldingConfigStore(ISolutionManager solutionManager)
        {
            if (solutionManager == null) throw new ArgumentNullException("solutionManager");
            _solutionManager = solutionManager;

            // Locate the solution-wide config file and ensure it exists
            var solutionDirectory = _solutionManager.SolutionDirectory ?? ScaffoldingPackagePathResolver.InferSolutionDirectory();
            _solutionConfigFilename = Path.Combine(solutionDirectory, ConfigXmlFilename);
            if (!File.Exists(_solutionConfigFilename))
                SaveConfigToFile(_solutionConfigFilename, new XmlScaffoldingConfig());
        }

        public IQueryable<DefaultScaffolderConfigEntry> GetProjectDefaultScaffolders(Project project)
        {
            return LoadProjectConfig(project).DefaultScaffolders.AsQueryable();
        }

        public IQueryable<DefaultScaffolderConfigEntry> GetSolutionDefaultScaffolders()
        {
            return LoadConfigFromFile(_solutionConfigFilename).DefaultScaffolders.AsQueryable();
        }

        public void SetProjectDefaultScaffolder(Project project, string defaultName, string scaffolderName, bool doNotOverwriteExistingSetting)
        {
            var config = LoadProjectConfig(project);
            if (doNotOverwriteExistingSetting && config.DefaultScaffolders.Any(x => x.DefaultName.Equals(defaultName, StringComparison.OrdinalIgnoreCase)))
                return;

            SetDefaultScaffolderConfigEntry(config, defaultName, scaffolderName);
            SaveProjectConfig(project, config);
        }

        public void SetSolutionDefaultScaffolder(string defaultName, string scaffolderName, bool doNotOverwriteExistingSetting)
        {
            var config = LoadConfigFromFile(_solutionConfigFilename);
            if (doNotOverwriteExistingSetting && config.DefaultScaffolders.Any(x => x.DefaultName.Equals(defaultName, StringComparison.OrdinalIgnoreCase)))
                return;

            SetDefaultScaffolderConfigEntry(config, defaultName, scaffolderName);
            SaveConfigToFile(_solutionConfigFilename, config);
        }

        private static void SetDefaultScaffolderConfigEntry(XmlScaffoldingConfig config, string defaultName, string scaffolderName)
        {
            // Replace any existing entry for this defaultName with the new one
            config.DefaultScaffolders.RemoveAll(x => x.DefaultName.Equals(defaultName, StringComparison.OrdinalIgnoreCase));
            config.DefaultScaffolders.Add(new DefaultScaffolderConfigEntry(defaultName, scaffolderName));
            config.DefaultScaffolders = config.DefaultScaffolders.OrderBy(x => x.DefaultName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void SaveProjectConfig(Project project, XmlScaffoldingConfig config)
        {
            var projectConfigItem = project.GetProjectItem(ConfigXmlFilename);
            if (projectConfigItem != null) 
                SaveConfigToFile(projectConfigItem.GetFullPath(), config);
            else {
                var outputFilename = Path.Combine(project.GetFullPath(), ConfigXmlFilename);
                SaveConfigToFile(outputFilename, config);
                project.ProjectItems.AddFromFile(outputFilename);
            }
        }

        private void SaveConfigToFile(string filename, XmlScaffoldingConfig config)
        {
            _solutionManager.EnsureCheckedOutIfExists(filename);
            using (var writer = new XmlTextWriter(filename, Encoding.UTF8) { Formatting = Formatting.Indented }) {
                var serializer = new XmlSerializer(typeof(XmlScaffoldingConfig));
                serializer.Serialize(writer, config);
            }
        }

        private static XmlScaffoldingConfig LoadProjectConfig(Project project)
        {
            var projectConfigItem = project.GetProjectItem(ConfigXmlFilename);
            return projectConfigItem != null ? LoadConfigFromFile(projectConfigItem.GetFullPath()) 
                                             : new XmlScaffoldingConfig();
        }

        private static XmlScaffoldingConfig LoadConfigFromFile(string filename)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException(string.Format("Scaffolding config file not found. Tried to read: {0}", filename));

            using (var reader = new XmlTextReader(filename)) {
                reader.MoveToContent();
                var serializer = new XmlSerializer(typeof(XmlScaffoldingConfig));
                return (XmlScaffoldingConfig)serializer.Deserialize(reader);
            }
        }
    }
}