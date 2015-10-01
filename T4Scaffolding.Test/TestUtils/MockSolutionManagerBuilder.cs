using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Castle.DynamicProxy.Generators;
using EnvDTE;
using Moq;
using T4Scaffolding.Core;
using T4Scaffolding.NuGetServices.Services;

namespace T4Scaffolding.Test.TestUtils
{
    /// <summary>
    /// Clunky but viable way to simulate a Visual Studio solution/projects/folders/files structure
    /// Consider tidying this up while retaining same functionality
    /// </summary>
    internal class MockSolutionManagerBuilder
    {
        private static readonly Func<bool> actionWrapper = () => { AttributesToAvoidReplicating.Add<TypeIdentifierAttribute>(); return true; };
        private static readonly Lazy<bool> lazyAction = new Lazy<bool>(actionWrapper);

        private readonly Mock<ISolutionManager> _mockSolutionManager;
        private readonly MockProject[] _projects;
        public string DefaultProjectName { get; set; }
        public bool IsSolutionOpen { get; set; }

        public MockSolutionManagerBuilder(params MockProject[] projects) : this(new Mock<ISolutionManager>(), projects)
        {
        }

        public MockSolutionManagerBuilder(Mock<ISolutionManager> mockSolutionManager, params MockProject[] projects)
        {
            if (mockSolutionManager == null) throw new ArgumentNullException("mockSolutionManager");

            Debug.Assert(lazyAction.Value, "Lazy action must have been initialized by now");
            _mockSolutionManager = mockSolutionManager;
            _projects = projects;
            DefaultProjectName = projects.Any() ? projects.First().Name : null;
        }

        public ISolutionManager Build()
        {
            var builtProjects = _projects.Select(x => x.Build()).ToList();

            _mockSolutionManager.SetupGet(c => c.DefaultProjectName).Returns(DefaultProjectName);
            _mockSolutionManager.Setup(c => c.GetProjects()).Returns(builtProjects);
            _mockSolutionManager.Setup(c => c.GetProject(It.IsAny<string>()))
                .Returns((string name) => builtProjects.FirstOrDefault(p => p.Name == name));
            _mockSolutionManager.SetupGet(c => c.IsSolutionOpen).Returns(IsSolutionOpen);
            _mockSolutionManager.SetupGet(c => c.DefaultProject).Returns(() => {
                switch (builtProjects.Count) {
                    case 0: throw new InvalidOperationException("Cannot get DefaultProject from mock - no projects in solution");
                    case 1: return builtProjects.Single();
                    default: return builtProjects.Single(x => x.Name == DefaultProjectName);
                }
            });
            return _mockSolutionManager.Object;
        }
    }

    internal class MockProjectItems
    {
        public MockProjectItems(MockItem[] children)
        {
            Children = children;
        }

        protected MockItem[] Children { get; private set; }

        public ProjectItems Build(object owner, string fullPath)
        {
            if (Children == null)
                return null;
            
            var lookup = new Dictionary<object, ProjectItem>();
            foreach (var file in Children) {
                lookup[file.Name] = file.BuildMock(owner);
            }

            var projectItems = new Mock<ProjectItems>(MockBehavior.Strict);
            projectItems.Setup(x => x.Item(It.IsAny<object>())).Returns((object index) => {
                return lookup[index];
            });
            projectItems.Setup(x => x.GetEnumerator()).Returns(() => {
                return lookup.Values.GetEnumerator();
            });
            projectItems.Setup(x => x.Count).Returns(lookup.Count);
            projectItems.Setup<object>(x => x.Parent).Returns(owner);
            
            projectItems.Setup(x => x.AddFromDirectory(It.IsAny<string>())).Returns<string>(dir => {
                if (!dir.StartsWith(fullPath + Path.DirectorySeparatorChar))
                    throw new InvalidOperationException(string.Format("Can't add dir {0} - this is not a subfolder of {1}", dir, fullPath));
                var subfolderName = dir.Substring(fullPath.Length + 1 /* add 1 for path separator char */);
                if (subfolderName.Contains(Path.DirectorySeparatorChar))
                    throw new InvalidOperationException("Invalid dir name: " + subfolderName);
                lookup[subfolderName] = new MockFolder(subfolderName).BuildMock(owner);
                return lookup[subfolderName];
            });

            projectItems.Setup(x => x.AddFromFile(It.IsAny<string>())).Returns<string>(filename => {
                filename = Path.GetFileName(filename);
                lookup[filename] = new MockItem(filename).BuildMock(owner);
                return lookup[filename];
            });

            return projectItems.Object;
        }
    }

