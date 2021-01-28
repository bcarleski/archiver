using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Archiver
{
    class Program
    {
        private const string _metaDataFolder = ".archiverMetaData\\";
        private const string _indexFileName = "\\index.html";

        // CONFIGURATION ITEMS
        private static string _basePath;
        private static string[] _importantArchiveFolders;
        private static string _destinationPath;
        private static string _sourceCodePath;
        private static long _importantArchiveDiscMB;
        private static long _regularArchiveDiscMB;

        static void Main(string[] args)
        {
            LoadConfiguration(args);

            Log($"Finding files under {_basePath}");
            var files = GetFiles(_basePath, "").ToList();

            Log($"Found {files.Count} files");
            var importantArchiveFiles = files.Where(x => _importantArchiveFolders.Any(y => x.PrincipalPath.StartsWith(y + '\\'))).ToList();
            var regularArchiveFiles = files.Where(x => !_importantArchiveFolders.Any(y => x.PrincipalPath.StartsWith(y + '\\'))).ToList();

            ProcessFiles("important", importantArchiveFiles, _importantArchiveDiscMB * 1000000L);
            ProcessFiles("regular", regularArchiveFiles, _regularArchiveDiscMB * 1000000L);
        }

        private static void LoadConfiguration(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var builder = new ConfigurationBuilder()
                .AddJsonFile($"appSettings.json", true, true)
                .AddJsonFile($"appSettings.{env}.json", true, true)
                .AddEnvironmentVariables()
                .AddCommandLine(args, new Dictionary<string, string>
                {
                    ["-b"] = "basePath",
                    ["--base"] = "basePath",
                    ["--basePath"] = "basePath",
                    ["-f"] = "importantArchiveFolders",
                    ["--folders"] = "importantArchiveFolders",
                    ["--importantFolders"] = "importantArchiveFolders",
                    ["--importantArchiveFolders"] = "importantArchiveFolders",
                    ["-d"] = "destinationPath",
                    ["--destination"] = "destinationPath",
                    ["--destinationPath"] = "destinationPath",
                    ["-s"] = "sourceCodePath",
                    ["--source"] = "sourceCodePath",
                    ["--sourceCode"] = "sourceCodePath",
                    ["--sourceCodePath"] = "sourceCodePath",
                    ["-imb"] = "importantArchiveDiscMB",
                    ["--importantMB"] = "importantArchiveDiscMB",
                    ["--importantDiscMB"] = "importantArchiveDiscMB",
                    ["--importantArchiveDiscMB"] = "importantArchiveDiscMB",
                    ["-rmb"] = "regularArchiveDiscMB",
                    ["--regularMB"] = "regularArchiveDiscMB",
                    ["--regularDiscMB"] = "regularArchiveDiscMB",
                    ["--regularArchiveDiscMB"] = "regularArchiveDiscMB"
                });

            var config = builder.Build();

            _basePath = Path.GetFullPath(config.GetSection("basePath").Value ?? throw new ArgumentNullException("You must provide a basePath"));
            _destinationPath = Path.GetFullPath(config.GetSection("destinationPath").Value ?? throw new ArgumentNullException("You must provide a destinationPath"));
            _sourceCodePath = Path.GetFullPath(config.GetSection("sourceCodePath").Value ?? throw new ArgumentNullException("You must provide a sourceCodePath"));
            _importantArchiveDiscMB = int.TryParse(config.GetSection("importantArchiveDiscMB").Value, out var v) ? v : 4000;
            _regularArchiveDiscMB = int.TryParse(config.GetSection("regularArchiveDiscMB").Value, out v) ? v : 4000;

            var folders = config.GetSection("importantArchiveFolders").Value?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
            if (folders.Length == 0)
            {
                _importantArchiveFolders = new string[0];
            }
            else
            {
                _importantArchiveFolders = folders.Select(x => Path.GetFullPath(Path.Combine(_basePath, x)).TrimEnd('\\')).ToArray();
            }
        }

        private static void Log(string message)
        {
            Console.WriteLine($"{DateTime.Now.ToString("O")} - {message}");
        }

        private static IEnumerable<FileData> GetFiles(string root, string relativePathPrefix, params string[] excludedFolders)
        {
            var basePath = Path.GetFullPath(root);
            var baseDir = new DirectoryInfo(basePath);
            return baseDir
                .EnumerateFiles("*", SearchOption.TopDirectoryOnly).Select(x => new FileData(basePath, x.FullName, relativePathPrefix))
                    .Concat(baseDir.EnumerateDirectories()
                    .Where(x => excludedFolders == null || !excludedFolders.Contains(x.Name))
                    .AsParallel()
                    .SelectMany(x => x.EnumerateFiles("*", SearchOption.AllDirectories).Select(y => new FileData(basePath, y.FullName, relativePathPrefix)))
                );
        }

        private static void ProcessFiles(string type, List<FileData> files, long maxDiscSizeInBytes)
        {
            DeDuplicateFiles(files);

            Log($"Processing {files.Count} unique {type} archive files");
            var extraFiles = FindExtraFiles();
            var discs = SplitIntoDiscs(Path.Combine(_destinationPath, $"{type}Archive"), files, extraFiles.Sum(x => x.Size), maxDiscSizeInBytes).ToList();
            var metaData = GenerateMetaData(discs);

            foreach (var disc in discs)
            {
                AddFilesToDiscFolder(disc, extraFiles, false);
                AddFilesToDiscFolder(disc, new[] { metaData }, false);
                WriteDiscMetaData(disc);
                AddFilesToDiscFolder(disc, disc.Files, true);
            }

            File.Delete(metaData.PrincipalPath);
        }

        private static void DeDuplicateFiles(List<FileData> files)
        {
            var knownHashes = new HashSet<string>();
            var knownMetaData = new HashSet<string>();
            var toRemove = new List<FileData>();

            foreach (var file in files)
            {
                if (knownHashes.Contains(file.Hash))
                {
                    toRemove.Add(file);
                }
                else
                {
                    knownHashes.Add(file.Hash);
                    if (file.MetaDataPath != null) knownMetaData.Add(file.MetaDataPath);
                }
            }

            foreach (var file in files)
            {
                if (knownMetaData.Contains(file.PrincipalPath)) toRemove.Add(file);
            }

            toRemove.ForEach(x => files.Remove(x));
        }

        private static List<FileData> FindExtraFiles()
        {
            var extraFiles = new List<FileData>();

            // Add all the code binary files
            extraFiles.AddRange(GetFiles(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath), _metaDataFolder));

            // Add all files from the source code
            extraFiles.AddRange(GetFiles(_sourceCodePath, _metaDataFolder, "bin", "obj"));

            // Make the index.html file appear in the root so that they see it when browsing the disc
            extraFiles.ForEach(x =>
            {
                if (x.PrincipalPath.EndsWith(_indexFileName)) x.ChangeRelativePathPrefix("");
            });

            return extraFiles;
        }

        private static IEnumerable<DiscData> SplitIntoDiscs(string baseDestinationPath, List<FileData> files, long extraSpace, long maxDiscSizeInBytes)
        {
            var discFiles = new List<FileData>();
            var availableSpace = maxDiscSizeInBytes - extraSpace;
            var discNumber = 1;

            foreach (var file in files)
            {
                if (availableSpace > file.Size)
                {
                    availableSpace -= file.Size;
                    discFiles.Add(file);
                }
                else
                {
                    if (discFiles.Count == 0) throw new Exception("There is a single file that is too big for a single disc.  Please split the file before proceeding.  " + file.PrincipalPath);

                    yield return new DiscData(baseDestinationPath, discNumber++, discFiles);
                    availableSpace = maxDiscSizeInBytes - extraSpace;
                    discFiles.Clear();
                }
            }

            if (discFiles.Count > 0) yield return new DiscData(baseDestinationPath, discNumber, discFiles);
        }

        private static FileData GenerateMetaData(List<DiscData> discs)
        {
            // Record each file name, and if it has a JSON file, also record the title, when it was taken (photoTakenTime/creationTime), where it was taken (geoData/geoDataExif), its description, and which people are in it
            var tempDir = Path.GetTempFileName();
            var metaDir = Path.Combine(tempDir, _metaDataFolder.TrimEnd('\\'));
            var uniqueId = 0L;

            File.Delete(tempDir);
            Directory.CreateDirectory(metaDir);

            var tempFile = Path.Combine(metaDir, "metaData.json");

            using (var wtr = new StreamWriter(tempFile))
            using (var json = new JsonTextWriter(wtr))
            {
                json.WriteStartObject();
                json.WritePropertyName("fieldMeaning");
                json.WriteStartObject();
                json.WritePropertyName("u");
                json.WriteValue("Unique ID");
                json.WritePropertyName("n");
                json.WriteValue("Name");
                json.WritePropertyName("r");
                json.WriteValue("Relative Path");
                json.WritePropertyName("t");
                json.WriteValue("Title");
                json.WritePropertyName("d");
                json.WriteValue("Timestamp of the date it was taken");
                json.WritePropertyName("l");
                json.WriteValue("Location it was taken at, as latitude,longitude");
                json.WritePropertyName("x");
                json.WriteValue("Description/Notes");
                json.WritePropertyName("p");
                json.WriteValue("People");
                json.WritePropertyName("c");
                json.WriteValue("Disc number the file is found on");
                json.WriteEndObject();
                json.WritePropertyName("files");
                json.WriteStartArray();

                foreach (var discFile in discs.SelectMany(x => x.Files.Select(y => (Disc: x, File: y))))
                {
                    json.WriteStartObject();
                    json.WritePropertyName("u");
                    json.WriteValue(++uniqueId);
                    json.WritePropertyName("n");
                    json.WriteValue(Path.GetFileName(discFile.File.PrincipalPath));
                    json.WritePropertyName("r");
                    json.WriteValue(discFile.File.PrincipalRelativePath.Replace('\\', '/'));

                    if (!string.IsNullOrWhiteSpace(discFile.File.MetaDataPath))
                    {
                        var metaDataText = File.ReadAllText(discFile.File.MetaDataPath);
                        var metaData = JsonConvert.DeserializeObject<GoogleMetaData>(metaDataText);

                        if (!string.IsNullOrWhiteSpace(metaData.Title)) { json.WritePropertyName("t"); json.WriteValue(metaData.Title); }

                        var date = metaData.PhotoTakenTime ?? metaData.CreationTime;
                        if (long.TryParse(date?.Timestamp, out var ts)) { json.WritePropertyName("d"); json.WriteValue(ts > 0 && ts < 10000000000 ? ts * 1000 : ts); }

                        var lat = metaData.GeoData?.Latitude ?? metaData.GeoDataExif?.Latitude;
                        var lng = metaData.GeoData?.Longitude ?? metaData.GeoDataExif?.Longitude;
                        if (lat != null && double.IsFinite(lat.Value) && lat != 0.0 && lng != null && double.IsFinite(lng.Value) && lng != 0.0) { json.WritePropertyName("l"); json.WriteValue($"{lat},{lng}"); }

                        if (!string.IsNullOrWhiteSpace(metaData.Description)) { json.WritePropertyName("x"); json.WriteValue(metaData.Description); }

                        var people = metaData.People?.Where(x => !string.IsNullOrWhiteSpace(x?.Name)).ToArray();
                        if (people != null && people.Length > 0)
                        {
                            json.WritePropertyName("p");
                            json.WriteStartArray();
                            
                            foreach (var person in people)
                            {
                                json.WriteValue(person.Name);
                            }

                            json.WriteEndArray();
                        }
                    }

                    json.WriteEndObject();
                }

                json.WriteEndArray();
                json.WriteEndObject();

                json.Flush();
            }

            return new FileData(tempDir, tempFile, "");
        }

        private static void WriteDiscMetaData(DiscData disc)
        {
            using (var wtr = new StreamWriter(Path.Combine(disc.Path, _metaDataFolder.TrimEnd('\\'), "discMetaData.json")))
            using (var json = new JsonTextWriter(wtr))
            {
                json.WriteStartObject();
                json.WritePropertyName("discNumber");
                json.WriteValue(disc.DiscNumber);
                json.WriteEndObject();
                json.Flush();
            }
        }

        private static void AddFilesToDiscFolder(DiscData disc, IEnumerable<FileData> files, bool moveFile)
        {
            foreach (var file in files)
            {
                foreach (var path in file.Paths)
                {
                    var dest = Path.Combine(disc.Path, path.Relative);
                    var writeAttempts = 0;
                    var written = false;

                    while (!written && writeAttempts++ < 20)
                    {
                        if (writeAttempts > 1) Thread.Sleep(200);

                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dest));

                            if (moveFile) File.Move(path.Full, dest, true);
                            else File.Copy(path.Full, dest, true);

                            written = true;
                        }
                        catch (DirectoryNotFoundException)
                        {
                            // Handles an OS bug where it can sometimes return from the CreateDirectory call before the directory is actually available to use
                        }
                    }
                }
            }
        }
    }
}
