using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Linq;
using System.Linq;
using System.Text.RegularExpressions;
using EnvDTE;
using Microsoft.CSharp;
using Microsoft.VisualBasic;
using T4Scaffolding.Core.EnvDTE;
using T4Scaffolding.Core.PrimaryKeyLocators;
using T4Scaffolding.Core.ProjectTypeLocators;

namespace T4Scaffolding.Core.RelatedEntityLocators
{
    public static class RelatedEntityLocation
    {
        public static IRelatedEntityLocator CurrentRelatedEntityLocator { get; private set; }

        static RelatedEntityLocation()
        {
            CurrentRelatedEntityLocator = new DefaultRelatedEntityLocator();
        }

        /// <summary>
        /// Finds related entities where:
        ///  * There is a property called SomethingID of type SomeClass (i.e., property name ends with "ID", case-insensitively)
        ///    or a property called SomeRelationID and one called SomeRelation of type SomeClass
        ///  * The type SomeClass is local to your project or one of its references
        ///  * The type SomeClass has a single primary key property
        /// 
        /// In other words, the two patterns we support are:
        ///  * public class Contact { public int CompanyId { get; set; } 
        ///    where there is a class Company with a single primary key
        ///  * public class Person { public int MotherId { get; set; }  public Person Mother { get; set; } } 
        ///    to support self-referencing entities and entities with multiple relations of a given type
        /// </summary>
        class DefaultRelatedEntityLocator : IRelatedEntityLocator
        {
            private static readonly string PropertySuffix = "id";

            public IEnumerable<RelatedEntityInfo> GetRelatedEntities(CodeType codeType, Project project, IProjectTypeLocator projectTypeLocator)
            {
                var thisEntityPrimaryKeys = PrimaryKeyLocation.GetPrimaryKeys(codeType).ToList();

                var propertiesToConsider = (from property in codeType.VisibleMembers().OfType<CodeProperty>()
                                           where ((CodeElement)property).IsPublic()
                                              && !thisEntityPrimaryKeys.Contains(property) // Exclude its own primary keys, otherwise it will always be related to itself
                                           select property).ToList();

                foreach (var property in propertiesToConsider) {
                    var relatedEntityInfo = GetRelatedParentEntityInfo(property, propertiesToConsider, project, projectTypeLocator)
                                            ?? GetRelatedChildEntityInfo(property, project, projectTypeLocator);
                    if (relatedEntityInfo != null)
                        yield return relatedEntityInfo;
                }
            }

            private static RelatedEntityInfo GetRelatedParentEntityInfo(CodeProperty property, IEnumerable<CodeProperty> allCandidateProperties, Project project, IProjectTypeLocator projectTypeLocator)
            {
                // First, we need to be able to extract "SomeType" from properties called "SomeTypeID"
                // "SomeType" is the name of the relation, and either corresponds to an entity type, or to another property whose type is an entity
                if (!property.Name.EndsWith(PropertySuffix, StringComparison.OrdinalIgnoreCase))
                    return null;
                var relationName = property.Name.Substring(0, property.Name.Length - PropertySuffix.Length);
                if (string.IsNullOrEmpty(relationName))
                    return null;

                // Next, "SomeType" needs to correspond to plausible entity or be the name of another property whose type is a plausible entity 
                List<CodeType> foundRelatedEntityTypes;
                var propertyNameComparison = VsConstants.VbCodeModelLanguageGuid.Equals(property.Language, StringComparison.OrdinalIgnoreCase) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                var matchesOtherCandidateProperty = allCandidateProperties.FirstOrDefault(x => string.Equals(x.Name, relationName, propertyNameComparison));
                if ((matchesOtherCandidateProperty != null) && (matchesOtherCandidateProperty.Type != null) && (matchesOtherCandidateProperty.Type.CodeType != null))
                    foundRelatedEntityTypes = new[] { matchesOtherCandidateProperty.Type.CodeType }.ToList();
                else
                    foundRelatedEntityTypes = projectTypeLocator.FindTypes(project, relationName).Distinct().ToList();

                // Get the primary key info for each possible type match so we can check it's a plausible entity.
                var foundRelatedEntityTypesPlusPrimaryKeyInfo = (from relatedEntityType in foundRelatedEntityTypes
                                                                 select new {
                                                                     type = relatedEntityType,
                                                                     primaryKeys = PrimaryKeyLocation.GetPrimaryKeys(relatedEntityType).ToList()
                                                                 }).ToList();

                // It's only a plausible entity if it has a single primary key, so filter out ones that don't, then ensure we're left with an unambiguous match
                foundRelatedEntityTypesPlusPrimaryKeyInfo.RemoveAll(x => x.primaryKeys.Count != 1);
                if (foundRelatedEntityTypesPlusPrimaryKeyInfo.Count != 1)
                    return null;

                return new RelatedEntityInfo(RelationType.Parent, relationName, property, foundRelatedEntityTypesPlusPrimaryKeyInfo.Single().type, foundRelatedEntityTypesPlusPrimaryKeyInfo.Single().primaryKeys.Single().Name, matchesOtherCandidateProperty);
            }