    internal class MockProject
    {
        public string Name { get; private set; }
        public string Kind { get; set; }
        public MockProjectItems Items { get; private set; }
        public string RootPath { get; set; }
        public IEnumerable<string> References { get; set; }

        public MockProject(params MockItem[] children) : this("Proj" + Guid.NewGuid(), children) { }

        public MockProject(string name, params MockItem[] children)
        {
            Name = name;
            Items = new MockProjectItems(children);
            Kind = VsConstants.CsharpProjectTypeGuid;
            References = new string[] {};
            RootPath = "default:\\mock\\root\\path";
        }

        public Project Build()
        {
            var project = new Mock<Project>();
            project.SetupGet(p => p.Name).Returns(Name);
            project.SetupGet(p => p.FullName).Returns(Name);
            project.SetupGet(p => p.UniqueName).Returns(Name);
            project.SetupGet(p => p.Kind).Returns(Kind);
            project.SetupGet(x => x.Properties).Returns(new MockProperties {
                { "FullPath", RootPath }
            }.Build());
            project.SetupGet(p => p.ProjectItems).Returns(Items.Build(project.Object, RootPath));
            project.SetupGet(p => p.ConfigurationManager).Returns((ConfigurationManager)null);

            dynamic obj = new ExpandoObject();
            obj.References = References.Select(CreateReferenceFromPath);
            project.SetupGet(x => x.Object).Returns(() => obj);
            
            return project.Object;
        }

        private static object CreateReferenceFromPath(string path)
        {
            var actualAssemblyName = Assembly.ReflectionOnlyLoadFrom(path).GetName();
            dynamic reference = new ExpandoObject();
            reference.Path = path;
            reference.AutoReferenced = false;
            reference.Name = actualAssemblyName.Name;
            reference.Version = actualAssemblyName.Version.ToString();
            reference.Culture = actualAssemblyName.CultureInfo.IsNeutralCulture ? null : actualAssemblyName.CultureInfo.Name;
            reference.PublicKeyToken = new System.Runtime.Remoting.Metadata.W3cXsd2001.SoapHexBinary(actualAssemblyName.GetPublicKeyToken()).ToString();
            return reference;
        }
    }

    internal class MockItem
    {
        public string Name { get; private set; }

        public MockItem(string name)
        {
            Name = name;
        }

        public virtual ProjectItem BuildMock(object parent)
        {
            if (parent == null) throw new ArgumentNullException("parent");

            var file = new Mock<ProjectItem>();
            file.SetupGet(x => x.Name).Returns(Name);
            file.SetupGet(x => x.Kind).Returns(VsConstants.VsProjectItemKindPhysicalFile);
            file.SetupGet(x => x.Properties).Returns(new MockProperties {
                { "FullPath", Path.Combine(((dynamic)parent).Properties.Item("FullPath").Value, Name) }
            }.Build());

            Project containingProject = parent is Project ? parent : ((dynamic)parent).ContainingProject;
            file.SetupGet(x => x.ContainingProject).Returns(containingProject);

            return file.Object;
        }
    }

    internal class MockFolder : MockItem
    {
        public MockProjectItems Items { get; private set; }

        public MockFolder(string name, params MockItem[] children)
            : base(name)
        {
            Items = new MockProjectItems(children);
        }

        public override ProjectItem BuildMock(object parent)
        {
            if (parent == null) throw new ArgumentNullException("parent");
            string fullPath = Path.Combine(((dynamic)parent).Properties.Item("FullPath").Value, Name);

            var folder = new Mock<ProjectItem>();
            folder.SetupGet(x => x.Name).Returns(Name);
            folder.SetupGet(x => x.Kind).Returns(VsConstants.VsProjectItemKindPhysicalFolder);
            folder.SetupGet(x => x.Properties).Returns(new MockProperties {
                { "FullPath", fullPath }
            }.Build());

            Project containingProject = parent is Project ? parent : ((dynamic)parent).ContainingProject;
            folder.SetupGet(x => x.ContainingProject).Returns(containingProject);

            folder.SetupGet(x => x.ProjectItems).Returns(Items.Build(folder.Object, fullPath));
            
            return folder.Object;
        }
    }

    internal class MockProperties : Dictionary<string, object>
    {
        public Properties Build()
        {
            var mockProperties = new Mock<Properties>();

            foreach (var keyValuePair in this) {
                var pair = keyValuePair;
                var prop = new Mock<Property>();
                prop.Setup<object>(x => x.Value).Returns(keyValuePair.Value);
                mockProperties.Setup(x => x.Item(pair.Key)).Returns(prop.Object);
            }

            return mockProperties.Object;
        }
    }
}