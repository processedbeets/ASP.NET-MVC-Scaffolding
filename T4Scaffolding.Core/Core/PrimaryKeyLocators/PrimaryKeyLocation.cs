using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Linq.Mapping;
using System.Data.Objects.DataClasses;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using T4Scaffolding.Core.EnvDTE;

namespace T4Scaffolding.Core.PrimaryKeyLocators
{
    public static class PrimaryKeyLocation
    {
        public static IList<IPrimaryKeyLocator> PrimaryKeyLocators { get; private set; }

        static PrimaryKeyLocation()
        {
            PrimaryKeyLocators = new List<IPrimaryKeyLocator> {
                new IdPropertyLocator(),
                new TypeNameIdPropertyLocator(),
                new KeyAttributePropertyLocator(),
                new EdmScalarPropertyAttributePropertyLocator(),
                new ColumnAttributePropertyLocator(),
            };
        }

        public static IEnumerable<CodeProperty> GetPrimaryKeys(CodeType codeType)
        {
            var membersToConsider = codeType.VisibleMembers();
            return membersToConsider.OfType<CodeProperty>().Where(x => IsPrimaryKey(x.Parent, x)).ToList();
        }

        private static bool IsPrimaryKey(CodeClass codeClass, CodeProperty codeProperty)
        {
            return PrimaryKeyLocators.Any(x => x.IsPrimaryKey(codeClass, codeProperty));
        }

        private static bool CodePropertyHasAttributeWithArgValue(CodeProperty codeProperty, Type attributeType, string argName, string value, StringComparison valueComparer)
        {
            return (from attrib in codeProperty.Attributes.OfType<CodeAttribute2>()
                    where attrib.FullName == attributeType.FullName
                    from arg in attrib.Arguments.OfType<CodeAttributeArgument>()
                    where arg.Name.Equals(argName, valueComparer)
                       && arg.Value.Equals(value, valueComparer)
                    select arg).Any();
        }

        public class IdPropertyLocator : IPrimaryKeyLocator
        {
            public bool IsPrimaryKey(CodeClass codeClass, CodeProperty codeProperty)
            {
                return string.Equals("id", codeProperty.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        public class TypeNameIdPropertyLocator : IPrimaryKeyLocator
        {
            public bool IsPrimaryKey(CodeClass codeClass, CodeProperty codeProperty)
            {
                return string.Equals(codeClass.Name + "id", codeProperty.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        public class KeyAttributePropertyLocator : IPrimaryKeyLocator
        {
            public bool IsPrimaryKey(CodeClass codeClass, CodeProperty codeProperty)
            {
                return codeProperty.Attributes.OfType<CodeAttribute2>().Any(x => x.FullName == typeof(KeyAttribute).FullName);
            }
        }

        public class EdmScalarPropertyAttributePropertyLocator : IPrimaryKeyLocator
        {
            public bool IsPrimaryKey(CodeClass codeClass, CodeProperty codeProperty)
            {
                return CodePropertyHasAttributeWithArgValue(codeProperty, typeof(EdmScalarPropertyAttribute), "EntityKeyProperty", bool.TrueString, StringComparison.OrdinalIgnoreCase);
            }
        }

        public class ColumnAttributePropertyLocator : IPrimaryKeyLocator
        {
            public bool IsPrimaryKey(CodeClass codeClass, CodeProperty codeProperty)
            {
                return CodePropertyHasAttributeWithArgValue(codeProperty, typeof(ColumnAttribute), "IsPrimaryKey", bool.TrueString, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}