using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Core.Configuration;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class GetDefaultScaffolderCmdletTest
    {
        private static readonly DefaultScaffolderConfigEntry[] EmptyConfig = new DefaultScaffolderConfigEntry[] { };
        private static readonly DefaultScaffolderConfigEntry[] SampleConfig = new[] {
            new DefaultScaffolderConfigEntry("d1", "s1"),
            new DefaultScaffolderConfigEntry("d3", "s3"),
            new DefaultScaffolderConfigEntry("d2", "s2"),
        };
        private static readonly DefaultScaffolderConfigEntry[] OverridesConfig = new[] {
            new DefaultScaffolderConfigEntry("d2", "s2override"),
            new DefaultScaffolderConfigEntry("someNewKey", "someNewValue"),
        };
        private readonly ISolutionManager _solutionManager = new MockSolutionManagerBuilder(
            new MockProject("myCsharpProject"),
            new MockProject("myVbProject")
        ) { DefaultProjectName = "myCsharpProject" }.Build();

        [TestMethod]
        public void ShouldListAllDefaultsIfNoNameIsSpecified()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            mockConfigStore.Setup(x => x.GetProjectDefaultScaffolders(_solutionManager.DefaultProject)).Returns(EmptyConfig.AsQueryable());
            mockConfigStore.Setup(x => x.GetSolutionDefaultScaffolders()).Returns(SampleConfig.AsQueryable());

            // Act
            var cmdlet = new GetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object);
            var results = cmdlet.GetResults<DefaultScaffolderConfigEntry>();

            // Assert
            CollectionAssert.AreEquivalent(SampleConfig, results.ToList());
        }

        [TestMethod]
        public void ShouldOverlayProjectDefaultsOnTopOfSolutionDefaultsWhenNoNameIsSpecified()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            mockConfigStore.Setup(x => x.GetProjectDefaultScaffolders(_solutionManager.DefaultProject)).Returns(OverridesConfig.AsQueryable());
            mockConfigStore.Setup(x => x.GetSolutionDefaultScaffolders()).Returns(SampleConfig.AsQueryable());

            // Act
            var cmdlet = new GetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object);
            var results = cmdlet.GetResults<DefaultScaffolderConfigEntry>();

            // Assert            
            var resultsDict = results.ToDictionary(x => x.DefaultName, x => x.ScaffolderName);
            Assert.AreEqual(4, resultsDict.Count);
            Assert.AreEqual("s1", resultsDict["d1"]);
            Assert.AreEqual("s2override", resultsDict["d2"]);
            Assert.AreEqual("s3", resultsDict["d3"]);
            Assert.AreEqual("someNewValue", resultsDict["someNewKey"]);
        }

        [TestMethod]
        public void ShouldReturnMatchingEntryFromSolutionIfNameIsSpecifiedAndNotConfiguredForProject()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            mockConfigStore.Setup(x => x.GetProjectDefaultScaffolders(_solutionManager.DefaultProject)).Returns(EmptyConfig.AsQueryable());
            mockConfigStore.Setup(x => x.GetSolutionDefaultScaffolders()).Returns(SampleConfig.AsQueryable());

            // Act
            var cmdlet = new GetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object) { Name = "d2" }; // Case-insensitive
            var results = cmdlet.GetResults<DefaultScaffolderConfigEntry>();

            // Assert
            Assert.AreEqual("d2", results.Single().DefaultName);
            Assert.AreEqual("s2", results.Single().ScaffolderName);
        }

        [TestMethod]
        public void ShouldReturnMatchingEntryFromProjectIfNameIsSpecifiedAndNotConfiguredForProject()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            mockConfigStore.Setup(x => x.GetProjectDefaultScaffolders(_solutionManager.DefaultProject)).Returns(OverridesConfig.AsQueryable());
            mockConfigStore.Setup(x => x.GetSolutionDefaultScaffolders()).Returns(SampleConfig.AsQueryable());

            // Act
            var cmdlet = new GetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object) { Name = "D2" }; // Case-insensitive
            var results = cmdlet.GetResults<DefaultScaffolderConfigEntry>();

            // Assert
            Assert.AreEqual("d2", results.Single().DefaultName);
            Assert.AreEqual("s2override", results.Single().ScaffolderName);
        }

        [TestMethod]
        public void ShouldReturnNothingIfNameDoesNotMatchAnExistingDefault()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            mockConfigStore.Setup(x => x.GetProjectDefaultScaffolders(_solutionManager.DefaultProject)).Returns(OverridesConfig.AsQueryable());
            mockConfigStore.Setup(x => x.GetSolutionDefaultScaffolders()).Returns(SampleConfig.AsQueryable());

            // Act
            var cmdlet = new GetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object) { Name = "NonExistent" }; // Case-insensitive
            var results = cmdlet.GetResults<DefaultScaffolderConfigEntry>();

            // Assert
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public void ShouldBeAbleToSpecifyArbitraryProjectName()
        {
            // Arrange
            var vbProject = _solutionManager.GetProject("myVbProject");
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            mockConfigStore.Setup(x => x.GetProjectDefaultScaffolders(vbProject)).Returns(SampleConfig.AsQueryable());

            // Act
            var cmdlet = new GetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object) {
                Project = vbProject.Name
            };
            var results = cmdlet.GetResults<DefaultScaffolderConfigEntry>();

            // Assert
            CollectionAssert.AreEquivalent(SampleConfig, results.ToList());
        }
    }
}
