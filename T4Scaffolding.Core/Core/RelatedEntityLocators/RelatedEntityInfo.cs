using System;
using System.Data.Entity.Design.PluralizationServices;
using EnvDTE;

namespace T4Scaffolding.Core.RelatedEntityLocators
{
    [Serializable]
    public class RelatedEntityInfo
    {
        public RelationType RelationType { get; set; }
        public string RelationName { get; private set; }
        public string RelationNamePlural { get; private set; }
        public CodeProperty RelationProperty { get; private set; }    // For parent entities, this is the primary key property. For child entities, it's the collection property.
        public CodeProperty LazyLoadingProperty { get; private set; } // For parent entities, this is optionally another property matching the relation name. For child entities, it's the collection property.
        public CodeType RelatedEntityType { get; private set; }
        public string RelatedEntityTypeNamePlural { get; private set; }
        public string RelatedEntityPrimaryKeyName { get; private set; }

        public RelatedEntityInfo(RelationType relationType, string relationName, CodeProperty relationProperty, CodeType relatedEntityType, string relatedEntityPrimaryKeyName, CodeProperty lazyLoadingProperty)
        {
            if (string.IsNullOrEmpty(relationName)) throw new ArgumentException("'relationName' cannot be null or empty.");
            if (relationProperty == null) throw new ArgumentNullException("relationProperty");
            if (relatedEntityType == null) throw new ArgumentNullException("relatedEntityType");
            if ((relationType == RelationType.Parent) && string.IsNullOrEmpty(relatedEntityPrimaryKeyName)) throw new ArgumentException("'relatedEntityPrimaryKeyName' cannot be null or empty for parent entities.");

            RelationType = relationType;
            RelationName = relationName;
            RelationNamePlural = Pluralize(relationName);
            RelationProperty = relationProperty;
            RelatedEntityType = relatedEntityType;
            RelatedEntityTypeNamePlural = Pluralize(relatedEntityType.Name);
            RelatedEntityPrimaryKeyName = relatedEntityPrimaryKeyName;
            LazyLoadingProperty = lazyLoadingProperty;
        }

        private static string Pluralize(string word)
        {
            try {
                var pluralizationService = PluralizationService.CreateService(System.Threading.Thread.CurrentThread.CurrentUICulture);
                return pluralizationService.Pluralize(word);
            } catch (NotImplementedException) {
                // Unsupported culture
                return word;
            }
        }
    }
}