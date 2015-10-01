using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EnvDTE;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class GetProjectItemCmdletTest
    {
        [TestMethod]
        public void ShouldReturnExistingItem()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(new MockProject("myProj",
                new MockFolder("someFolder",
                    new MockFolder("childFolder", new MockItem("someFile.ext"))
                )
            )).Build();

            // Act
            var results = new GetProjectItemCmdlet(solutionManager) {
                Path = "someFolder\\childFolder\\someFile.ext"
            }.GetResults<ProjectItem>();

            // Assert
            Assert.AreEqual("someFile.ext", results.Single().Name);
        }

        [TestMethod]
        public void ShouldReturnNothingIfFileNotFound()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(new MockProject("myProj",
                new MockFolder("someFolder", new MockItem("someFile.ext"))
            )).Build();

            // Act
            var results = new GetProjectItemCmdlet(solutionManager) {
                Path = "someFolder\\nonExistent.ext"
            }.GetResults<ProjectItem>();

            // Assert
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public void ShouldReturnNothingIfFolderNotFound()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(new MockProject("myProj",
                new MockFolder("someFolder", new MockItem("someFile.ext"))
            )).Build();

            // Act
            var results = new GetProjectItemCmdlet(solutionManager) {
                Path = "nonExistent\\someFile.ext"
            }.GetResults<ProjectItem>();

            // Assert
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public void ShouldBeAbleToSpecifyArbitraryProjectName()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(
                new MockProject("firstProj"),
                new MockProject("secondProj", new MockItem("someFile.ext"))
            ).Build();

            // Act
            var results = new GetProjectItemCmdlet(solutionManager) {
                Path = "someFile.ext",
                Project = "secondProj"
            }.GetResults<ProjectItem>();

            // Assert
            Assert.AreEqual("someFile.ext", results.Single().Name);
        }
    }
}
