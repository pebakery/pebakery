/*
    Copyright (C) 2018-2022 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    #region class UpdateJson
    /// <summary>
    /// Manages operations with update json file (project.index, [script].meta.json)
    /// </summary>
    /// <remarks>
    /// Remember, do not add constructors to sub-classes!
    /// Newtonsoft.Json expects that deserialized class either have a default constructor, one constructor with arguments or a constructor marked with the JsonConstructor attribute.
    /// Adding any constructors may confuse the library's serializer.
    /// </remarks>
    public class UpdateJson
    {
        #region enum UpdateKind
        public enum UpdateKind
        {
            FileIndex = 1,
            ScriptMeta = 2,
        }

        public enum IndexEntryKind
        {
            Folder = 1,
            Script = 2,
            NonScriptFile = 3,
        }

        public enum ScriptFormat
        {
            /// <summary>
            /// Ini-based format (which originated from WinBuilder days)
            /// </summary>
            IniBased = 1,
        }
        #endregion

        #region Methods
        public static Task ReadUpdateJsonAsync(string destJsonFile)
        {
            return Task.Run(() => ReadUpdateJson(destJsonFile));
        }

        public static ResultReport<Root> ReadUpdateJson(string metaJsonFile)
        {
            // Prepare JsonSerializer
            JsonSerializer serializer = CreateJsonSerializer();

            // Read json file
            Root? jsonRoot;
            try
            {
                // Use UTF-8 without a BOM signature, as the file was served by a web server
                using (StreamReader sr = new StreamReader(metaJsonFile, new UTF8Encoding(false), false))
                using (JsonTextReader jr = new JsonTextReader(sr))
                {
                    jsonRoot = serializer.Deserialize<Root>(jr);
                }
            }
            catch (JsonException e)
            {
                return new ResultReport<Root>(false, null, $"Update json file is corrupted: {Logger.LogExceptionMessage(e)}");
            }

            if (jsonRoot == null)
                return new ResultReport<Root>(false, null, $"Update json file is corrupted: deserialize failure");

            // Validate json instance
            ResultReport report = jsonRoot.Validate();
            if (!report.Success)
                return new ResultReport<Root>(false, null, report.Message);

            return new ResultReport<Root>(true, jsonRoot);
        }

        public static Task CreateProjectUpdateJsonAsync(Project p, string destJsonFile)
        {
            return Task.Run(() => CreateProjectUpdateJson(p, destJsonFile));
        }

        /// <summary>
        /// Create an update json of a project
        /// </summary>
        /// <remarks>
        /// Currently supports only "ini_based" script format
        /// </remarks>
        /// <param name="p">Target project</param>
        /// <param name="destJsonFile">update json file to create</param>
        public static void CreateProjectUpdateJson(Project p, string destJsonFile)
        {
            Root jsonRoot = Root.CreateInstance(p.ProjectRoot);
            WriteToJson(jsonRoot, destJsonFile);
        }

        public static Task CreateScriptUpdateJsonAsync(Script sc, string destJsonFile)
        {
            return Task.Run(() => CreateScriptUpdateJson(sc, destJsonFile));
        }

        /// <summary>
        /// Create an update json of a script
        /// </summary>
        /// <remarks>
        /// Currently supports only "ini_based" script format
        /// </remarks>
        /// <param name="sc">Target script</param>
        /// <param name="destJsonFile">update json file to create</param>
        public static void CreateScriptUpdateJson(Script sc, string destJsonFile)
        {
            Root jsonRoot = Root.CreateInstance(sc);
            WriteToJson(jsonRoot, destJsonFile);
        }

        public static Task CreatePathUpdateJsonAsync(string path, string destJsonFile)
        {
            return Task.Run(() => CreatePathUpdateJson(path, destJsonFile));
        }

        /// <summary>
        /// Create an update json of a project
        /// </summary>
        /// <remarks>
        /// Currently supports only "ini_based" script format
        /// </remarks>
        /// <param name="p">Target project</param>
        /// <param name="destJsonFile">update json file to create</param>
        public static void CreatePathUpdateJson(string path, string destJsonFile)
        {
            Root jsonRoot = Root.CreateInstance(path);
            WriteToJson(jsonRoot, destJsonFile);
        }

        private static void WriteToJson(Root jsonRoot, string destJsonFile)
        {
            // Check sanity (Debug only)
            Debug.Assert(jsonRoot.Validate().Success, $"Internal error at {nameof(UpdateJson)}.{nameof(WriteToJson)}");

            // Prepare JsonSerializer
            JsonSerializer serializer = CreateJsonSerializer();

            // Use UTF-8 without a BOM signature, as the file is going to be served in a web server
            using (StreamWriter sw = new StreamWriter(destJsonFile, false, new UTF8Encoding(false)))
            using (JsonTextWriter jw = new JsonTextWriter(sw))
            {
                jw.Formatting = Formatting.Indented;
                jw.Indentation = 2;
                serializer.Serialize(jw, jsonRoot);
            }
        }

        private static JsonSerializer CreateJsonSerializer()
        {
            // https://www.newtonsoft.com/json/help/html/ReducingSerializedJSONSize.htm
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new VersionExJsonConverter());
            settings.Converters.Add(new StringEnumConverter(new SnakeCaseNamingStrategy()));
            return JsonSerializer.Create(settings);
        }
        #endregion

        #region Root
        public class Root
        {
            #region Properties
            /// <summary>
            /// UpdateJson Schema version
            /// </summary>
            [JsonProperty(PropertyName = "schema_ver")]
            public VersionEx SchemaVer { get; set; } = new VersionEx(0, 0);
            /// <summary>
            /// Minimum required PEBakery version
            /// </summary>
            [JsonProperty(PropertyName = "pebakery_min_ver")]
            public VersionEx PEBakeryMinVer { get; set; } = new VersionEx(0, 0);

            /// <summary>
            /// Json creation time in UTC
            /// </summary>
            [JsonProperty(PropertyName = "created_at")]
            public DateTime CreatedAt { get; set; }
            [JsonProperty(PropertyName = "index")]
            public FileIndex Index { get; set; } = new FileIndex();
            #endregion

            #region CreateInstance
            private static Root CreateInstanceBase()
            {
                return new Root()
                {
                    SchemaVer = Global.Const.UpdateSchemaMaxVerInst,
                    PEBakeryMinVer = Global.Const.ProgramVersionInst,
                    CreatedAt = DateTime.UtcNow,
                };
            }

            public static Root CreateInstance(string path)
            {
                Root root = CreateInstanceBase();
                root.Index = FileIndex.CreateInstance(path);
                return root;
            }

            public static Root CreateInstance(Script sc)
            {
                Root root = CreateInstanceBase();
                root.Index = FileIndex.CreateInstance(sc);
                return root;
            }
            #endregion

            #region Validate
            /// <summary>
            /// Return true if schema is valid
            /// </summary>
            /// <returns>True if valid</returns>
            public ResultReport Validate()
            {
                // Check if properties are not null
                if (SchemaVer == null || PEBakeryMinVer == null)
                    return new ResultReport(false, "Update json is corrupted");

                // Check schema_ver and pebakery_min_ver
                if (Global.Const.UpdateSchemaMaxVerInst < SchemaVer)
                    return new ResultReport(false, "Requires newer version of PEBakery");
                if (SchemaVer < Global.Const.UpdateSchemaMinVerInst)
                    return new ResultReport(false, "Update json is too old to be read by PEbakery");
                if (Global.Const.ProgramVersionInst < PEBakeryMinVer)
                    return new ResultReport(false, $"Requires PEBakery {Global.Const.ProgramVersionStr} or higher");

                // Check created_at
                if (CreatedAt.Equals(DateTime.MinValue))
                    return new ResultReport(false, "Update json is corrupted");

                // Check file index
                if (Index == null)
                    return new ResultReport(false, "Update json is corrupted");
                ResultReport report = Index.Validate();
                if (!report.Success)
                    return report;

                return new ResultReport(true);
            }
            #endregion
        }
        #endregion

        #region FileIndex
        public class FileIndex
        {
            #region Properties
            /// <summary>
            /// Kind of the entry
            /// </summary>
            [JsonProperty(PropertyName = "kind")]
            public IndexEntryKind Kind { get; set; }
            /// <summary>
            /// Filename or foldername
            /// </summary>
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// Valid for EntryType.Folder
            /// </summary>
            [JsonProperty(PropertyName = "children")]
            public List<FileIndex>? Children { get; set; }
            /// <summary>
            /// Valid for EntryType.Script and EntryType.NonScriptFile
            /// </summary>
            [JsonProperty(PropertyName = "file_metadata")]
            public FileMetadata? FileMetadata { get; set; }
            /// <summary>
            /// Valid for EntryType.Script
            /// </summary>
            [JsonProperty(PropertyName = "script_info")]
            public ScriptInfo? ScriptInfo { get; set; }
            #endregion

            #region CreateInstance
            public static FileIndex CreateInstance(string path)
            {
                if (Directory.Exists(path))
                {
                    DirectoryInfo di = new DirectoryInfo(path);
                    return CreateInstance(di);
                }
                else if (File.Exists(path))
                {
                    FileInfo fi = new FileInfo(path);
                    return CreateInstance(fi);
                }
                else
                {
                    throw new ArgumentException($"Path [{path}] does not exist", nameof(path));
                }
            }

            public static FileIndex CreateInstance(Script sc)
            {
                return new FileIndex
                {
                    Kind = IndexEntryKind.Script,
                    Name = Path.GetFileName(sc.DirectRealPath),
                    FileMetadata = FileMetadata.CreateInstance(sc.DirectRealPath),
                    ScriptInfo = ScriptInfo.CreateInstance(sc),
                };
            }

            private static FileIndex CreateInstance(DirectoryInfo rootDir)
            {
                FileIndex rootIndex = new FileIndex
                {
                    Kind = IndexEntryKind.Folder,
                    Name = rootDir.Name,
                    Children = new List<FileIndex>(),
                };

                Queue<(FileIndex, DirectoryInfo)> dirQueue = new Queue<(FileIndex, DirectoryInfo)>();
                dirQueue.Enqueue((rootIndex, rootDir));
                do
                {
                    (FileIndex dirIndex, DirectoryInfo di) = dirQueue.Dequeue();
                    if (dirIndex.Children is null)
                        throw new InvalidOperationException($"{nameof(dirIndex.Children)} is null, even though it is Folder");

                    foreach (DirectoryInfo subDir in di.GetDirectories())
                    {
                        FileIndex subIndex = new FileIndex
                        {
                            Kind = IndexEntryKind.Folder,
                            Name = subDir.Name,
                            Children = new List<FileIndex>(),
                        };
                        dirIndex.Children.Add(subIndex);
                        dirQueue.Enqueue((subIndex, subDir));
                    }
                    foreach (FileInfo fi in di.GetFiles())
                    {
                        FileIndex subIndex = CreateInstance(fi);
                        dirIndex.Children.Add(subIndex);
                    }
                }
                while (0 < dirQueue.Count);

                return rootIndex;
            }

            private static FileIndex CreateInstance(FileInfo fi)
            {
                return new FileIndex
                {
                    Kind = IndexEntryKind.NonScriptFile,
                    Name = fi.Name,
                    FileMetadata = FileMetadata.CreateInstance(fi.FullName),
                };
            }
            #endregion

            #region Validate
            /// <summary>
            /// Recursively check sanity of the FileIndex and its children. Return true if a schema is valid.
            /// </summary>
            /// <returns>Return true if a schema is valid.</returns>
            public ResultReport Validate()
            {
                Queue<FileIndex> q = new Queue<FileIndex>();
                q.Enqueue(this);
                do
                {
                    FileIndex fi = q.Dequeue();

                    // Check if properties are not null
                    if (fi.Name == null)
                        return new ResultReport(false, "File index is corrupted");

                    // Check filename / foldername
                    if (!FileHelper.CheckWin32Path(Name, false, false))
                        return new ResultReport(false, "File index is corrupted");

                    // Check per kind
                    switch (fi.Kind)
                    {
                        case IndexEntryKind.Folder:
                            {
                                if (Children == null)
                                    return new ResultReport(false, $"Children of folder [{Name}] is corrupted");

                                foreach (FileIndex sub in Children)
                                    q.Enqueue(sub);
                            }
                            break;
                        case IndexEntryKind.Script:
                            {
                                // Check null
                                if (FileMetadata == null)
                                    return new ResultReport(false, $"Metadata of script [{Name}] is corrupted");
                                if (ScriptInfo == null)
                                    return new ResultReport(false, $"Information of script [{Name}] is corrupted");

                                // Check file metadata
                                ResultReport report = FileMetadata.Validate();
                                if (!report.Success)
                                    return report;
                                // Check script information
                                report = ScriptInfo.Validate();
                                if (!report.Success)
                                    return report;
                            }
                            break;
                        case IndexEntryKind.NonScriptFile:
                            {
                                if (FileMetadata == null)
                                    return new ResultReport(false, $"Metadata of script {Name} is corrupted");

                                // Check file metadata
                                ResultReport report = FileMetadata.Validate();
                                if (!report.Success)
                                    return report;
                            }
                            break;
                    }
                }
                while (0 < q.Count);

                return new ResultReport(true);
            }
            #endregion
        }
        #endregion

        #region FileMetadata
        public class FileMetadata
        {
            #region Properties
            /// <summary>
            /// Last modified time in UTC.
            /// </summary>
            [JsonProperty(PropertyName = "updated_at")]
            public DateTime UpdatedAt { get; set; }
            /// <summary>
            /// Size of the file.
            /// </summary>
            [JsonProperty(PropertyName = "file_size")]
            public long FileSize { get; set; }
            /// <summary>
            /// SHA256 hash of the file.
            /// </summary>
            [JsonProperty(PropertyName = "sha256")]
            public byte[] Sha256 { get; set; } = Array.Empty<byte>();
            #endregion

            #region CreateInstance
            public static FileMetadata CreateInstance(string targetFile)
            {
                // Calculate SHA256 of the file
                byte[] hashDigest = HashHelper.GetHash(HashType.SHA256, targetFile);

                // Get UpdatedAt and FileSize
                FileInfo fi = new FileInfo(targetFile);

                // Create an instance of FileMeta
                return new FileMetadata
                {
                    UpdatedAt = fi.LastWriteTimeUtc,
                    FileSize = fi.Length,
                    Sha256 = hashDigest,
                };
            }
            #endregion

            #region Validate
            /// <summary>
            /// Return true if a schema is valid
            /// </summary>
            /// <returns>True if valid</returns>
            public ResultReport Validate()
            {
                // Check if properties are not null
                if (Sha256 == null)
                    return new ResultReport(false, "File metadata is corrupted");

                if (UpdatedAt.Equals(DateTime.MinValue))
                    return new ResultReport(false, "File metadata is corrupted");

                if (Sha256.Length != HashHelper.HashLenDict[HashType.SHA256])
                    return new ResultReport(false, "File metadata is corrupted");

                return new ResultReport(true);
            }
            #endregion

            #region VerifyFile
            public ResultReport VerifyFile(string targetFile)
            {
                // Check the file size first, as this check is more faster
                FileInfo fi = new FileInfo(targetFile);
                if (fi.Length != FileSize)
                    return new ResultReport(false, "File size of the file is different");

                // Check the SHA256 hash laster, as this check is more slower
                // Avoid using LINQ SequenceEqual for maximum performance (34% faster)
                byte[] targetDigest = HashHelper.GetHash(HashType.SHA256, targetFile);
                if (Sha256.Length != targetDigest.Length) // Failing to ensure this will result in out-of-bound exception
                    return new ResultReport(false, "Hash of the file is corrupted");
                for (int i = 0; i < targetDigest.Length; i++)
                {
                    if (Sha256[i] != targetDigest[i])
                        return new ResultReport(false, "Hash of the file is corrupted");
                }

                return new ResultReport(true);
            }
            #endregion
        }
        #endregion

        #region ScriptInfo
        public class ScriptInfo
        {
            #region Properties
            [JsonProperty(PropertyName = "format")]
            public ScriptFormat Format { get; set; }

            /// <summary>
            /// Script information of the IniBased format
            /// </summary>
            [JsonProperty(PropertyName = "ini_based")]
            public IniBasedScript? IniBased { get; set; }
            #endregion

            #region CreateInstance
            public static ScriptInfo CreateInstance(Script sc)
            {
                return new ScriptInfo
                {
                    // Currently only supports Winbuilder format
                    Format = ScriptFormat.IniBased,
                    IniBased = IniBasedScript.CreateInstance(sc),
                };
            }
            #endregion

            #region Validate
            /// <summary>
            /// Return true if a schema is valid
            /// </summary>
            /// <returns>True if valid</returns>
            public ResultReport Validate()
            {
                if (!Enum.IsDefined(typeof(ScriptFormat), Format))
                    return new ResultReport(false, $"Not supported script format {Format}");

                switch (Format)
                {
                    case ScriptFormat.IniBased:
                        if (IniBased == null)
                            return new ResultReport(false, $"Unable to find script information");
                        ResultReport report = IniBased.Validate();
                        if (!report.Success)
                            return report;
                        break;
                    default:
                        return new ResultReport(false, $"Internal error at {nameof(UpdateJson)}.{nameof(ScriptInfo)}.{nameof(Validate)}");
                }

                return new ResultReport(true);
            }
            #endregion
        }
        #endregion

        #region IniBasedScript
        public class IniBasedScript
        {
            #region Properties
            [JsonProperty(PropertyName = "title")]
            public string Title { get; set; } = string.Empty;
            [JsonProperty(PropertyName = "desc")]
            public string Desc { get; set; } = string.Empty;
            [JsonProperty(PropertyName = "author")]
            public string Author { get; set; } = string.Empty;
            [JsonProperty(PropertyName = "version")]
            public VersionEx Version { get; set; } = new VersionEx(0, 0);
            #endregion

            #region CreateInstance
            public static IniBasedScript CreateInstance(Script sc)
            {
                return new IniBasedScript
                {
                    Title = sc.Title,
                    Desc = sc.Description,
                    Author = sc.Author,
                    Version = sc.ParsedVersion,
                };
            }
            #endregion

            #region Validate
            /// <summary>
            /// Return true if a schema is valid
            /// </summary>
            /// <returns></returns>
            public ResultReport Validate()
            {
                // Check if properties are not null
                if (Title == null || Desc == null || Author == null || Version == null)
                    return new ResultReport(false, "Ini-based script information is corrupted");

                return new ResultReport(true);
            }
            #endregion
        }
        #endregion
    }
    #endregion
}

