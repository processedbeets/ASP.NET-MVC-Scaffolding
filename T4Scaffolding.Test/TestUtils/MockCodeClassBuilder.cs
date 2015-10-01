using System;
using System.Collections.Generic;
using System.Data.Linq.Mapping;
using System.Data.Objects.DataClasses;
using System.Linq;
using System.Reflection;
using EnvDTE;
using EnvDTE80;
using Moq;
using T4Scaffolding.Core;
using T4Scaffolding.Core.ProjectTypeLocators;

namespace T4Scaffolding.Test.TestUtils
{
    internal static class MockCodeClassBuilder
    {
        public static CodeClass BuildMockCodeClass(Type type, Mock<IProjectTypeLocator> mockTypeLocator, Project project)
        {
            var mockCodeClass = new Mock<CodeClass>();
            var mockCodeClassAsCodeType = mockCodeClass.As<CodeType>(); // Implements CodeType too

            var baseProperties = type.BaseType != null ? type.BaseType.GetProperties() : Enumerable.Empty<PropertyInfo>();
            var propertiesExcludingInherited = type.GetProperties().Where(prop => !baseProperties.Any(baseProp => baseProp.Name == prop.Name));
            var properties = propertiesExcludingInherited.Select(prop =>
            {
                var mockCodeProperty = new Mock<CodeProperty>();
                var mockCodePropertyAsCodeElement = mockCodeProperty.As<CodeElement>(); // Implements CodeElement too
                mockCodeProperty.SetupGet(x => x.Name).Returns(prop.Name);
                mockCodePropertyAsCodeElement.SetupGet(x => x.Name).Returns(prop.Name);
                mockCodeProperty.SetupGet(x => x.Access).Returns(vsCMAccess.vsCMAccessPublic);
                mockCodeProperty.SetupGet(x => x.Parent).Returns(mockCodeClass.Object);
                
                var attribs = prop.GetCustomAttributes(false).Select(attrib =>
                {
                    var mockAttrib = new Mock<CodeAttribute2>();
                    mockAttrib.SetupGet(x => x.FullName).Returns(attrib.GetType().FullName);

                    // Can't extract arguments given to attribute instances in general, because that information
                    // doesn't exist at runtime. Handle relevant special cases here.
                    var attribArgs = new List<CodeAttributeArgument>();
                    if (attrib is ColumnAttribute)
                    {
                        var mockArg = new Mock<CodeAttributeArgument>();
                        mockArg.Setup(x => x.Name).Returns("IsPrimaryKey");
                        mockArg.Setup(x => x.Value).Returns(((ColumnAttribute)attrib).IsPrimaryKey.ToString());
                        attribArgs.Add(mockArg.Object);
                    }
                    if (attrib is EdmScalarPropertyAttribute)
                    {
                        var mockArg = new Mock<CodeAttributeArgument>();
                        mockArg.Setup(x => x.Name).Returns("EntityKeyProperty");
                        mockArg.Setup(x => x.Value).Returns(((EdmScalarPropertyAttribute)attrib).EntityKeyProperty.ToString());
                        attribArgs.Add(mockArg.Object);
                    }

                    var attribArgsElements = new Mock<CodeElements>();
                    attribArgsElements.Setup(x => x.GetEnumerator()).Returns(() => attribArgs.GetEnumerator());
                    mockAttrib.SetupGet(x => x.Arguments).Returns(attribArgsElements.Object);

                    return mockAttrib.Object;
                }).ToList();
                var attribsElements = new Mock<CodeElements>();
                attribsElements.Setup(x => x.GetEnumerator()).Returns(() => attribs.GetEnumerator());
                mockCodeProperty.SetupGet(x => x.Attributes).Returns(attribsElements.Object);

                // Recursively mock property types, reusing existing type mocks where available to avoid infinite recursion
                var mockCodeTypeRef = new Mock<CodeTypeRef>();
                if (prop.PropertyType.Assembly == type.Assembly) {
                    var mockPropertyType = mockTypeLocator.Object.FindUniqueType(project, prop.PropertyType.Name);
                    if (mockPropertyType == null)
                        mockPropertyType = (CodeType)BuildMockCodeClass(prop.PropertyType, mockTypeLocator, project);
                    mockCodeTypeRef.Setup(x => x.CodeType).Returns(mockPropertyType);
                    mockCodeTypeRef.Setup(x => x.AsFullName).Returns("not mocked");
                }
                mockCodeProperty.SetupGet(x => x.Type).Returns(mockCodeTypeRef.Object);

                return mockCodeProperty.Object;
            }).ToList();
            var members = new Mock<CodeElements>();
            members.Setup(x => x.GetEnumerator()).Returns(() => properties.GetEnumerator());

            mockCodeClassAsCodeType.SetupGet(x => x.Name).Returns(type.Name);
            mockCodeClassAsCodeType.SetupGet(x => x.Members).Returns(members.Object);
            mockCodeClassAsCodeType.SetupGet(x => x.InfoLocation).Returns(vsCMInfoLocation.vsCMInfoLocationProject);
            mockCodeClassAsCodeType.SetupGet(x => x.Language).Returns(VsConstants.CsharpCodeModelLanguageGuid);

            // No need to mock interfaces yet. Just return an empty collection.
            var interfaces = new Mock<CodeElements>();
            interfaces.Setup(x => x.GetEnumerator()).Returns(() => new List<CodeInterface>().GetEnumerator());
            mockCodeClass.SetupGet(x => x.ImplementedInterfaces).Returns(interfaces.Object);

            var bases = new Mock<CodeElements>();
            var baseCodeTypes = new List<CodeType>();
            if (type.BaseType != null)
                baseCodeTypes.Add((CodeType)BuildMockCodeClass(type.BaseType, mockTypeLocator, project));
            bases.Setup(x => x.GetEnumerator()).Returns(() => baseCodeTypes.GetEnumerator());
            mockCodeClassAsCodeType.SetupGet(x => x.Bases).Returns(bases.Object);

            mockTypeLocator.Setup(x => x.FindUniqueType(project, type.Name)).Returns(mockCodeClassAsCodeType.Object);
            return mockCodeClass.Object;
        }
    }
}