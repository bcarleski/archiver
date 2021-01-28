using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Archiver
{
    class FileData
    {
        private readonly List<(string, string)> _paths;

        public FileData(string basePath, string path, string relativePathPrefix)
        {
            var file = new FileInfo(path);

            PrincipalPath = path;
            PrincipalRelativePath = relativePathPrefix + path.Substring(basePath.Length + (basePath.EndsWith('\\') ? 0 : 1));
            _paths = new List<(string, string)> { (PrincipalPath, PrincipalRelativePath) };
            Size = file.Length;

            var metaData = new FileInfo(path + ".json");
            if (metaData.Exists)
            {
                MetaDataPath = metaData.FullName;
                _paths.Add((MetaDataPath, relativePathPrefix + MetaDataPath.Substring(basePath.Length + (basePath.EndsWith('\\') ? 0 : 1))));
                Size += metaData.Length;
            }

            using (var str = file.OpenRead())
            using (var hasher = SHA1.Create())
            {
                var bytes = hasher.ComputeHash(str);
                Hash = string.Join("", bytes.Select(x => x.ToString("X2")));
            }
        }

        public long Size { get; }
        public string Hash { get; }
        public string PrincipalPath { get; }
        public string PrincipalRelativePath { get; }
        public IEnumerable<(string Full, string Relative)> Paths => _paths;
        public string MetaDataPath { get; }
        public string RelativePathPrefix { get; private set; }

        public void ChangeRelativePathPrefix(string newPrefix)
        {
            var oldPrefix = RelativePathPrefix;
            var newPaths = _paths.Select(x => (x.Item1, newPrefix + x.Item2.Substring(oldPrefix.Length)));

            _paths.Clear();
            _paths.AddRange(newPaths);
            RelativePathPrefix = newPrefix;
        }
    }
}
