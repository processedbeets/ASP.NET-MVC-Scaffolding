using System;

namespace T4Scaffolding
{
    public class ViewScaffolderAttribute : ScaffolderAttribute
    {
        public string DisplayName { get; set; }
        public string LayoutPageFilter { get; set; }
        public bool IsRazorType { get; set; }

        public ViewScaffolderAttribute(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                throw new ArgumentException("Value cannot be null or empty", "displayName");

            DisplayName = displayName;
        }
    }
}