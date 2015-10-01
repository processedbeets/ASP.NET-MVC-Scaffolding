using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Linq.Mapping;
using System.Data.Objects.DataClasses;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Core.ProjectTypeLocators;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.Test.TestUtils;
using System.Linq;
using Moq;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class GetPrimaryKeyCmdletTest
    {
        #region Initialization
        [TestInitialize]
        public void Setup()
        {
            _projectTypeLocator = new Mock<IProjectTypeLocator>();
            _solutionManager = new Mock<ISolutionManager>();
            new MockSolutionManagerBuilder(_solutionManager, new MockProject("MyProject")).Build();
            _cmdlet = new GetPrimaryKeyCmdlet(_solutionManager.Object, _projectTypeLocator.Object);
        }

        private GetPrimaryKeyCmdlet _cmdlet;
        private Mock<IProjectTypeLocator> _projectTypeLocator;
        private Mock<ISolutionManager> _solutionManager;

        #endregion

        private class TypeWithNoPrimaryKeys {
            public int IrrelevantId { get; set; }
            public string SomeProperty { get; set; }
        }
        [TestMethod]
        public void ShouldReturnNothingIfTypeHasNoPrimaryKeys()
        {
            _cmdlet.Type = MockCodeClass<TypeWithNoPrimaryKeys>();
            Assert.AreEqual(0, _cmdlet.GetResults().Count());
        }
        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldThrowErrorIfTypeHasNoPrimaryKeysAndErrorIfNotFoundIsSet()
        {
            _cmdlet.Type = MockCodeClass<TypeWithNoPrimaryKeys>();
            _cmdlet.ErrorIfNotFound = true;
            _cmdlet.GetResults();
        }

        private class TypeWithTwoPrimaryKeys {
            public int Id { get; set; }
            public Guid TypeWithTwoPrimaryKeysId { get; set; }
        }
        [TestMethod]
        public void ShouldReturnNothingIfTypeHasMultiplePrimaryKeys()
        {
            _cmdlet.Type = MockCodeClass<TypeWithTwoPrimaryKeys>();
            Assert.AreEqual(0, _cmdlet.GetResults().Count());
        }
        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldThrowErrorIfTypeHasNoMultipleKeysAndErrorIfNotFoundIsSet()
        {
            _cmdlet.Type = MockCodeClass<TypeWithTwoPrimaryKeys>();
            _cmdlet.ErrorIfNotFound = true;
            _cmdlet.GetResults();
        }

        private class TypeWithIdProperty {
            public int Id { get; set; }
            public string SomeOtherProperty { get; set; }
        }
        [TestMethod]
        public void ShouldFindPrimaryKeyCalledId()
        {
            _cmdlet.Type = MockCodeClass<TypeWithIdProperty>();
            Assert.AreEqual("Id", _cmdlet.GetResults().Single());
        }

        private class TypeWithTypeNameIdProperty {
            public int TypeWithTypeNameIdPropertyId { get; set; }
            public string SomeOtherProperty { get; set; }
        }
        [TestMethod]
        public void ShouldFindPrimaryKeyCalledTypeNameId()
        {
            _cmdlet.Type = MockCodeClass<TypeWithTypeNameIdProperty>();
            Assert.AreEqual("TypeWithTypeNameIdPropertyId", _cmdlet.GetResults().Single());
        }

        private class TypeWithKeyAttributeProperty {
            [Key] public int MyKeyProperty { get; set; }
            public string SomeOtherProperty { get; set; }
        }
        [TestMethod]
        public void ShouldFindPrimaryKeyIdentifiedByKeyAttribute()
        {
            _cmdlet.Type = MockCodeClass<TypeWithKeyAttributeProperty>();
            Assert.AreEqual("MyKeyProperty", _cmdlet.GetResults().Single());
        }

        private class TypeWithEdmScalarPropertyAttributeProperty {
            [EdmScalarProperty] public int ShouldNotMatchThis { get; set; }
            [EdmScalarProperty(EntityKeyProperty = true)] public int ExpectedPrimaryKeyName { get; set; }
            public string SomeOtherProperty { get; set; }
        }
        [TestMethod]
        public void ShouldFindPrimaryKeyIdentifiedByEdmScalarPropertyAttribute()
        {
            _cmdlet.Type = MockCodeClass<TypeWithEdmScalarPropertyAttributeProperty>();
            Assert.AreEqual("ExpectedPrimaryKeyName", _cmdlet.GetResults().Single());
        }

        private class TypeWithColumnAttributeProperty {
            [Column] public int MyOtherColumnProperty { get; set; }
            [Column(IsPrimaryKey = true)] public int MyPrimaryColumnProperty { get; set; }
            public string SomeOtherProperty { get; set; }
        }
        [TestMethod]
        public void ShouldFindPrimaryKeyIdentifiedByColumnAttribute()
        {
            _cmdlet.Type = MockCodeClass<TypeWithColumnAttributeProperty>();
            Assert.AreEqual("MyPrimaryColumnProperty", _cmdlet.GetResults().Single());
        }

        private class TypeWithIdInBaseBaseBase
        {
            public int TypeWithIdInBaseBaseBaseId { get; set; }
        }
        private class TypeWithIdInBaseBase : TypeWithIdInBaseBaseBase
        {
        }
        private class TypeWithIdInBase : TypeWithIdInBaseBase
        {
        }
        [TestMethod]
        public void ShouldFindPrimaryKeyFromInheritanceChain()
        {
            _cmdlet.Type = MockCodeClass<TypeWithIdInBase>();
            Assert.AreEqual("TypeWithIdInBaseBaseBaseId", _cmdlet.GetResults().Single());
        }

        public string MockCodeClass<T>()
        {
            return MockCodeClassBuilder.BuildMockCodeClass(typeof(T), _projectTypeLocator, _solutionManager.Object.DefaultProject).Name;
        }
    }
}
