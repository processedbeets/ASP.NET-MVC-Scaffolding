using System.Collections.Generic;

namespace T4Scaffolding.Core.FileSystem
{
    public interface IFileSystem
    {
        bool DirectoryExists(string path);
        bool FileExists(string path);
        void CreateDirectory(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string textContents);
        IEnumerable<string> FindFiles(string path, string pattern, bool includeSubdirectories);
    }
}