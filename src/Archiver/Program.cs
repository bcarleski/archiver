using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;

namespace Archiver
{
    class Program
    {
        private const string _metaDataFolder = ".archiverMetaData";

        // CONFIGURATION ITEMS
        private static string _basePath;
        private static string[] _importantArchiveFolders;
        private static string _destinationPath;
        private static Uri _sourceCodePath;
        private static Uri _browserHtmlPath;
        private static long _importantArchiveDiscMB;
        private static long _regularArchiveDiscMB;
        private static bool _copyOnly;
        private static bool _test;

        static int Main(string[] args)
        {
            if (!LoadConfiguration(args)) return 1;

            try
            {
                Log($"Finding files under {_basePath}");
                var files = GetBaseFiles();

                Log($"Found {files.Count} files");
                var importantArchiveFiles = files.Where(x => _importantArchiveFolders.Any(y => x.PrincipalPath.StartsWith(y + '\\'))).ToList();
                var regularArchiveFiles = files.Where(x => !_importantArchiveFolders.Any(y => x.PrincipalPath.StartsWith(y + '\\'))).ToList();

                ProcessFiles("important", importantArchiveFiles, _importantArchiveDiscMB * 1000000L);
                ProcessFiles("regular", regularArchiveFiles, _regularArchiveDiscMB * 1000000L);

                return 0;
            }
            catch (Exception ex)
            {
                Log("UNHANDLED EXCEPTION: " + ex.ToString());
                return 2;
            }
        }

        private static bool LoadConfiguration(string[] args)
        {
            try
            {
                if (args == null || args.Length == 0 || args.Select(x => x.ToLowerInvariant()).Any(x => x == "help" || x == "?" || x == "/help" || x == "/?" || x == "-help" || x == "-?" || x == "--help" || x == "--?"))
                {
                    Console.WriteLine("OVERVIEW:");
                    Console.WriteLine("The Archiver command takes a Google Takeout archive, and prepares it for writing to optical media.  This includes de-duplicating files, splitting into folders small enough for the optical media, copying the binary and source versions of this program, writing meta-data about the files, and writing an HTML browser that can browse and review the file listings.");
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine("USAGE:");
                    Console.WriteLine("Archiver {RequiredArguments...} [{OptionalArguments...}]");
                    Console.WriteLine();
                    Console.WriteLine("Required Arguments:");
                    Console.WriteLine("    -b|--base {BasePath}                          The path to the Google Takeout archive containing all the files to archive");
                    Console.WriteLine("    -d|--destination {DestinationPath}            The base destination folder");
                    Console.WriteLine("    -s|--source {SourceCodePath}                  The URL to the Archiver source code ZIP file");
                    Console.WriteLine("    -h|--html {BrowserHtmlPath}                   The URL to the Archiver Browser compiled HTML ZIP file");
                    Console.WriteLine();
                    Console.WriteLine("Optional Arguments:");
                    Console.WriteLine("    -f|--folders {ImportantArchiveFolders}        A semi-colon separated list of relative paths within the base path which should be considered important");
                    Console.WriteLine("    -imb|--importantMB {ImportantArchiveDiscMB}   The size of disc, in megabytes, that will be used for the important files");
                    Console.WriteLine("    -rmb|--regularMB {RegularArchiveDiscMB}       The size of disc, in megabytes, that will be used for non-important files");
                    Console.WriteLine("    -c|--copy (true|false)                        If true, then files will be copied into the destination folder.  If false, they will be moved in.  Defaults to false.");
                    Console.WriteLine("    -t|--test (true|false)                        If true, files will not actually be copied/moved but the program will perform all other steps.  Defaults to false.");

                    return false;
                }

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
                        ["-h"] = "browserHtmlPath",
                        ["--html"] = "browserHtmlPath",
                        ["--browserHtml"] = "browserHtmlPath",
                        ["--browserHtmlPath"] = "browserHtmlPath",
                        ["-imb"] = "importantArchiveDiscMB",
                        ["--importantMB"] = "importantArchiveDiscMB",
                        ["--importantDiscMB"] = "importantArchiveDiscMB",
                        ["--importantArchiveDiscMB"] = "importantArchiveDiscMB",
                        ["-rmb"] = "regularArchiveDiscMB",
                        ["--regularMB"] = "regularArchiveDiscMB",
                        ["--regularDiscMB"] = "regularArchiveDiscMB",
                        ["--regularArchiveDiscMB"] = "regularArchiveDiscMB",
                        ["-t"] = "test",
                        ["--test"] = "test",
                        ["-c"] = "copyOnly",
                        ["--copy"] = "copyOnly",
                        ["--copyOnly"] = "copyOnly"
                    });

