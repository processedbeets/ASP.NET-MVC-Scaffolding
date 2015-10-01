using System.ComponentModel.DataAnnotations;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Core.ProjectTypeLocators;
using T4Scaffolding.Core.RelatedEntityLocators;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class GetRelatedEntitiesCmdletTest
    {
        #region Initialization
        [TestInitialize]
        public void Setup()
        {
            _projectTypeLocator = new Mock<IProjectTypeLocator>();
            _solutionManager = new Mock<ISolutionManager>();
            new MockSolutionManagerBuilder(_solutionManager, new MockProject("MyProject"), new MockProject("MyOtherProject")).Build();
            _cmdlet = new GetRelatedEntitiesCmdlet(_solutionManager.Object, _projectTypeLocator.Object);

            // Types that we can find by default
            MockCodeClass<AnotherTypeWithSinglePrimaryKey>();
            MockCodeClass<AnotherTypeWithNoPrimaryKey>();
            MockCodeClass<AnotherTypeWithTwoPrimaryKeys>();
        }

        private GetRelatedEntitiesCmdlet _cmdlet;
        private Mock<IProjectTypeLocator> _projectTypeLocator;
        private Mock<ISolutionManager> _solutionManager;

        #endregion

        private class AnotherTypeWithNoPrimaryKey
        {
            public string UnrelatedProperty { get; set; }
        }
        private class AnotherTypeWithSinglePrimaryKey
        {
            [Key]
            public int ArbitrarilyNamedPrimaryKey { get; set; }
            public string UnrelatedProperty { get; set; }
        }
        private class AnotherTypeWithTwoPrimaryKeys
        {
            [Key]
            public int ArbitrarilyNamedPrimaryKey { get; set; }
            public int ID { get; set; }
        }

        private class TypeWithNoRelations
        {
            public string Name { get; set; }
        }
        [TestMethod]
        public void ShouldReturnEmptyIfTypeHasNoRelations()
        {
            _cmdlet.Type = MockCodeClass<TypeWithNoRelations>().Name;
            Assert.AreEqual(0, _cmdlet.GetResults().Count());
        }

        private class TypeWithUnconnectedIDProperty
        {
            public string UnrelatedID { get; set; }
        }
        [TestMethod]
        public void ShouldIgnoreIDPropertiesThatDoNotCorrespondToTypes()
        {
            _cmdlet.Type = MockCodeClass<TypeWithUnconnectedIDProperty>().Name;
            Assert.AreEqual(0, _cmdlet.GetResults().Count());
        }

        private class TypeWithIDPropertyReferencingTypeWithNoPrimaryKey
        {
            public string AnotherTypeWithNoPrimaryKeyID { get; set; }
        }
        [TestMethod]
        public void ShouldIgnoreIDPropertiesThatDoNotCorrespondToTypesWithPrimaryKeys()
        {
            _cmdlet.Type = MockCodeClass<TypeWithIDPropertyReferencingTypeWithNoPrimaryKey>().Name;
            Assert.AreEqual(0, _cmdlet.GetResults().Count());
        }

        private class TypeWithIDPropertyReferencingTypeWithMultiplePrimaryKeys
        {
            public string AnotherTypeWithTwoPrimaryKeysID { get; set; }
        }
        [TestMethod]
        public void ShouldIgnoreIDPropertiesThatCorrespondToTypesWithMultiplePrimaryKeys()
        {
            _cmdlet.Type = MockCodeClass<TypeWithIDPropertyReferencingTypeWithMultiplePrimaryKeys>().Name;
            Assert.AreEqual(0, _cmdlet.GetResults().Count());
        }

        private class TypeWithSelfReferencingPrimaryKey
        {
            public int TypeWithSelfReferencingPrimaryKeyID { get; set; }
        }
        [TestMethod]
        public void ShouldIgnoreIDPropertiesThatAreThePrimaryKeyForThisEntity() // i.e., don't regard entities as being related to themselves just because Foo has a FooID property
        {
            _cmdlet.Type = MockCodeClass<TypeWithSelfReferencingPrimaryKey>().Name;
            Assert.AreEqual(0, _cmdlet.GetResults().Count());
        }

        private class TypeWithSingleRelation
        {
            public string ID { get; set; }
            public int AnotherTypeWithSinglePrimaryKeyID { get; set; }
        }
        [TestMethod]
        public void ShouldRecognizeIDPropertiesThatCorrespondToTypesWithPrimaryKeys()
        {
            _cmdlet.Type = MockCodeClass<TypeWithSingleRelation>().Name;
            var relatedEntityInfo = _cmdlet.GetResults<RelatedEntityInfo>().Single();
            Assert.AreEqual("AnotherTypeWithSinglePrimaryKey", relatedEntityInfo.RelationName);
            Assert.AreEqual("AnotherTypeWithSinglePrimaryKeyID", relatedEntityInfo.RelationProperty.Name);
            Assert.AreEqual("AnotherTypeWithSinglePrimaryKey", relatedEntityInfo.RelatedEntityType.Name);
            Assert.AreEqual("ArbitrarilyNamedPrimaryKey", relatedEntityInfo.RelatedEntityPrimaryKeyName);
        }

        private class TypeWithIDPropertyCorrespondingToMultipleEntityTypes
        {
            public int SomeTypeID { get; set; }
        }
        [TestMethod]
        public void ShouldIgnoreIDPropertiesThatCorrespondToMultiplePlausibleEntities()
        {
            _cmdlet.Type = MockCodeClass<TypeWithIDPropertyCorrespondingToMultipleEntityTypes>().Name;
            var firstEntity = MockCodeClass<AnotherTypeWithSinglePrimaryKey>();
            var secondEntity = MockCodeClass<AnotherTypeWithSinglePrimaryKey>();
            _projectTypeLocator.Setup(x => x.FindTypes(_solutionManager.Object.DefaultProject, "SomeType")).Returns(new[] { (CodeType)firstEntity, (CodeType)secondEntity });
            Assert.AreEqual(0, _cmdlet.GetResults().Count());
        }

        [TestMethod]
        public void ShouldResolveRelatedTypeAmbiguityByPickingTheTypeWithAPrimaryKey()
        {
            _cmdlet.Type = MockCodeClass<TypeWithIDPropertyCorrespondingToMultipleEntityTypes>().Name;
            var firstEntity = MockCodeClass<AnotherTypeWithNoPrimaryKey>();
            var secondEntity = MockCodeClass<AnotherTypeWithSinglePrimaryKey>();
            _projectTypeLocator.Setup(x => x.FindTypes(_solutionManager.Object.DefaultProject, "SomeType")).Returns(new[] { (CodeType)firstEntity, (CodeType)secondEntity });
            
            var relatedEntityInfo = _cmdlet.GetResults<RelatedEntityInfo>().Single();
            Assert.AreEqual("SomeType", relatedEntityInfo.RelationName);
            Assert.AreEqual("SomeTypeID", relatedEntityInfo.RelationProperty.Name);
            Assert.AreEqual("AnotherTypeWithSinglePrimaryKey", relatedEntityInfo.RelatedEntityType.Name);
            Assert.AreEqual("ArbitrarilyNamedPrimaryKey", relatedEntityInfo.RelatedEntityPrimaryKeyName);
        }

        private class SomePlausibleEntity
        {
            public int SomePlausibleEntityID { get; set; }
        }
        private class TypeWithMultipleRelations
        {
            public int ID { get; set; }

            public int FirstRelationID { get; set; }
            public AnotherTypeWithSinglePrimaryKey FirstRelation { get; set; }

            public int SecondRelationID { get; set; }
            public SomePlausibleEntity SecondRelation { get; set; }
        }
        [TestMethod]
        public void ShouldBeAbleToReturnMultipleRelations()
        {
            _cmdlet.Type = MockCodeClass<TypeWithMultipleRelations>().Name;
            MockCodeClass<SomePlausibleEntity>();

            var relatedEntityInfo = _cmdlet.GetResults<RelatedEntityInfo>().ToList();
            Assert.AreEqual(2, relatedEntityInfo.Count);

            Assert.AreEqual("FirstRelation", relatedEntityInfo[0].RelationName);
            Assert.AreEqual("FirstRelationID", relatedEntityInfo[0].RelationProperty.Name);
            Assert.AreEqual("AnotherTypeWithSinglePrimaryKey", relatedEntityInfo[0].RelatedEntityType.Name);
            Assert.AreEqual("ArbitrarilyNamedPrimaryKey", relatedEntityInfo[0].RelatedEntityPrimaryKeyName);

            Assert.AreEqual("SecondRelation", relatedEntityInfo[1].RelationName);
            Assert.AreEqual("SecondRelationID", relatedEntityInfo[1].RelationProperty.Name);
            Assert.AreEqual("SomePlausibleEntity", relatedEntityInfo[1].RelatedEntityType.Name);
            Assert.AreEqual("SomePlausibleEntityID", relatedEntityInfo[1].RelatedEntityPrimaryKeyName);
        }

        public class TypeInMyOtherProjectWithRelatedEntity
        {
            public int SomethingID { get; set; }
            public EntityInMyOtherProject Something { get; set; }
        }
        public class EntityInMyOtherProject
        {
            public int ID { get; set; }
        }
        [TestMethod]
        public void ShouldBeAbleToSpecifyAnArbitraryProject()
        {
            var myOtherProject = _solutionManager.Object.GetProject("MyOtherProject");
            MockCodeClass<EntityInMyOtherProject>(myOtherProject);
            _cmdlet.Project = myOtherProject.Name;
            _cmdlet.Type = MockCodeClass<TypeInMyOtherProjectWithRelatedEntity>(myOtherProject).Name;

            var relatedEntityInfo = _cmdlet.GetResults<RelatedEntityInfo>().Single();
            Assert.AreEqual("Something", relatedEntityInfo.RelationName);
            Assert.AreEqual("SomethingID", relatedEntityInfo.RelationProperty.Name);
            Assert.AreEqual("EntityInMyOtherProject", relatedEntityInfo.RelatedEntityType.Name);
            Assert.AreEqual("ID", relatedEntityInfo.RelatedEntityPrimaryKeyName);
        }

        public CodeClass MockCodeClass<T>(Project project = null)
        {
            if (project == null)
                project = _solutionManager.Object.DefaultProject;
            var mockCodeClass = MockCodeClassBuilder.BuildMockCodeClass(typeof(T), _projectTypeLocator, project);
            _projectTypeLocator.Setup(x => x.FindTypes(project, mockCodeClass.Name)).Returns(new[] { (CodeType)mockCodeClass });
            return mockCodeClass;
        }
    }
}
