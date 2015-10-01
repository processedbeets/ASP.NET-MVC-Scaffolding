using System.Linq;
using System.Management.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EnvDTE;
using Moq;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Core.FileSystem;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class GetProjectFolderCmdletTest
    {
        [TestMethod]
        public void ShouldReturnFolderItemsIfFolderExists()
        {
            // Try both with and without trailing slash
            foreach (var path in new[] { "someFolder\\childFolder", "someFolder\\childFolder\\" }) {

                // Arrange
                var solutionManager = new MockSolutionManagerBuilder(new MockProject("myProj",
                    new MockFolder("someFolder",
                        new MockFolder("childFolder", new MockItem("someFile"))
                    )
                )).Build();

                // Act
                var results = new GetProjectFolderCmdlet(solutionManager, null) {
                    Path = path
                }.GetResults<ProjectItems>();

                // Assert
                Assert.IsNotNull(results.Single().Item("someFile"));
            }
        }

        [TestMethod]
        public void ShouldBeAbleToSpecifyArbitraryProjectName()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(
                new MockProject("firstProj"),
                new MockProject("secondProj", new MockItem("someFile"))
            ).Build();

            // Act
            var results = new GetProjectFolderCmdlet(solutionManager, null) {
                Path = "", Project = "secondProj"
            }.GetResults<ProjectItems>();

            // Assert
            Assert.IsNotNull(results.Single().Item("someFile"));
        }

        [TestMethod]
        public void ShouldReturnNoItemsIfFolderDoesNotExist()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(new MockProject("myProj",
                new MockFolder("someFolder",
                    new MockFolder("childFolder", new MockItem("someFile"))
                )
            )).Build();

            // Act
            var results = new GetProjectFolderCmdlet(solutionManager, null) {
                Path = "someFolder\\nonExistent\\anotherNonExistent"
            }.GetResults<ProjectItems>();

            // Assert
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public void ShouldCreateFolderChainIfRequested()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(new MockProject("myProj",
                new MockFolder("someFolder")
            ) { RootPath = "proj:\\root" }).Build();
            var mockFileSystem = new Mock<IFileSystem>();

            // Act
            var results = new GetProjectFolderCmdlet(solutionManager, mockFileSystem.Object) {
                Path = "someFolder\\newFolder\\anotherNewFolder",
                Create = new SwitchParameter(true)
            }.GetResults<ProjectItems>();

            // Assert
            Assert.AreEqual(1, results.Count());
            Assert.AreEqual("anotherNewFolder", ((ProjectItem)results.Single().Parent).Name);
            mockFileSystem.Verify(x => x.CreateDirectory("proj:\\root\\someFolder\\newFolder\\anotherNewFolder"));
        }
    }
}
