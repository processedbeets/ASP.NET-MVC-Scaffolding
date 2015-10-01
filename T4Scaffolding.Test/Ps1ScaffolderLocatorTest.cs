using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using EnvDTE;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using T4Scaffolding.Core.CommandInvokers;
using T4Scaffolding.Core.Configuration;
using T4Scaffolding.Core.FileSystem;
using T4Scaffolding.Core.ScaffolderLocators;
using T4Scaffolding.NuGetServices.Services;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class Ps1ScaffolderLocatorTest
    {
        #region Initialization
        private static Runspace _psRunspace;
        [ClassInitialize] public static void CreatePsRunspace(TestContext context) { _psRunspace = RunspaceFactory.CreateRunspace(); _psRunspace.Open(); }
        [ClassCleanup] public static void DestroyPsRunspace() { _psRunspace.Dispose(); }

        [TestInitialize] public void Setup()
        {
            _packageManager = new Mock<IPackageManager>();
            _packagePathResolver = new Mock<IPackagePathResolver>();
            _fileSystem = new Mock<IFileSystem>();
            _commandInvoker = new Mock<IPowershellCommandInvoker>();   
            _configStore = new Mock<IScaffoldingConfigStore>();
            _solutionManager = new Mock<ISolutionManager>();
            new MockSolutionManagerBuilder(_solutionManager, new MockProject("DummyProject")).Build();
            _project = _solutionManager.Object.DefaultProject;
            _scaffolderLocator = new Ps1ScaffolderLocator(_commandInvoker.Object, _packageManager.Object, _packagePathResolver.Object, _fileSystem.Object, _configStore.Object);
        }

        private Mock<IPackageManager> _packageManager;
        private Mock<IPackagePathResolver> _packagePathResolver;
        private Mock<IFileSystem> _fileSystem;
        private Mock<IPowershellCommandInvoker> _commandInvoker;
        private Mock<IScaffoldingConfigStore> _configStore;
        private Ps1ScaffolderLocator _scaffolderLocator;
        private Mock<ISolutionManager> _solutionManager;
        private Project _project;

        #endregion

        [TestMethod]
        public void ShouldFindAnnotatedPs1ScriptsBelowAnyPackageToolsFolderOrProjectScaffoldersFolder()
        {
            // Arrange
            var mockPackage1 = AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "package1", "1.0", "*.ps1", new Dictionary<string, ExternalScriptInfo> {
                { "someScript.ps1", MakeExternalScriptInfo("simpleScaffolder") },
                { "subfolder\\another.ps1", MakeExternalScriptInfo("simpleScaffolder") },
            });
            var mockPackage2 = AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "package2", "1.0", "*.ps1", new Dictionary<string, ExternalScriptInfo> {
                /* No scaffolders in this package */
            });
            var mockPackage3 = AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "package3", "1.0", "*.ps1", new Dictionary<string, ExternalScriptInfo> {
                { "shouldNotFindThis.ps1", MakeExternalScriptInfo("notAScaffolder") },
                { "butShouldFindThis.ps1", MakeExternalScriptInfo("simpleScaffolder") },
            });
            _packageManager.Setup(x => x.LocalRepository.GetPackages()).Returns(new [] {
                mockPackage1, mockPackage2, mockPackage3
            }.AsQueryable());

            // Arrange: VS project containing a mix of valid scaffolders, scaffolder files on disk not referenced by project, and invalid scaffolder files
            new MockSolutionManagerBuilder(_solutionManager,
                new MockProject(new MockFolder("CodeTemplates",
                    new MockFolder("Scaffolders",
                        new MockItem("customScaffolderInRootFolder.ps1"),
                        new MockItem("ps1ReferencedByProjectButIsNotScaffolder.ps1"),
                        new MockFolder("subfolder",
                            new MockFolder("subsubfolder",
                                new MockItem("customScaffolderInSubsubfolder.ps1")
                            )
                        )
                    )
                )) { RootPath = "z:\\myproj" }
            ).Build();
            AddCustomScaffolders("z:\\myproj\\CodeTemplates\\Scaffolders", "*.ps1", new Dictionary<string, string> {
                { "customScaffolderInRootFolder.ps1", "simpleScaffolder" },
                { "ps1ReferencedByProjectButIsNotScaffolder.ps1", "notAScaffolder" },
                { "ps1NotReferencedByProjectButIsScaffolder.ps1", "simpleScaffolder" },
                { "subfolder\\subsubfolder\\customScaffolderInSubsubfolder.ps1", "simpleScaffolder" },
            });

            // Act
            var results = _scaffolderLocator.GetScaffolders(_solutionManager.Object.DefaultProject, null, false).ToList();
            
            // Assert
            Assert.AreEqual(5, results.Count);
            Assert.AreEqual("someScript", results[0].Name);
            Assert.AreEqual("package1 1.0", results[0].PackageName);

            Assert.AreEqual("another", results[1].Name);
            Assert.AreEqual("package1 1.0", results[1].PackageName);

            Assert.AreEqual("butShouldFindThis", results[2].Name);
            Assert.AreEqual("package3 1.0", results[2].PackageName);

            Assert.AreEqual("customScaffolderInRootFolder", results[3].Name);
            Assert.IsNull(results[3].PackageName);

            Assert.AreEqual("customScaffolderInSubsubfolder", results[4].Name);
            Assert.IsNull(results[4].PackageName);
        }

        [TestMethod]
        public void ShouldFindScaffoldersOnlyInLatestVersionOfAnyGivenPackage()
        {
            // Arrange
            var mockPackage1old = AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "packageWithNewerVersion", "1.9.0", "*.ps1", new Dictionary<string, ExternalScriptInfo> {
                { "scriptOverriddenInNewerPackage.ps1", MakeExternalScriptInfo("simpleScaffolder") },
                { "scriptNotPresentInNewerPackage.ps1", MakeExternalScriptInfo("simpleScaffolder") },
            });
            var mockPackage1new = AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "packageWithNewerVersion", "1.10.0", "*.ps1", new Dictionary<string, ExternalScriptInfo>
            {
                { "scriptOverriddenInNewerPackage.ps1", MakeExternalScriptInfo("simpleScaffolder") },
            });
            var mockPackage2 = AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "packageWithSingleVersion", "0.3.1", "*.ps1", new Dictionary<string, ExternalScriptInfo> {
                { "scriptInOtherPackage.ps1", MakeExternalScriptInfo("simpleScaffolder") },
            });
            _packageManager.Setup(x => x.LocalRepository.GetPackages()).Returns(new[] {
                mockPackage1old, mockPackage1new, mockPackage2
            }.AsQueryable());

            // Act
            var results = _scaffolderLocator.GetScaffolders(_solutionManager.Object.DefaultProject, null, false).ToList();

            // Assert
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("scriptOverriddenInNewerPackage", results[0].Name);
            Assert.AreEqual("packageWithNewerVersion 1.10.0", results[0].PackageName);

            Assert.AreEqual("scriptInOtherPackage", results[1].Name);
            Assert.AreEqual("packageWithSingleVersion 0.3.1", results[1].PackageName);
        }

        [TestMethod]
        public void ShouldFindOnlyPackageBasedScaffoldersIfNoProjectIsSpecified()
        {
            // Arrange: One package-based scaffolder
            var mockPackage1 = AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "package1", "1.0.0", "*.ps1", new Dictionary<string, ExternalScriptInfo> {
                { "packageBasedScaffolder.ps1", MakeExternalScriptInfo("simpleScaffolder") },
            });
            _packageManager.Setup(x => x.LocalRepository.GetPackages()).Returns(new[] { mockPackage1 }.AsQueryable());

            // Arrange: One project-based scaffolder
            new MockSolutionManagerBuilder(_solutionManager,
                new MockProject(new MockFolder("CustomScaffolders",
                    new MockItem("customScaffolderInRootFolder.ps1")
                )) { RootPath = "z:\\myproj" }
            ).Build();
            AddCustomScaffolders("z:\\myproj\\CustomScaffolders", "*.ps1", new Dictionary<string, string> {
                { "customScaffolderInRootFolder.ps1", "simpleScaffolder" },
            });

            // Act ** SHOULD FAIL *** - change project to null
            var results = _scaffolderLocator.GetScaffolders(null, null, false).ToList();

            // Assert
            Assert.AreEqual("packageBasedScaffolder", results.Single().Name);
        }

        [TestMethod]
        public void ShouldBeAbleToFindScaffolderByName()
        {
            // Arrange
            _packageManager.Setup(x => x.LocalRepository.GetPackages()).Returns(new[] {
                AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "myPackage", "1.0.0", "scriptName.ps1", new Dictionary<string, ExternalScriptInfo> {
                    { "anotherfolder\\yetanother\\scriptName.ps1", MakeExternalScriptInfo("simpleScaffolder") },
                })
            }.AsQueryable());

            // Act
            var results = _scaffolderLocator.GetScaffolders(_project, "scriptName", false).ToList();

            // Assert
            Assert.AreEqual("scriptName", results.Single().Name);
        }

        [TestMethod]
        public void ShouldMatchActualScaffolderInPreferenceToResolvingDefaultName()
        {
            // Arrange
            _configStore.Setup(x => x.GetProjectDefaultScaffolders(It.IsAny<Project>())).Callback(() => Assert.Fail("Should not have attempted to resolve the default name"));
            _configStore.Setup(x => x.GetSolutionDefaultScaffolders()).Callback(() => Assert.Fail("Should not have attempted to resolve the default name"));
            _packageManager.Setup(x => x.LocalRepository.GetPackages()).Returns(new[] {
                AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "myPackage", "1.0.0", "scriptName.ps1", new Dictionary<string, ExternalScriptInfo> {
                    { "anotherfolder\\yetanother\\scriptName.ps1", MakeExternalScriptInfo("simpleScaffolder") },
                })
            }.AsQueryable());

            // Act
            var results = _scaffolderLocator.GetScaffolders(_project, "scriptName", true).ToList();
            
            // Assert
            Assert.AreEqual("scriptName", results.Single().Name);
            // Since it did not read the value of DefaultScaffolders (which would have thrown an exception), this is a pass
        }

        [TestMethod]
        public void ShouldResolveDefaultNameFromProjectConfigIfRequested()
        {
            // Arrange
            _configStore.Setup(x => x.GetProjectDefaultScaffolders(_solutionManager.Object.DefaultProject)).Returns(new[] {
                new DefaultScaffolderConfigEntry("someDefaultName", "scriptName")
            }.AsQueryable());
            _packageManager.Setup(x => x.LocalRepository.GetPackages()).Returns(new[] {
                AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "myPackage", "1.0.0", "scriptName.ps1", new Dictionary<string, ExternalScriptInfo> {
                    { "anotherfolder\\yetanother\\scriptName.ps1", MakeExternalScriptInfo("simpleScaffolder") },
                })
            }.AsQueryable());

            // Act
            var results = _scaffolderLocator.GetScaffolders(_project, "someDefaultName", true).ToList();

            // Assert
            Assert.AreEqual("scriptName", results.Single().Name);
        }

        [TestMethod]
        public void ShouldResolveDefaultNameFromSolutionConfigIfRequestedAndNotConfiguredForProject()
        {
            // Arrange
            _configStore.Setup(x => x.GetProjectDefaultScaffolders(_solutionManager.Object.DefaultProject)).Returns(new DefaultScaffolderConfigEntry[] { }.AsQueryable());
            _configStore.Setup(x => x.GetSolutionDefaultScaffolders()).Returns(new[] {
                new DefaultScaffolderConfigEntry("someDefaultName", "scriptName")
            }.AsQueryable());
            _packageManager.Setup(x => x.LocalRepository.GetPackages()).Returns(new[] {
                AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "myPackage", "1.0.0", "scriptName.ps1", new Dictionary<string, ExternalScriptInfo> {
                    { "anotherfolder\\yetanother\\scriptName.ps1", MakeExternalScriptInfo("simpleScaffolder") },
                })
            }.AsQueryable());

            // Act
            var results = _scaffolderLocator.GetScaffolders(_project, "someDefaultName", true).ToList();

            // Assert
            Assert.AreEqual("scriptName", results.Single().Name);
        }

        [TestMethod]
        public void ShouldNotResolveDefaultNameIfNotRequested()
        {
            // Arrange
            _configStore.Setup(x => x.GetProjectDefaultScaffolders(It.IsAny<Project>())).Callback(() => Assert.Fail("Should not have attempted to resolve the default name"));
            _configStore.Setup(x => x.GetSolutionDefaultScaffolders()).Callback(() => Assert.Fail("Should not have attempted to resolve the default name"));
            _packageManager.Setup(x => x.LocalRepository.GetPackages()).Returns(new[] {
                // This will be ignored
                AddMockPackage(_packagePathResolver, _fileSystem, _commandInvoker, "myPackage", "1.0.0", "scriptName.ps1", new Dictionary<string, ExternalScriptInfo> {
                    { "anotherfolder\\yetanother\\scriptName.ps1", MakeExternalScriptInfo("simpleScaffolder") },
                })
            }.AsQueryable());

            // Act
            var results = _scaffolderLocator.GetScaffolders(_project, "thisNameDoesNotMatchAnActualScriptName", false).ToList();

            // Assert
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void ShouldBeAbleToSpecifyArbitraryProjectName()
        {
            // Arrange
            _packageManager.Setup(x => x.LocalRepository.GetPackages()).Returns(new IPackage[] {}.AsQueryable());
            new MockSolutionManagerBuilder(_solutionManager,
                new MockProject("proj1", new MockFolder("CodeTemplates", new MockFolder("Scaffolders", new MockItem("proj1scaffolder.ps1")))) { RootPath = "z:\\proj1" },
                new MockProject("proj2", new MockFolder("CodeTemplates", new MockFolder("Scaffolders", new MockItem("proj2scaffolder.ps1")))) { RootPath = "z:\\proj2" }
            ).Build();
            AddCustomScaffolders("z:\\proj1\\CodeTemplates\\Scaffolders", "*.ps1", new Dictionary<string, string> {
                { "proj1scaffolder.ps1", "simpleScaffolder" }
            });
            AddCustomScaffolders("z:\\proj2\\CodeTemplates\\Scaffolders", "*.ps1", new Dictionary<string, string> {
                { "proj2scaffolder.ps1", "simpleScaffolder" }
            });

            // Act
            var results = _scaffolderLocator.GetScaffolders(_solutionManager.Object.GetProject("proj2"), null, false).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("proj2scaffolder", results[0].Name);
        }

        private static IPackage AddMockPackage(Mock<IPackagePathResolver> pathResolver, Mock<IFileSystem> fileSystem, Mock<IPowershellCommandInvoker> commandInvoker, string packageName, string packageVersion, string expectedFilePattern, IDictionary<string, ExternalScriptInfo> scripts)
        {
            var mockPackage = MakeMockPackage(packageName, packageVersion);
            var randomInstallPath = "drive:\\somedir\\" + Guid.NewGuid();
            var toolsPath = Path.Combine(randomInstallPath, "tools");
            pathResolver.Setup(x => x.GetInstallPath(mockPackage)).Returns(randomInstallPath);
            fileSystem.Setup(x => x.DirectoryExists(toolsPath)).Returns(true);
            fileSystem.Setup(x => x.FindFiles(toolsPath, expectedFilePattern, true)).Returns(scripts.Select(x => Path.Combine(toolsPath, x.Key)));
            foreach (var script in scripts) {
                KeyValuePair<string, ExternalScriptInfo> scriptWithinClosure = script;
                commandInvoker.Setup(x => x.GetCommand(Path.Combine(toolsPath, scriptWithinClosure.Key), CommandTypes.ExternalScript))
                              .Returns(script.Value);
            }

            return mockPackage;
        }

        private void AddCustomScaffolders(string customScaffoldersRoot, string expectedSearchFilter, Dictionary<string, string> scaffolders)
        {
            _fileSystem.Setup(x => x.FindFiles(customScaffoldersRoot, expectedSearchFilter, true)).Returns(
                scaffolders.Select(x => Path.Combine(customScaffoldersRoot, x.Key))
                );
            foreach (var scaffolder in scaffolders) {
                KeyValuePair<string, string> scaffolderInClosure = scaffolder;
                _commandInvoker.Setup(x => x.GetCommand(Path.Combine(customScaffoldersRoot, scaffolderInClosure.Key), CommandTypes.ExternalScript))
                               .Returns(MakeExternalScriptInfo(scaffolderInClosure.Value));
            }
        }

        private static IPackage MakeMockPackage(string id, string version)
        {
            var mockPackage = new Mock<IPackage>();
            mockPackage.SetupGet(x => x.Id).Returns(id);
            mockPackage.SetupGet(x => x.Version).Returns(new Version(version));
            return mockPackage.Object;
        }

        private static ExternalScriptInfo MakeExternalScriptInfo(string exampleScriptName)
        {
            return ExampleScripts.MakeExternalScriptInfo(_psRunspace, exampleScriptName);
        }
    }
}
