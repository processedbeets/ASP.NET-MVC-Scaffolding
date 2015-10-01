using System.Management.Automation;

namespace T4Scaffolding.Core.ScaffolderLocators
{
    public class ScaffolderInfo
    {
        public string Name { get; private set; }
        public string PackageName { get; private set; }
        public string Location { get; private set; }
        public CommandInfo Command { get; private set; }
        public ScaffolderAttribute ScaffolderAttribute { get; set; }
        public string Description { get { return ScaffolderAttribute == null ? null : ScaffolderAttribute.Description; } }

        public ScaffolderInfo(string name, string packageName, string location, CommandInfo command, ScaffolderAttribute scaffolderAttribute)
        {
            Name = name;
            PackageName = packageName;
            Location = location;
            Command = command;
            ScaffolderAttribute = scaffolderAttribute;
        }
    }
}