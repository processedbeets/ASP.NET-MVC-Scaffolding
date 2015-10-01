using System;
using System.Linq;
using System.Management.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Core.Configuration;
using T4Scaffolding.Core.ScaffolderLocators;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class SetDefaultScaffolderCmdletTest
    {
        private readonly ISolutionManager _solutionManager = new MockSolutionManagerBuilder(
            new MockProject("myCsharpProject"),
            new MockProject("myVbProject")
        ) { DefaultProjectName = "myCsharpProject" }.Build();

        [TestMethod]
        public void ShouldWriteNewSettingsToTheConfigProviderAndReturnNothing()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            var mockScaffolderLocator = new Mock<IScaffolderLocator>();
            mockScaffolderLocator.Setup(x => x.GetScaffolders(_solutionManager.DefaultProject, "someScaffolder", false)).Returns(new[] {
                new ScaffolderInfo("foundScaffolderName", null, null, null, null)
            });

            // Act
            var results = new SetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object, mockScaffolderLocator.Object) {
                Name = "someDefault",
                Scaffolder = "someScaffolder"
            }.GetResults<DefaultScaffolderConfigEntry>();

            // Assert
            Assert.AreEqual(0, results.Count());
            mockConfigStore.Verify(x => x.SetProjectDefaultScaffolder(_solutionManager.DefaultProject, "someDefault", "foundScaffolderName", false));
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldRejectNonExistentScaffolders()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            var mockScaffolderLocator = new Mock<IScaffolderLocator>();

            // Act / Assert
            new SetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object, mockScaffolderLocator.Object) {
                Name = "someDefault",
                Scaffolder = "nonExistentScaffolder"
            }.GetResults<DefaultScaffolderConfigEntry>();
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldRejectAmbiguousScaffolderNames()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            var mockScaffolderLocator = new Mock<IScaffolderLocator>();
            mockScaffolderLocator.Setup(x => x.GetScaffolders(_solutionManager.DefaultProject, "ambiguousSearchString", false)).Returns(new[] {
                new ScaffolderInfo("firstMatch", null, null, null, null),
                new ScaffolderInfo("secondMatch", null, null, null, null)
            });

            // Act / Assert
            new SetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object, mockScaffolderLocator.Object) {
                Name = "doesNotMatter",
                Scaffolder = "ambiguousSearchString"
            }.GetResults<DefaultScaffolderConfigEntry>();
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldRejectDefaultNamesThatMatchActualScaffolderNames()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            var mockScaffolderLocator = new Mock<IScaffolderLocator>();
            mockScaffolderLocator.Setup(x => x.GetScaffolders(_solutionManager.DefaultProject, "actualScaffolderName", false)).Returns(new[] {
                new ScaffolderInfo("actualScaffolderName", null, null, null, null),
            });

            // Act / Assert
            new SetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object, mockScaffolderLocator.Object) {
                Name = "actualScaffolderName", // This default name clashes with an actual scaffolder name, which shouldn't be allowed
                Scaffolder = "doesNotMatter"
            }.GetResults<DefaultScaffolderConfigEntry>();
        }

        [TestMethod]
        public void ShouldBeAbleToWriteConfigToArbitraryProject()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            var mockScaffolderLocator = new Mock<IScaffolderLocator>();
            var vbProject = _solutionManager.GetProject("myVbProject");
            mockScaffolderLocator.Setup(x => x.GetScaffolders(vbProject, "someScaffolder", false)).Returns(new[] {
                new ScaffolderInfo("foundScaffolderName", null, null, null, null)
            });

            // Act
            var results = new SetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object, mockScaffolderLocator.Object) {
                Name = "someDefault",
                Scaffolder = "someScaffolder",
                Project = vbProject.Name
            }.GetResults<DefaultScaffolderConfigEntry>();

            // Assert
            Assert.AreEqual(0, results.Count());
            mockConfigStore.Verify(x => x.SetProjectDefaultScaffolder(vbProject, "someDefault", "foundScaffolderName", false));
        }

        [TestMethod]
        public void ShouldBeAbleToWriteSolutionWideSettings()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            var mockScaffolderLocator = new Mock<IScaffolderLocator>();
            mockScaffolderLocator.Setup(x => x.GetScaffolders(null, "someScaffolder", false)).Returns(new[] {
                new ScaffolderInfo("foundScaffolderName", null, null, null, null)
            });

            // Act
            var results = new SetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object, mockScaffolderLocator.Object)
            {
                Name = "someDefault",
                Scaffolder = "someScaffolder",
                SolutionWide = SwitchParameter.Present,
                DoNotOverwriteExistingSetting = SwitchParameter.Present
            }.GetResults<DefaultScaffolderConfigEntry>();

            // Assert
            Assert.AreEqual(0, results.Count());
            mockConfigStore.Verify(x => x.SetSolutionDefaultScaffolder("someDefault", "foundScaffolderName", true));
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldNotBeAllowedToSpecifyAProjectWhenWritingSolutionWideSettings()
        {
            // Arrange
            var mockConfigStore = new Mock<IScaffoldingConfigStore>();
            var mockScaffolderLocator = new Mock<IScaffolderLocator>();
            mockScaffolderLocator.Setup(x => x.GetScaffolders(null, "someScaffolder", false)).Returns(new[] {
                new ScaffolderInfo("foundScaffolderName", null, null, null, null)
            });

            // Act
            new SetDefaultScaffolderCmdlet(_solutionManager, null, mockConfigStore.Object, mockScaffolderLocator.Object)
            {
                Name = "someDefault",
                Scaffolder = "someScaffolder",
                SolutionWide = SwitchParameter.Present,
                Project = "anyProject"
            }.GetResults<DefaultScaffolderConfigEntry>();
        }
    }
}
