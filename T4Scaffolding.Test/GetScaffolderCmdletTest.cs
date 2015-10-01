using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Core.ScaffolderLocators;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class GetScaffolderCmdletTest
    {
        private readonly ISolutionManager _solutionManager = new MockSolutionManagerBuilder(
            new MockProject("myCsharpProject"),
            new MockProject("myVbProject")
        ) { DefaultProjectName = "myCsharpProject" }.Build();

        [TestMethod]
        public void ShouldReturnResultsFromScaffolderLocator()
        {
            // Arrange
            var mockScaffolderLocator = new Mock<IScaffolderLocator>();
            var expectedResult = new List<ScaffolderInfo> {
                new ScaffolderInfo("name1", "packagename1", "location1", null, null),
                new ScaffolderInfo("name2", "packagename2", "location2", null, null),
            };
            mockScaffolderLocator.Setup(x => x.GetScaffolders(_solutionManager.DefaultProject, "someScaffolder", true)).Returns(expectedResult);

            // Act
            var results = new GetScaffolderCmdlet(_solutionManager, null, mockScaffolderLocator.Object) {
                Name = "someScaffolder"
            }.GetResults<ScaffolderInfo>();

            // Assert
            CollectionAssert.AreEqual(expectedResult, results.ToList());
        }

        [TestMethod]
        public void ShouldBeAbleToSpecifyArbitraryProjectName()
        {
            // Arrange
            var vbProject = _solutionManager.GetProject("myVbProject");
            var mockScaffolderLocator = new Mock<IScaffolderLocator>();
            var expectedResult = new List<ScaffolderInfo> {
                new ScaffolderInfo("name1", "packagename1", "location1", null, null),
                new ScaffolderInfo("name2", "packagename2", "location2", null, null),
            };
            mockScaffolderLocator.Setup(x => x.GetScaffolders(vbProject, "someScaffolder", true)).Returns(expectedResult);

            // Act
            var results = new GetScaffolderCmdlet(_solutionManager, null, mockScaffolderLocator.Object) {
                Name = "someScaffolder",
                Project = vbProject.Name
            }.GetResults<ScaffolderInfo>();

            // Assert
            CollectionAssert.AreEqual(expectedResult, results.ToList());
        }

        [TestMethod]
        public void ShouldIncludeHiddenScaffoldersOnlyWhenExplicitlyRequested()
        {
            // Arrange
            var mockScaffolderLocator = new Mock<IScaffolderLocator>();
            var expectedResult = new List<ScaffolderInfo> {
                new ScaffolderInfo("hiddenScaffolder",  "packagename1", "location1", null, new ScaffolderAttribute { HideInConsole = true }),
                new ScaffolderInfo("visibleScaffolder", "packagename2", "location2", null, new ScaffolderAttribute { HideInConsole = false}),
            };
            mockScaffolderLocator.Setup(x => x.GetScaffolders(_solutionManager.DefaultProject, "someScaffolder", true)).Returns(expectedResult);

            // Act/Assert: Not asking for hidden scaffolders
            var results = new GetScaffolderCmdlet(_solutionManager, null, mockScaffolderLocator.Object) {
                Name = "someScaffolder"
            }.GetResults<ScaffolderInfo>().ToList();
            Assert.AreEqual(1, results.Count);
            Assert.AreSame(expectedResult[1], results.Single());

            // Act/Assert: Asking for hidden scaffolders
            results = new GetScaffolderCmdlet(_solutionManager, null, mockScaffolderLocator.Object) {
                Name = "someScaffolder",
                IncludeHidden = true
            }.GetResults<ScaffolderInfo>().ToList();
            Assert.AreEqual(2, results.Count);
            CollectionAssert.AreEqual(expectedResult, results);
        }
    }
}
