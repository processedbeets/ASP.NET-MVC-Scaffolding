using System.Collections.Generic;
using System.IO;

namespace T4Scaffolding.Core.FileSystem
{
    public class DefaultFileSystem : IFileSystem
    {
        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public void WriteAllText(string path, string textContents)
        {
            File.WriteAllText(path, textContents);
        }

        public IEnumerable<string> FindFiles(string path, string pattern, bool includeSubdirectories)
        {
            return Directory.EnumerateFiles(path, pattern, includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }
    }
}