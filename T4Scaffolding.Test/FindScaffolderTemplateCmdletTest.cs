using System;
using System.Linq;
using System.Management.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Core;
using T4Scaffolding.Core.FileSystem;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class FindScaffolderTemplateCmdletTest
    {
        #region Initialization
        [TestInitialize]
        public void Setup()
        {
            _fileSystem = new Mock<IFileSystem>();
            _solutionManager = new Mock<ISolutionManager>();
            new MockSolutionManagerBuilder(_solutionManager,
                 new MockProject("MyCsProject") { Kind = VsConstants.CsharpProjectTypeGuid },
                 new MockProject("MyVbProject") { Kind = VsConstants.VbProjectTypeGuid }
            ) { DefaultProjectName = "MyCsProject" }.Build();
            _cmdlet = new FindScaffolderTemplateCmdlet(_solutionManager.Object, _fileSystem.Object);
        }

        private Mock<IFileSystem> _fileSystem;
        private Mock<ISolutionManager> _solutionManager;
        private FindScaffolderTemplateCmdlet _cmdlet;

        #endregion

        [TestMethod]
        public void ShouldFindTemplateInFirstMatchingFolderOrSubfolder()
        {
            // Arrange
            _fileSystem.Setup(x => x.FindFiles("disk:\\first\\folder", "testTemplate.cs.t4", true)).Returns(new string[] { });
            _fileSystem.Setup(x => x.FindFiles("disk:\\second\\folder", "testTemplate.cs.t4", true)).Returns(new[] { "fullPathOfFoundTemplateInSecondFolder" });
            _fileSystem.Setup(x => x.FindFiles("disk:\\third\\folder", "testTemplate.cs.t4", true)).Returns(new[] { "fullPathOfFoundTemplateInThirdFolder" });

            // Act
            _cmdlet.TemplateFolders = new[] { "disk:\\first\\folder", "disk:\\second\\folder", "disk:\\third\\folder" };
            _cmdlet.Template = "testTemplate";
            var results = _cmdlet.GetResults();

            // Assert
            Assert.AreEqual("fullPathOfFoundTemplateInSecondFolder", results.Single());
        }


        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldThrowErrorIfNoTemplateSourcesProvided()
        {
            _cmdlet.TemplateFolders = new string[] { };
            _cmdlet.Template = "ignored";
            _cmdlet.GetResults();
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldWriteErrorIfNoTemplateMatchesAndErrorIfNotFoundOptionIsSet()
        {
            _cmdlet.TemplateFolders = new[] { "contains:\\no\\templates" };
            _cmdlet.Template = "ignored";
            _cmdlet.ErrorIfNotFound = SwitchParameter.Present;
            _cmdlet.GetResults();
        }

        [TestMethod]
        public void ShouldReturnNothingAndNotWriteErrorIfNoTemplateMatchesAndErrorIfNotFoundOptionIsNotSet()
        {
            _fileSystem.Setup(x => x.FindFiles("contains:\\no\\match", "someTemplate.cs.t4", true)).Returns(new string[] { }).Verifiable();
            _cmdlet.TemplateFolders = new[] { "contains:\\no\\match" };
            _cmdlet.Template = "someTemplate";
            Assert.AreEqual(0, _cmdlet.GetResults().Count());
            _fileSystem.VerifyAll();
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldThrowErrorIfMultipleTemplatesMatch() // Only possible if user passes a wildcard as template name
        {
            // Arrange
            _fileSystem.Setup(x => x.FindFiles("disk:\\some\\folder", "testTemplate.cs.t4", true)).Returns(new[] { "match1", "match2" });

            // Act
            _cmdlet.TemplateFolders = new[] { "disk:\\some\\folder" };
            _cmdlet.Template = "testTemplate";
            _cmdlet.GetResults();
        }

        [TestMethod]
        public void ShouldUseSameCodeLanguageAsSpecifiedProjectIfCodeLanguageNotSpecified()
        {
            // Arrange
            var vbProject = _solutionManager.Object.GetProject("MyVbProject");
            _fileSystem.Setup(x => x.FindFiles("disk:\\some\\folder", "testTemplate.vb.t4", true)).Returns(new[] { "fullPathOfFoundVbTemplate" });

            // Act
            _cmdlet.TemplateFolders = new[] { "disk:\\some\\folder" };
            _cmdlet.Template = "testTemplate";
            _cmdlet.Project = vbProject.Name;
            var results = _cmdlet.GetResults();

            // Assert
            Assert.AreEqual("fullPathOfFoundVbTemplate", results.Single());
        }

        [TestMethod]
        public void ShouldUseSameCodeLanguageAsDefaultProjectIfCodeLanguageAndProjectNotSpecified()
        {
            // Arrange
            _fileSystem.Setup(x => x.FindFiles("disk:\\some\\folder", "testTemplate.cs.t4", true)).Returns(new[] { "fullPathOfFoundCsTemplate" });

            // Act
            _cmdlet.TemplateFolders = new[] { "disk:\\some\\folder" };
            _cmdlet.Template = "testTemplate";
            var results = _cmdlet.GetResults();

            // Assert
            Assert.AreEqual("fullPathOfFoundCsTemplate", results.Single());
        }


        [TestMethod]
        public void ShouldBeAbleToSpecifyArbitraryCodeLanguage()
        {
            _fileSystem.Setup(x => x.FindFiles("disk:\\some\\folder", "testTemplate.arbitraryCodeLanguage.t4", true)).Returns(new[] { "fullPathOfFoundTemplate" });

            // Act
            _cmdlet.TemplateFolders = new[] { "disk:\\some\\folder" };
            _cmdlet.Template = "testTemplate";
            _cmdlet.CodeLanguage = "arbitraryCodeLanguage";
            var results = _cmdlet.GetResults();

            // Assert
            Assert.AreEqual("fullPathOfFoundTemplate", results.Single());
        }
    }
}
