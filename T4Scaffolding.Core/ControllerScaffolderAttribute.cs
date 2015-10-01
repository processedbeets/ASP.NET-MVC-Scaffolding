using System;

namespace T4Scaffolding
{
    public class ControllerScaffolderAttribute : ScaffolderAttribute
    {
        public string DisplayName { get; set; }
        public bool SupportsModelType { get; set; }
        public bool SupportsDataContextType { get; set; }
        public bool SupportsViewScaffolder { get; set; }
        public Type ViewScaffolderSelector { get; set; }

        public ControllerScaffolderAttribute(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                throw new ArgumentException("Value cannot be null or empty", "displayName");

            DisplayName = displayName;
        }
    }
}