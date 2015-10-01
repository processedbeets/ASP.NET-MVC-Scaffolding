using System;

namespace T4Scaffolding
{
    public class ScaffolderAttribute : Attribute
    {
        public string Description { get; set; }

        /// <summary>
        /// If true, the scaffolder will not appear in any of the usual lists of scaffolders, and will only appear
        /// when we query for the set of scaffolders that should appear in the GUI.
        /// </summary>
        public bool HideInConsole { get; set; } 
    }
}