            private static RelatedEntityInfo GetRelatedChildEntityInfo(CodeProperty property, Project project, IProjectTypeLocator projectTypeLocator)
            {
                // We are only interested in properties that implement ICollection<TEntity>
                var collectionElementType = ExtractFirstGenericArgOfCollectionType(property, project, projectTypeLocator);
                if (collectionElementType == null)
                    return null;
                
                // The element type has to be plausibly an entity, meaning that it has a single primary key
                var collectionElementTypePrimaryKeys = PrimaryKeyLocation.GetPrimaryKeys(collectionElementType).ToList();
                if (collectionElementTypePrimaryKeys.Count != 1)
                    return null;

                return new RelatedEntityInfo(RelationType.Child, property.Name, property, collectionElementType, collectionElementTypePrimaryKeys.Single().Name, property);
            }

            private static CodeType ExtractFirstGenericArgOfCollectionType(CodeProperty property, Project project, IProjectTypeLocator projectTypeLocator)
            {
                // This is difficult for VB types because the API won't let us know the generic args when scanning the type hierarchy.
                // We can see if a type implements ICollection<T>, but there's no way to know what T is. So, try to spot some
                // common collection types at the CodeTypeRef level where VB gives us a string representation of the closed generic type.
                var genericArg = ExtractFirstGenericArgOfSpecificCollectionType(property.Type, property.Language, typeof (List<>), typeof (IList<>), typeof(Collection<>), typeof (ICollection<>), typeof (EntitySet<>));
                
                // If this failed, then for C# types at least, we can scan up the inheritance hierarchy looking for something that implements ICollection<T>
                if (string.IsNullOrEmpty(genericArg)) {
                    if (!string.Equals(property.Language, VsConstants.VbCodeModelLanguageGuid, StringComparison.OrdinalIgnoreCase))
                        genericArg = ExtractFirstGenericArgOfImplementedICollectionInterface(property.Type.CodeType as CodeClass);
                }

                // Convert the string representation of the collection element type to a project-local CodeType
                if (string.IsNullOrEmpty(genericArg))
                    return null;
                var foundTypes = projectTypeLocator.FindTypes(project, genericArg).ToList();
                if (foundTypes.Count != 1) // Only accept unambiguous match
                    return null;
                return foundTypes.Single();
            }

            private static string ExtractFirstGenericArgOfImplementedICollectionInterface(CodeClass codeClass)
            {
                if (codeClass == null)
                    return null;
                var iCollectionRegex = new Regex(@"^System\.Collections\.Generic\.ICollection<([^>]+)>$");
                foreach (var codeInterface in codeClass.ImplementedInterfaces.OfType<CodeInterface>()) {
                    var match = iCollectionRegex.Match(codeInterface.FullName);
                    if (match.Success)
                        return match.Groups[1].Captures[0].Value;
                }
                // No match at this level. Recurse up the hierarchy.
                return codeClass.Bases.OfType<CodeClass>().Select(ExtractFirstGenericArgOfImplementedICollectionInterface).FirstOrDefault(x => !string.IsNullOrEmpty(x));
            }

            private static string ExtractFirstGenericArgOfSpecificCollectionType(CodeTypeRef typeRef, string codeLanguageGuid, params Type[] candidateCollectionTypes)
            {
                if ((typeRef == null) || string.IsNullOrEmpty(typeRef.AsFullName))
                    return null;

                var codeDomProvider = string.Equals(codeLanguageGuid, VsConstants.VbCodeModelLanguageGuid, StringComparison.OrdinalIgnoreCase)
                                          ? (CodeDomProvider)new VBCodeProvider()
                                          : new CSharpCodeProvider();
                foreach (var collectionType in candidateCollectionTypes) {
                    var languageSpecificCollectionTypeName = codeDomProvider.GetTypeOutput(new CodeTypeReference(collectionType));
                    var regex = string.Format("^{0}$", languageSpecificCollectionTypeName.Replace(".", @"\.")
                        .Replace(")", string.Empty)
                        .Replace(">", string.Empty)
                        .Replace("<", @"<([^>]+)>"))
                        .Replace("(Of ", @"\(Of ([^\)]+)\)");
                    var match = Regex.Match(typeRef.AsFullName, regex);
                    if (match.Success)
                        return match.Groups[1].Captures[0].Value;
                }
                return null;
            }
        }
    }
}