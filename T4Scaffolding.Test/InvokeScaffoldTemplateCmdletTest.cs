using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TextTemplating;
using Moq;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Core.FileSystem;
using T4Scaffolding.Test.ExampleModels;
using T4Scaffolding.Test.TestUtils;
using T4Scaffolding.NuGetServices.ExtensionMethods;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class InvokeScaffoldTemplateCmdletTest
    {
        [ClassInitialize]
        public static void InitializeT4Engine(TestContext context)
        {
            InvokeScaffoldTemplateCmdlet._t4Engine = new Engine();
        }

        [TestMethod]
        public void ShouldRejectNonProjectRelativeOutputPaths()
        {
            try {
                new InvokeScaffoldTemplateCmdlet(new MockSolutionManagerBuilder().Build(), null) {
                    OutputPath = "c:\\absolute\\path.cs"
                }.GetResults<string>();
                Assert.Fail("Did not throw");
            } catch(InvalidOperationException ex) {
                StringAssert.Contains(ex.Message, "OutputPath");
            }
        }

        [TestMethod]
        public void ShouldRenderTemplateAndAddOutputToProject()
        {
            var templateOutput = RunTemplateRenderingTest("fixedStringTemplate", model: null);
            Assert.AreEqual("Fixed string output from template", templateOutput);
        }

        [TestMethod]
        public void ShouldApplyFilenameExtensionFromOutputDirective()
        {
            var templateOutput = RunTemplateRenderingTest("templateWithOutputExtension", model: null, expectedOutputExtension: "extensionSpecifiedByOutputDirective");
            Assert.AreEqual("Fixed string output from template with output extension", templateOutput);
        }

        [TestMethod]
        public void ShouldSkipRenderingIfPhysicalFileAlreadyExistsAndForceIsOff()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(new MockProject { RootPath = "z:\\proj" }).Build();
            var mockFileSystem = new Mock<IFileSystem>().WithExampleTemplate("requestedTemplate", "fixedStringTemplate")
                                                        .WithFile("z:\\proj\\someFolder\\someFile.ext");

            // Act
            var results = new InvokeScaffoldTemplateCmdlet(solutionManager, mockFileSystem.Object) {
                Template = "requestedTemplate",
                OutputPath = "someFolder\\someFile.ext"
            }.GetResults<string>();

            // Assert
            Assert.AreEqual(0, results.Count()); // Did not write any files
        }

        [TestMethod]
        public void ShouldPerformRenderingIfPhysicalFileExistsAndIsReferencedByProjectAndForceIsOn()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(new MockProject(
                new MockFolder("someFolder", new MockItem("someFile.ext"))
            ) { RootPath = "z:\\proj" }).Build();
            var mockFileSystem = new Mock<IFileSystem>().WithExampleTemplate("requestedTemplate", "fixedStringTemplate")
                                                        .WithFile("z:\\proj\\someFolder\\someFile.ext");

            // Act
            var results = new InvokeScaffoldTemplateCmdlet(solutionManager, mockFileSystem.Object) {
                OutputPath = "someFolder\\someFile.ext",
                Template = "requestedTemplate",
                Force = new SwitchParameter(true)
            }.GetResults<string>();

            // Assert
            Assert.AreEqual("someFolder\\someFile.ext", results.Single());
            mockFileSystem.Verify(x => x.WriteAllText("z:\\proj\\someFolder\\someFile.ext", "Fixed string output from template"));
        }

        [TestMethod]
        public void ShouldPerformRenderingIfPhysicalFileExistsAndIsNotReferencedByProjectAndForceIsOn()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(new MockProject { RootPath = "z:\\proj" }).Build();
            var mockFileSystem = new Mock<IFileSystem>().WithExampleTemplate("requestedTemplate", "fixedStringTemplate")
                                                        .WithFile("z:\\proj\\someFolder\\someFile.ext");

            // Act
            var results = new InvokeScaffoldTemplateCmdlet(solutionManager, mockFileSystem.Object) {
                OutputPath = "someFolder\\someFile.ext",
                Template = "requestedTemplate",
                Force = new SwitchParameter(true)
            }.GetResults<string>();

            // Assert
            Assert.AreEqual("someFolder\\someFile.ext", results.Single());
            mockFileSystem.Verify(x => x.WriteAllText("z:\\proj\\someFolder\\someFile.ext", "Fixed string output from template"));
            Assert.IsNotNull(solutionManager.DefaultProject.GetProjectItem("someFolder\\someFile.ext"));
        }

        [TestMethod]
        public void ShouldPerformRenderingIfPhysicalFileDoesNotExistButIsReferencedByProject()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(new MockProject(
                new MockFolder("someFolder", new MockItem("someFile.ext"))
            ) { RootPath = "z:\\proj" }).Build();
            var mockFileSystem = new Mock<IFileSystem>().WithExampleTemplate("requestedTemplate", "fixedStringTemplate");

            // Act
            var results = new InvokeScaffoldTemplateCmdlet(solutionManager, mockFileSystem.Object) {
                OutputPath = "someFolder\\someFile.ext",
                Template = "requestedTemplate",
            }.GetResults<string>();

            // Assert
            Assert.AreEqual("someFolder\\someFile.ext", results.Single());
            mockFileSystem.Verify(x => x.WriteAllText("z:\\proj\\someFolder\\someFile.ext", "Fixed string output from template"));
        }

        [TestMethod]
        public void ShouldBeAbleToReadPropertiesFromModelDefinedInExternallyReferencedAssembly()
        {
            var templateOutput = RunTemplateRenderingTest("personTemplate", new Hashtable {
                {
                    "Person",
                    new PersonTemplateModel { Name = "Bertie", Age = 789 }
                }
            }, projectAssemblyReferences: new[] { typeof(PersonTemplateModel).Assembly });
            Assert.AreEqual("Bertie is 789 years old.", templateOutput);
        }

        [TestMethod]
        public void ShouldBeAbleToSpecifyArbitraryProjectName()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(
                new MockProject("firstProject"),
                new MockProject("secondProject") { RootPath = "second:\\root" }
            ).Build();
            var mockFileSystem = new Mock<IFileSystem>().WithExampleTemplate("requestedTemplate", "fixedStringTemplate");

            // Act
            var results = new InvokeScaffoldTemplateCmdlet(solutionManager, mockFileSystem.Object) {
                OutputPath = "someFolder\\someFile.ext",
                Template = "requestedTemplate",
                Project = "secondProject"
            }.GetResults<string>();

            // Assert
            Assert.AreEqual("someFolder\\someFile.ext", results.Single());
            mockFileSystem.Verify(x => x.WriteAllText("second:\\root\\someFolder\\someFile.ext", "Fixed string output from template"));
            Assert.IsNotNull(solutionManager.GetProject("secondProject").GetProjectItem("someFolder\\someFile.ext"));
        }

        [TestMethod]
        public void ShouldBeAbleToLocateProjectRelativeTemplates()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(new MockProject(
                new MockFolder("myTemplates", new MockItem("someTemplate.ext.t4"))
            ) { RootPath = "z:\\proj" }).Build();
            var mockFileSystem = new Mock<IFileSystem>().WithTextFile("z:\\proj\\myTemplates\\someTemplate.ext.t4", "My project-relative template contents");

            // Act
            var results = new InvokeScaffoldTemplateCmdlet(solutionManager, mockFileSystem.Object) {
                OutputPath = "someFolder\\someFile.ext",
                Template = "myTemplates\\someTemplate.ext.t4"
            }.GetResults<string>();

            // Assert
            Assert.AreEqual("someFolder\\someFile.ext", results.Single());
            mockFileSystem.Verify(x => x.WriteAllText("z:\\proj\\someFolder\\someFile.ext", "My project-relative template contents"));
            Assert.IsNotNull(solutionManager.DefaultProject.GetProjectItem("someFolder\\someFile.ext"));
        }

        [TestMethod]
        public void ShouldBeAbleToGetOutputAsStringIfNoOutputPathIsSpecified()
        {
            // Arrange
            var solutionManager = new MockSolutionManagerBuilder(new MockProject()).Build();
            var mockFileSystem = new Mock<IFileSystem>().WithExampleTemplate("requestedTemplate", "simpleTemplate");

            // Act
            var results = new InvokeScaffoldTemplateCmdlet(solutionManager, mockFileSystem.Object) {
                Model = new Hashtable {
                    { "Name", "Bert" },
                    { "Values", new [] { 1, 2, 3, 4 }}
                },
                Template = "requestedTemplate"
            }.GetResults<string>();

            // Assert
            Assert.AreEqual("The name is Bert. There are 4 values.", results.Single());
        }

        private static string RunTemplateRenderingTest(string exampleTemplateName, Hashtable model, IEnumerable<Assembly> projectAssemblyReferences = null, string expectedOutputExtension = null)
        {
            // Arrange: Project containing a single folder
            var solutionManager = new MockSolutionManagerBuilder(new MockProject(new MockFolder("someFolder")) {
                RootPath = "z:\\proj", 
                References = projectAssemblyReferences != null ? projectAssemblyReferences.Select(x => x.Location) : new string[] { }
            }).Build();

            // Arrange: Filesystem that has template and captures attempt to write rendered output to disk
            var requestedOutputPath = "someFolder\\nonExistentFolder\\file.ext";
            var expectedOutputPathWithExtension = requestedOutputPath;
            if (!string.IsNullOrEmpty(expectedOutputExtension))
                expectedOutputPathWithExtension += "." + expectedOutputExtension;
            var mockFileSystem = new Mock<IFileSystem>().WithExampleTemplate("requestedTemplate", exampleTemplateName);
            string templateOutput = null;
            mockFileSystem.Setup(x => x.WriteAllText("z:\\proj\\" + expectedOutputPathWithExtension, It.IsAny<string>()))
                          .Callback<string, string>((filename, writtenText) => { templateOutput = writtenText; });

            // Act: Invoke the template
            try
            {                
                var results = new InvokeScaffoldTemplateCmdlet(solutionManager, mockFileSystem.Object) {
                    OutputPath = requestedOutputPath,
                    Template = "requestedTemplate",
                    Model = model
                }.GetResults<string>();

                // Assert
                Assert.AreEqual(expectedOutputPathWithExtension, results.Single());
                Assert.IsNotNull(solutionManager.DefaultProject.GetProjectItem(expectedOutputPathWithExtension));
                return templateOutput;
            }
            catch (InvalidOperationException ex) {
                // If it was a template rendering error, throw a more descriptive exception
                var templateErrors = ex.Data["CmdletOutput"] as IList<object>;
                if ((templateErrors != null) && (templateErrors.Any()) && (templateErrors.First() is IEnumerable<CompilerError>))
                    throw new InvalidOperationException("Template rendering error: " + string.Join(Environment.NewLine, ((IEnumerable<CompilerError>)templateErrors.First()).Select(x => x.ToString())));
                throw;
            }
        }
    }
}