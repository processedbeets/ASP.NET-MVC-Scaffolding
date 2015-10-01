using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Core;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class GetProjectLanguageCmdletTest
    {
        [TestMethod]
        public void ShouldReturnLanguageForKnownProjectTypes()
        {
            var testCases = new Dictionary<string, string> {
                { VsConstants.CsharpProjectTypeGuid, "cs" },
                { VsConstants.VbProjectTypeGuid, "vb" },
            };

            foreach (var testCase in testCases) {
                // Arrange
                var solutionManager = new MockSolutionManagerBuilder(
                    new MockProject("myProj") { Kind = testCase.Key }
                ).Build();

                // Act
                var result = new GetProjectLanguageCmdlet(solutionManager).GetResults<string>();

                // Assert
                Assert.AreEqual(testCase.Value, result.Single());
            }
        }

        [TestMethod]
        public void ShouldReturnNothingForUnknownProjectTypes()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(
                new MockProject("myProj") { Kind = "someUnknownProjectKind" }
            ).Build();

            // Act
            var result = new GetProjectLanguageCmdlet(solutionManager).GetResults<string>();

            // Assert
            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public void ShouldBeAbleToSpecifyArbitraryProjectName()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(
                new MockProject("firstProj") { Kind = VsConstants.CsharpProjectTypeGuid },
                new MockProject("secondProj") { Kind = VsConstants.VbProjectTypeGuid }
            ).Build();

            // Act
            var result = new GetProjectLanguageCmdlet(solutionManager) { Project = "secondProj" }.GetResults<string>();

            // Assert
            Assert.AreEqual("vb", result.Single());
        }
    }
}
