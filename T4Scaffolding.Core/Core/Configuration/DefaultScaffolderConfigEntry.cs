using System;
using System.Xml.Serialization;

namespace T4Scaffolding.Core.Configuration
{
    public class DefaultScaffolderConfigEntry
    {
        private string _defaultName;
        [XmlAttribute] public string DefaultName
        {
            get { return _defaultName; }
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentException("ScaffolderName cannot be null or empty");

                _defaultName = value;
            }
        }

        private string _scaffolderName;
        [XmlAttribute] public string ScaffolderName
        {
            get { return _scaffolderName; }
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentException("ScaffolderName cannot be null or empty");

                _scaffolderName = value;
            }
        }

        public DefaultScaffolderConfigEntry()
        {
        }

        public DefaultScaffolderConfigEntry(string defaultName, string scaffolderName)
        {
            DefaultName = defaultName;
            ScaffolderName = scaffolderName;
        }
    }
}