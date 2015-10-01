using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using EnvDTE;
using EnvDTE80;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.VisualStudio.TextTemplating;
using T4Scaffolding.Core.EnvDTE;
using T4Scaffolding.Core.RelatedEntityLocators;

namespace T4Scaffolding.Core.Templating {
    [Serializable]
    public class DynamicTextTemplatingEngineHost : MarshalByRefObject, ITextTemplatingEngineHost, IDisposable
    {
        private readonly List<string> _assemblyReferencesLocations = new[] {
            typeof(Uri).Assembly,                       // System
            typeof(System.Data.Linq.Binary).Assembly,   // System.Data.Linq
            typeof(DynamicMetaObject).Assembly,         // System.Dynamic
            typeof(RuntimeBinderException).Assembly,    // Microsoft.CSharp
            typeof(CodeClass).Assembly,                 // EnvDTE
            typeof(CodeClass2).Assembly,                // EnvDTE80
            Assembly.GetExecutingAssembly(),            // T4Scaffolding
        }.Select(x => x.Location).ToList();

        private AppDomain _templateAppDomain;

        public DynamicTextTemplatingEngineHost() {
            FileEncoding = Encoding.UTF8;
            FileExtension = String.Empty;            
        }

        public dynamic Model { get; set; }
        public string TemplateFile { get; set; }
        public string FileExtension { get; private set; }
        public Encoding FileEncoding { get; private set; }

        public CompilerErrorCollection Errors { get; private set; }

        public IList<string> StandardAssemblyReferences {
            get {
                return _assemblyReferencesLocations.AsReadOnly();
            }
        }

        public IList<string> StandardImports {
            get {
                return new[] {
                    "System",
                    typeof(DynamicViewModel).Namespace,
                    typeof(EnvDTEExtensions).Namespace,
                    typeof(RelatedEntityLocation).Namespace,
                };
            }
        }

        // We only want to reference assemblies that you specifically request using an <@ Assembly @> directive.
        // To make it possible to reference your project assemblies using such a directive (and without putting
        // them in the GAC), we hold a list of assemblies we know about that you might be trying to reference.
        private readonly List<string> _findableAssemblies = new List<string>();
        public void AddFindableAssembly(string location)
        {
            if (!string.IsNullOrEmpty(location))
                _findableAssemblies.Add(location);
        }

        /// <summary>
        /// The included text is returned in the content parameter.
        /// If the host searches the registry for the location of include files,
        /// or if the host searches multiple locations by default, the host can
        /// return the final path of the include file in the location parameter.
        /// </summary>
        public bool LoadIncludeText(string requestFileName, out string content, out string location) {
            content = String.Empty;
            location = String.Empty;

            // If the argument is the fully qualified path of an existing file, then we are done.
            if (File.Exists(requestFileName)) {
                content = File.ReadAllText(requestFileName);
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Called by the Engine to enquire about 
        /// the processing options you require. 
        /// </summary>
        public object GetHostOption(string optionName) {
            switch (optionName) {
                case "CacheAssemblies":
                    return true;
                default:
                    return null;
            }
        }


        /// <summary>
        /// The engine calls this method to resolve assembly references used in
        /// the generated transformation class project and for the optional 
        /// assembly directive if the user has specified it in the text template.
        /// This method can be called 0, 1, or more times.
        /// </summary>
        public string ResolveAssemblyReference(string assemblyReference) {
            // If the argument is the fully qualified path of an existing file,
            // then we are done. (This does not do any work.)
            if (File.Exists(assemblyReference)) {
                return assemblyReference;
            }

            // Maybe the assembly is in the same folder as the text template that 
            // called the directive.
            string candidate = Path.Combine(Path.GetDirectoryName(TemplateFile), assemblyReference);
            if (File.Exists(candidate)) {
                return candidate;
            }

            // Maybe it's the name of an assembly we've already loaded. (In that case, don't load it from a different location)
            var alreadyLoadedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name.Equals(assemblyReference, StringComparison.Ordinal) || x.GetName().FullName.Equals(assemblyReference, StringComparison.Ordinal));
            if (alreadyLoadedAssembly != null)
                return alreadyLoadedAssembly.Location;

            // Maybe it's the name of something we can find among the FindableAssemblies collection
            foreach (var location in _findableAssemblies.Where(File.Exists)) {
                var assemblyName = AssemblyName.GetAssemblyName(location);
                if (assemblyName.FullName.Equals(assemblyReference, StringComparison.Ordinal) || assemblyName.Name.Equals(assemblyReference, StringComparison.Ordinal))
                    return location;
            }

            // Maybe it's a fully-qualified reference to something in the GAC
            try {
                Assembly assembly = Assembly.Load(assemblyReference);
                if (assembly != null) {
                    return assembly.Location;
                }
            }
            catch (FileNotFoundException) { }
            catch (FileLoadException) { }
            catch (BadImageFormatException) { }

            return null;
        }

        public Type ResolveDirectiveProcessor(string processorName) {
            // This host will not resolve any specific processors.
            throw new Exception("Directive Processor not found");
        }

        /// <summary>
        /// A directive processor can call this method if a file name does not 
        /// have a path.
        /// The host can attempt to provide path information by searching 
        /// specific paths for the file and returning the file and path if found.
        /// This method can be called 0, 1, or more times.
        /// </summary>
        public string ResolvePath(string fileName) {
            if (fileName == null) throw new ArgumentNullException("fileName");

            // If the argument is the fully qualified path of an existing file,
            // then we are done
            if (File.Exists(fileName)) {
                return fileName;
            }

            // Maybe the file is in the same folder as the text template that 
            // called the directive.
            string candidate = Path.Combine(Path.GetDirectoryName(TemplateFile), fileName);
            if (File.Exists(candidate)) {
                return candidate;
            }

            return fileName;
        }

        /// <summary>
        /// If a call to a directive in a text template does not provide a value
        /// for a required parameter, the directive processor can try to get it
        /// from the host by calling this method.
        /// This method can be called 0, 1, or more times.
        /// </summary>
        public string ResolveParameterValue(string directiveId, string processorName, string parameterName) {
            if (directiveId == null) throw new ArgumentNullException("directiveId");
            if (processorName == null) throw new ArgumentNullException("processorName");
            if (parameterName == null) throw new ArgumentNullException("parameterName");

            return String.Empty;
        }

        public void SetFileExtension(string extension) {
            FileExtension = extension;
        }

        public void SetOutputEncoding(Encoding encoding, bool fromOutputDirective) {
            FileEncoding = encoding;
        }

        public void LogErrors(CompilerErrorCollection errors) {
            Errors = errors;
        }

        public AppDomain ProvideTemplatingAppDomain(string content)
        {
            if (_templateAppDomain == null)
                _templateAppDomain = AppDomain.CreateDomain("Generation App Domain");
            return _templateAppDomain;
        }

        public void Dispose()
        {
            if (_templateAppDomain != null)
                AppDomain.Unload(_templateAppDomain);
        }
    }
}