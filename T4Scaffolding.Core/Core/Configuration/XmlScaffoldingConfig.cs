using System.Collections.Generic;
using System.Xml.Serialization;

namespace T4Scaffolding.Core.Configuration
{
    [XmlRoot("Config")]
    public class XmlScaffoldingConfig
    {
        [XmlArrayItem("Default")]
        public List<DefaultScaffolderConfigEntry> DefaultScaffolders { get; set; }

        public XmlScaffoldingConfig()
        {
            DefaultScaffolders = new List<DefaultScaffolderConfigEntry>();
        }
    }
}