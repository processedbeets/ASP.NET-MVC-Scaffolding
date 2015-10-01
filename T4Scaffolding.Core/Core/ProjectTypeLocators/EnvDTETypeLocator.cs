using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using EnvDTE80;

namespace T4Scaffolding.Core.ProjectTypeLocators
{
    class EnvDTETypeLocator : IProjectTypeLocator
    {
        // For the sake of speed, when scanning for types by unqualified name, ignore the following top-level namespaces
        // So, you must not put model classes in these namespaces (which would be a wrong thing to do anyway), or if you do,
        // you need to reference them by fully-qualified name
        private static readonly string[] ExcludedNamespaces = new[] { "Microsoft", "MS", "System" };

        public CodeType FindUniqueType(Project project, string typeName)
        {
            var results = FindTypes(project, typeName).ToList();
            switch (results.Count)
            {
                case 0:
                    throw new InvalidOperationException(string.Format("Cannot find a type matching the name '{0}'. Try specifying the fully-qualified type name, including namespace.", typeName));
                case 1:
                    return results.Single();
                default:
                    throw new InvalidOperationException(string.Format("Ambiguous type name '{0}'. Try specifying the fully-qualified type name, including namespace.", typeName));
            }
        }

        public IEnumerable<CodeType> FindTypes(Project project, string typeName)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (string.IsNullOrEmpty(typeName)) throw new ArgumentException("'typeName' cannot be null or empty.");

            // First try a direct lookup on the main project itself
            var foundExact = FindTypeExactMatch(project, typeName);
            // Otherwise try direct lookups on referenced projects
            if (foundExact == null)
                foundExact = GetReferencedSolutionProjects(project, false).Select(x => FindTypeExactMatch(x, typeName)).Where(x => x != null).FirstOrDefault();
            if (foundExact != null)
                return new[] { foundExact };

            // Not found, so scan the projects for similar type names
            var allTypes = FindAllTypes(project).ToList();
            var results = allTypes.Where(x => typeName.Equals(x.Name, StringComparison.Ordinal) || typeName.Equals(x.FullName, StringComparison.Ordinal)).ToList();
            if (!results.Any())
                results = allTypes.Where(x => typeName.Equals(x.Name, StringComparison.OrdinalIgnoreCase) || typeName.Equals(x.FullName, StringComparison.OrdinalIgnoreCase)).ToList();

            // For C#, partial classes will be represented by a single CodeType instance
            // For VB, we get a separate CodeType instance for each member of the set of partial classes. Normalize each set down to a single representative.
            return PickArbitraryRepresentativeOfPartialClasses(results);
        }

        public IEnumerable<CodeType> FindAllTypes(Project project)
        {
            if (project == null) throw new ArgumentNullException("project");

            var projectsToSearch = GetReferencedSolutionProjects(project, true);
            return projectsToSearch.SelectMany(x => GetCodeTypesFromLocalCodeElements(x.CodeModel.CodeElements));
        }

        /// <summary>
        /// Gets the type only if typeName is its fully-qualified name and it's local to the specified project
        /// </summary>
        public CodeType FindTypeExactMatch(Project project, string typeName)
        {
            try {
                var fullNameResult = project.CodeModel.CodeTypeFromFullName(typeName);
                if ((fullNameResult != null) && (fullNameResult.InfoLocation == vsCMInfoLocation.vsCMInfoLocationProject))
                    return fullNameResult;
            } catch (ArgumentException) {
                // For VB projects it throws an ArgumentException if the type wasn't found, whereas
                // for C# projects it returns null in that case.
            }
            return null;
        }

        /// <summary>
        /// Picks out the referenced projects that are defined in your solution (i.e., are not external assemblies)
        /// by discarding references without a SourceProject
        /// </summary>
        private static IEnumerable<Project> GetReferencedSolutionProjects(Project project, bool includeSuppliedProject)
        {
            var result = new List<Project>();
            dynamic projectObject = project.Object;
            foreach (dynamic reference in projectObject.References) {
                if (reference.SourceProject is Project)
                    result.Add((Project)reference.SourceProject);
            }
            if (includeSuppliedProject && !result.Contains(project))
                result.Insert(0, project);
            return result;
        }

        /// <summary>
        /// Recursively scan to extract all CodeType elements. But since the CodeModel contains *all*
        /// referenced types (including the whole of System.*), it would be too slow to consider them all. 
        /// We're only interested in types defined in the project itself, so we disregard namespaces that 
        /// contain one or more type with InfoLocation != vsCMInfoLocation.vsCMInfoLocationProject
        /// </summary>
        private static IEnumerable<CodeType> GetCodeTypesFromLocalCodeElements(CodeElements codeElements)
        {
            var results = new List<CodeType>();
            foreach (CodeElement codeElement in codeElements) {
                if ((codeElement is CodeType) && (codeElement.InfoLocation == vsCMInfoLocation.vsCMInfoLocationProject)) {
                    results.Add((CodeType)codeElement);
                }

                CodeElements childrenToSearch = null;
                if ((codeElement is CodeClass) && (codeElement.InfoLocation == vsCMInfoLocation.vsCMInfoLocationProject)) {
                    childrenToSearch = ((CodeClass)codeElement).Members;
                } else if (codeElement is CodeNamespace) {
                    var codeNamespace = (CodeNamespace)codeElement;
                    if (!ExcludedNamespaces.Contains(codeNamespace.FullName, StringComparer.Ordinal))
                        childrenToSearch = codeNamespace.Members;
                }
                if (childrenToSearch != null) {
                    var childResults = GetCodeTypesFromLocalCodeElements(childrenToSearch);
                    results.AddRange(childResults);
                }
            }
            return results;
        }

        /// <summary>
        /// Out of a set of CodeType instances, some of them may be different partials of the same class.
        /// This method filters down such a set so that you get only one partial per class.
        /// </summary>
        private static List<CodeType> PickArbitraryRepresentativeOfPartialClasses(IEnumerable<CodeType> codeTypes)
        {
            var representatives = new List<CodeType>();
            foreach (var codeType in codeTypes) {
                var codeClass2 = codeType as CodeClass2;
                if (codeClass2 != null) {
                    var matchesExistingRepresentative = (from candidate in representatives.OfType<CodeClass2>()
                                                         let candidatePartials = candidate.PartialClasses.OfType<CodeClass2>()
                                                         where candidatePartials.Contains(codeClass2)
                                                         select candidate).Any();
                    if (!matchesExistingRepresentative)
                        representatives.Add(codeType);
                } else {
                    // Can't have partials because it's not a CodeClass2, so it can't clash with others
                    representatives.Add(codeType);
                }
            }
            return representatives;
        }
    }
}
