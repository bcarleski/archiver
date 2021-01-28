using System.Collections.Generic;

namespace Archiver
{
    class DiscData
    {
        public DiscData(string baseDestinationPath, int discNumber, IEnumerable<FileData> files)
        {
            Path = System.IO.Path.Combine(baseDestinationPath, $"Disc-{discNumber:000}");
            DiscNumber = discNumber;
            Files = new List<FileData>(files);
        }

        public string Path { get; }
        public int DiscNumber { get; }
        public List<FileData> Files { get; }
    }
}
