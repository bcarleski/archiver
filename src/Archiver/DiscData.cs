using System.Collections.Generic;

namespace Archiver
{
    class DiscData
    {
        public DiscData(string baseDestinationPath, int discNumber, List<FileData> files)
        {
            Path = System.IO.Path.Combine(baseDestinationPath, $"Disc-{discNumber:000}");
            DiscNumber = discNumber;
            Files = files;
        }

        public string Path { get; }
        public int DiscNumber { get; }
        public List<FileData> Files { get; }
    }
}