                var config = builder.Build();

                _basePath = Path.GetFullPath(config.GetSection("basePath")?.Value ?? throw new ArgumentNullException("basePath", "You must provide a basePath"));
                _destinationPath = Path.GetFullPath(config.GetSection("destinationPath")?.Value ?? throw new ArgumentNullException("destinationPath", "You must provide a destinationPath"));
                _sourceCodePath = Uri.TryCreate(config.GetSection("sourceCodePath")?.Value ?? throw new ArgumentNullException("sourceCodePath", "You must provide a sourceCodePath"), UriKind.Absolute, out var u)
                    ? u : throw new ArgumentException("Invalid source code URL");
                _browserHtmlPath = Uri.TryCreate(config.GetSection("browserHtmlPath")?.Value ?? throw new ArgumentNullException("browserHtmlPath", "You must provide a browserHtmlPath"), UriKind.Absolute, out u)
                    ? u : throw new ArgumentException("Invalid browser HTML URL");

                var folders = config.GetSection("importantArchiveFolders").Value?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
                if (folders.Length == 0)
                {
                    _importantArchiveFolders = new string[0];
                }
                else
                {
                    _importantArchiveFolders = folders.Select(x => Path.GetFullPath(Path.Combine(_basePath, x)).TrimEnd('\\')).ToArray();
                }

                _importantArchiveDiscMB = int.TryParse(config.GetSection("importantArchiveDiscMB")?.Value, out var v) ? v : 0;
                if (_importantArchiveDiscMB <= 0) _importantArchiveDiscMB = 4000;

                _regularArchiveDiscMB = int.TryParse(config.GetSection("regularArchiveDiscMB")?.Value, out v) ? v : 0;
                if (_regularArchiveDiscMB <= 0) _regularArchiveDiscMB = 4000;

                _copyOnly = bool.TryParse(config.GetSection("copyOnly")?.Value, out var b) && b;
                _test = bool.TryParse(config.GetSection("test")?.Value, out b) && b;

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        private static void Log(string message)
        {
            Console.WriteLine($"{DateTime.Now.ToString("O")} - {message}");
        }

        private static List<FileData> GetBaseFiles()
        {
            var savedJson = Path.Combine(_basePath, ".archiverFileData.json");
            if (File.Exists(savedJson))
            {
                using (var wtr = new StreamReader(savedJson))
                using (var json = new JsonTextReader(wtr))
                {
                    var serializer = new JsonSerializer();
                    var files = serializer.Deserialize<List<FileData>>(json);

                    return files;
                }
            }
            else
            {
                var files = GetFiles(_basePath, "").ToList();

                using (var wtr = new StreamWriter(savedJson))
                using (var json = new JsonTextWriter(wtr))
                {
                    var serializer = new JsonSerializer();
                    serializer.Serialize(json, files);
                }

                return files;
            }
        }

        private static IEnumerable<FileData> GetFiles(string root, string relativePathPrefix)
        {
            var basePath = Path.GetFullPath(root);
            var baseDir = new DirectoryInfo(basePath);
            return baseDir
                .EnumerateFiles("*", SearchOption.TopDirectoryOnly).Select(x => new FileData(basePath, x.FullName, relativePathPrefix))
                    .Concat(baseDir.EnumerateDirectories()
                    .AsParallel()
                    .SelectMany(x => x.EnumerateFiles("*", SearchOption.AllDirectories).Select(y => new FileData(basePath, y.FullName, relativePathPrefix)))
                );
        }

        private static IEnumerable<FileData> GetFiles(string tempDir, Uri url, string relativePathPrefix)
        {
            var zipFile = Path.Combine(tempDir, "data.zip");
            var expandedDir = Path.Combine(tempDir, "expanded");
            Directory.CreateDirectory(expandedDir);

            using (var http = new WebClient())
            {
                http.DownloadFile(url, zipFile);
            }

            ZipFile.ExtractToDirectory(zipFile, expandedDir, true);

            return GetFiles(expandedDir, relativePathPrefix);
        }

