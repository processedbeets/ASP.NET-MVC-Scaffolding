using Moq;
using T4Scaffolding.Core.FileSystem;

namespace T4Scaffolding.Test.TestUtils
{
    static class MockFileSystemExtensions
    {
        public static Mock<IFileSystem> WithFile(this Mock<IFileSystem> fileSystem, string fullPath)
        {
            fileSystem.Setup(x => x.FileExists(fullPath)).Returns(true);
            return fileSystem;
        }

        public static Mock<IFileSystem> WithTextFile(this Mock<IFileSystem> fileSystem, string fullPath, string textContents)
        {
            fileSystem.Setup(x => x.ReadAllText(fullPath)).Returns(textContents);
            return fileSystem.WithFile(fullPath);
        }

        public static Mock<IFileSystem> WithExampleTemplate(this Mock<IFileSystem> fileSystem, string mockFilePath, string exampleTemplateName)
        {
            return fileSystem.WithTextFile(mockFilePath, ExampleTemplates.GetContents(exampleTemplateName));
        }
    }
}
