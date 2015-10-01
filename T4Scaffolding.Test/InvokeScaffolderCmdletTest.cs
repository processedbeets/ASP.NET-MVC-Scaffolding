using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Core;
using T4Scaffolding.Core.CommandInvokers;
using T4Scaffolding.Core.ScaffolderLocators;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class InvokeScaffolderCmdletTest
    {
        #region Initialization
        private static Runspace _psRunspace;
        [ClassInitialize] public static void CreatePsRunspace(TestContext context) { _psRunspace = RunspaceFactory.CreateRunspace(); _psRunspace.Open(); }
        [ClassCleanup] public static void DestroyPsRunspace() { _psRunspace.Dispose(); }

        [TestInitialize]
        public void Setup()
        {
            _solutionManager = new Mock<ISolutionManager>();
            _scaffolderLocator = new Mock<IScaffolderLocator>();
            _commandInvoker = new Mock<IPowershellCommandInvoker>();
            new MockSolutionManagerBuilder(_solutionManager,
                 new MockProject("MyCsProject") { Kind = VsConstants.CsharpProjectTypeGuid },
                 new MockProject("MyVbProject") { Kind = VsConstants.VbProjectTypeGuid }
            ) { DefaultProjectName = "MyCsProject" }.Build();
            _cmdlet = new InvokeScaffolderCmdlet(_solutionManager.Object, null, _scaffolderLocator.Object, _commandInvoker.Object);
        }

        private Mock<ISolutionManager> _solutionManager;
        private Mock<IScaffolderLocator> _scaffolderLocator;
        private Mock<IPowershellCommandInvoker> _commandInvoker;
        private InvokeScaffolderCmdlet _cmdlet;

        #endregion

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldRequireAValueForScaffolder()
        {
            _cmdlet.GetResults();
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldThrowErrorIfNoScaffoldersMatch()
        {
            _scaffolderLocator.Setup(x => x.GetScaffolders(_solutionManager.Object.DefaultProject, "someScaffolderName", true)).Returns(Enumerable.Empty<ScaffolderInfo>());
            _cmdlet.Scaffolder = "someScaffolderName";
            _cmdlet.GetResults();
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void ShouldThrowErrorIfMultipleScaffoldersMatch()
        {
            _scaffolderLocator.Setup(x => x.GetScaffolders(_solutionManager.Object.DefaultProject, "someScaffolderName", true)).Returns(new[] {
                new ScaffolderInfo("name1", "packagename1", "location1", null, null),
                new ScaffolderInfo("name2", "packagename2", "location2", null, null),
            });
            _cmdlet.Scaffolder = "someScaffolderName";
            _cmdlet.GetResults();
        }

        [TestMethod]
        public void ShouldInvokeScaffolderPassingCustomParametersIgnoringMismatchingParameters()
        {
            // Arrange / Act
            var boundParameters = new Dictionary<string, object> {
                { "SomeCustomParam", "myCustomParamValue" },
                { "SomeCustomSwitch", SwitchParameter.Present },
            };
            var passedParams = RunBasicInvokeScaffolderTest(boundParameters);

            // Assert
            Assert.AreEqual("myCustomParamValue", passedParams["SomeCustomParam"]);
            Assert.AreEqual(SwitchParameter.Present, passedParams["SomeCustomSwitch"]);
        }

        [TestMethod]
        public void ShouldPassScaffolderTemplateOverridesFolderPlusScaffolderLocationAsTemplateFoldersIfAcceptedByScaffolder()
        {
            // Arrange
            new MockSolutionManagerBuilder(_solutionManager,
                 new MockProject("MyCsProject", 
                     new MockFolder("CodeTemplates", new MockFolder("Scaffolders", new MockFolder("foundScaffolder")))
                 ) { RootPath = "z:\\proj", Kind = VsConstants.CsharpProjectTypeGuid }
            ).Build();
            var passedParams = RunBasicInvokeScaffolderTest(new Dictionary<string, object>());

            // Assert
            var templateFoldersParam = (string[])passedParams["TemplateFolders"];
            Assert.AreEqual(2, templateFoldersParam.Length);
            Assert.AreEqual("z:\\proj\\CodeTemplates\\Scaffolders\\foundScaffolder", templateFoldersParam[0]);
            Assert.AreEqual("g:\\folder\\subfolder", templateFoldersParam[1]);
        }

        [TestMethod]
        public void ShouldPassAnySuppliedTemplateOverrideFoldersInPreferenceToOtherLocations()
        {
            // Arrange / Act
            _cmdlet.OverrideTemplateFolders = new object[] { "disk:\\firstOverride", "disk:\\secondOverride", "disk:\\thirdOverride" };
            var passedParams = RunBasicInvokeScaffolderTest(new Dictionary<string, object>());

            // Assert
            var templateFoldersParam = (string[])passedParams["TemplateFolders"];
            Assert.IsTrue(templateFoldersParam.Length > 3);
            Assert.AreEqual("disk:\\firstOverride", templateFoldersParam[0]);
            Assert.AreEqual("disk:\\secondOverride", templateFoldersParam[1]);
            Assert.AreEqual("disk:\\thirdOverride", templateFoldersParam[2]);
        }

        [TestMethod]
        public void ShouldBeAbleToSpecifyArbitraryProject()
        {
            // Arrange
            var vbProject = _solutionManager.Object.GetProject("MyVbProject");
            var scriptInfo = ExampleScripts.MakeExternalScriptInfo(_psRunspace, "simpleScaffolder");
            _scaffolderLocator.Setup(x => x.GetScaffolders(vbProject, "someScaffolderName", true)).Returns(new[] {
                new ScaffolderInfo("foundScaffolderInVbProject", "", "g:\\folder\\subfolder\\file.ps1", scriptInfo, null),
            });

            // Act
            _cmdlet.Project = vbProject.Name;
            RunBasicInvokeScaffolderTest(new Dictionary<string, object>());

            // Assert
            _commandInvoker.Verify(x => x.InvokePipeToOutput(scriptInfo, It.IsAny<Hashtable>(), It.IsAny<PipelineResultTypes>()));
        }

        private Hashtable RunBasicInvokeScaffolderTest(Dictionary<string, object> boundParameters)
        {
            var scaffolderScriptInfo = ExampleScripts.MakeExternalScriptInfo(_psRunspace, "scaffolderThatAcceptsParameters");
            _scaffolderLocator.Setup(x => x.GetScaffolders(_solutionManager.Object.DefaultProject, "someScaffolderName", true)).Returns(new[] {
                new ScaffolderInfo("foundScaffolder", "", "g:\\folder\\subfolder\\file.ps1", scaffolderScriptInfo, null),
            });
            _commandInvoker.SetupGet(x => x.BoundParameters).Returns(boundParameters);
            Hashtable passedParams = null;
            _commandInvoker.Setup(x => x.InvokePipeToOutput(scaffolderScriptInfo, It.IsAny<Hashtable>(), It.IsAny<PipelineResultTypes>()))
                .Callback<CommandInfo, Hashtable, PipelineResultTypes>((c,p,r) => { passedParams = p; });

            // Act
            _cmdlet.Scaffolder = "someScaffolderName";
            _cmdlet.GetResults();
            return passedParams;
        }
    }
}