using System;
using EnvDTE;

namespace T4Scaffolding
{
    /// <summary>
    /// Utility method - produces the "Begin Namespace ..." and "End Namespace" lines for VB
    /// accounting for VB's "omit default namespace" convention and the possibility of there
    /// being no namespace lines required
    /// </summary>
    public static class Namespaces
    {
        public static string BeginVb(string fullNamespace, string defaultNamespace)
        {
            var output = NamespaceToOutput(fullNamespace, defaultNamespace);
            return string.IsNullOrEmpty(output) ? null : "Namespace " + output;
        }

        public static string EndVb(string fullNamespace, string defaultNamespace)
        {
            var output = NamespaceToOutput(fullNamespace, defaultNamespace);
            return string.IsNullOrEmpty(output) ? null : "End Namespace";            
        }

        public static string Normalize(string fullNamespace)
        {
            if (string.IsNullOrEmpty(fullNamespace))
                return null;
            return string.Join(".", fullNamespace.Split(new [] { '.' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static string GetNamespace(string fullyQualifiedTypeName)
        {
            if (string.IsNullOrEmpty(fullyQualifiedTypeName))
                return null;
            if (!fullyQualifiedTypeName.Contains("."))
                return null;
            return fullyQualifiedTypeName.Substring(0, fullyQualifiedTypeName.LastIndexOf('.'));
        }

        private static string NamespaceToOutput(string fullNamespace, string defaultNamespace)
        {
            // Sometimes namespaces may be constructed as: defaultNamespace + ".Something"
            // Fix up the case where defaultNamespace is blank
            if ((fullNamespace != null) && fullNamespace.StartsWith("."))
                fullNamespace = fullNamespace.Substring(1);
            
            // Degenerate case)
            if (string.IsNullOrEmpty(fullNamespace))
                return null;

            // It's possible to disable the default namespace, in which case you get C#-like behavior
            if (string.IsNullOrEmpty(defaultNamespace))
                return fullNamespace;

            // There is no namespace line if you're in the default namespace
            if (string.Equals(fullNamespace, defaultNamespace, StringComparison.OrdinalIgnoreCase)) 
                return null;

            // Omit leading default namespace
            if (fullNamespace.StartsWith(defaultNamespace + "."))
                return fullNamespace.Substring(defaultNamespace.Length + 1);

            // You're asking for an unrelated namespace, so leave it unchanged
            return fullNamespace;
        }
    }
}