        private static void ProcessFiles(string type, List<FileData> files, long maxDiscSizeInBytes)
        {
            var tempDir = Path.GetTempFileName();
            if (File.Exists(tempDir)) File.Delete(tempDir);
            Directory.CreateDirectory(tempDir);

            try
            {
                DeDuplicateFiles(files);

                Log($"Processing {files.Count} unique {type} archive files");
                var extraFiles = FindExtraFiles(tempDir);
                var metaDataLength = DetermineMetaDataLength(files);
                var discs = SplitIntoDiscs(Path.Combine(_destinationPath, $"{type}Archive"), files, extraFiles.Sum(x => x.Size) + metaDataLength, maxDiscSizeInBytes).ToList();
                var metaData = GenerateMetaData(discs, tempDir);

                foreach (var disc in discs)
                {
                    Log($"    Creating disc {disc.DiscNumber} of {discs.Count}");
                    Log("        Adding source, binary, html, and meta-data files");
                    AddFilesToDiscFolder(disc, extraFiles.Concat(new[] { metaData }), false);
                    WriteDiscMetaData(disc);

                    var action = _copyOnly ? "Copy" : "Mov";
                    Log($"        {action}ing {disc.Files.Count} content files");
                    AddFilesToDiscFolder(disc, disc.Files, !_copyOnly);
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
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

        private static List<FileData> FindExtraFiles(string tempDir)
        {
            var extraFiles = new List<FileData>();

            // Add all the code binary files
            extraFiles.AddRange(GetFiles(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath), _metaDataFolder + "\\binaries\\"));

            // Add all files from the source code
            extraFiles.AddRange(GetFiles(Path.Combine(tempDir, "source"), _sourceCodePath, _metaDataFolder + "\\source\\"));

            // Add the HTML files
            extraFiles.AddRange(GetFiles(Path.Combine(tempDir, "html"), _browserHtmlPath, ""));

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

        private static FileData GenerateMetaData(List<DiscData> discs, string tempDir)
        {
            // Record each file name, and if it has a JSON file, also record the title, when it was taken (photoTakenTime/creationTime), where it was taken (geoData/geoDataExif), its description, and which people are in it
            var metaDir = Path.Combine(tempDir, _metaDataFolder);
            var tempFile = Path.Combine(metaDir, "metaData.js");

            using (var str = File.OpenWrite(tempFile))
                GenerateMetaData(discs, str);

            return new FileData(tempDir, tempFile, "");
        }

        private static long DetermineMetaDataLength(List<FileData> files)
        {
            var discs = new List<DiscData> { new DiscData(_destinationPath, 1, files) };

            using (var str = new MemoryStream())
            {
                GenerateMetaData(discs, str);
                return str.Position;
            }
        }

        private static void GenerateMetaData(List<DiscData> discs, Stream outputStream)
        {
            var uniqueId = 0L;

            using (var wtr = new StreamWriter(outputStream, leaveOpen: true))
            {
                wtr.Write("window.archiverMetaData = ");

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
                    json.WriteValue("Date Taken");
                    json.WritePropertyName("l");
                    json.WriteValue("Location Coordinates");
                    json.WritePropertyName("x");
                    json.WriteValue("Description/Notes");
                    json.WritePropertyName("p");
                    json.WriteValue("People");
                    json.WritePropertyName("c");
                    json.WriteValue("Archival Disc Number");
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

                        json.WritePropertyName("c");
                        json.WriteValue(discFile.Disc.DiscNumber);
                        json.WriteEndObject();
                    }

                    json.WriteEndArray();
                    json.WriteEndObject();

                    json.Flush();
                }
            }
        }

        private static void WriteDiscMetaData(DiscData disc)
        {
            if (_test) return;

            using (var wtr = new StreamWriter(Path.Combine(disc.Path, _metaDataFolder, "discMetaData.js")))
            {
                wtr.Write("window.archiverDiscMetaData = ");
                using (var json = new JsonTextWriter(wtr))
                {
                    json.WriteStartObject();
                    json.WritePropertyName("discNumber");
                    json.WriteValue(disc.DiscNumber);
                    json.WriteEndObject();
                    json.Flush();
                }
            }
        }

        private static void AddFilesToDiscFolder(DiscData disc, IEnumerable<FileData> files, bool moveFiles)
        {
            if (_test)
            {
                if (moveFiles) Log("            Skipping moving because we are in test mode");
                else Log("            Not copying because we are in test mode");
                return;
            }

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

                            if (moveFiles) File.Move(path.Full, dest, true);
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
