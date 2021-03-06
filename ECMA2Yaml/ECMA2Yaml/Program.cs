﻿using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.DataContracts.ManagedReference;
using Microsoft.OpenPublishing.FileAbstractLayer;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ECMA2Yaml
{
    class Program
    {
        static void Main(string[] args)
        {
            var opt = new CommandLineOptions();

            try
            {
                if (opt.Parse(args))
                {
                    if (!string.IsNullOrEmpty(opt.RepoRootPath))
                    {
                        OPSLogger.PathTrimPrefix = opt.RepoRootPath;
                    }
                    if (opt.MapMode)
                    {
                        MapFolder(opt);
                    }
                    else
                    {
                        LoadAndConvert(opt);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex.ToString());
                OPSLogger.LogSystemError(LogCode.ECMA2Yaml_InternalError, ex.ToString());
            }
            finally
            {
                OPSLogger.Flush(opt.LogFilePath);
            }
        }

        static void LoadAndConvert(CommandLineOptions opt)
        {
            var rootPath = Path.GetFullPath(opt.RepoRootPath ?? opt.SourceFolder);
            var xmlFolder = Path.GetFullPath(opt.SourceFolder).Replace(rootPath, "").Trim(Path.DirectorySeparatorChar);
            var fileAccessor = new FileAccessor(rootPath);
            ECMALoader loader = new ECMALoader(fileAccessor);
            WriteLine("Loading ECMAXML files...");
            var store = loader.LoadFolder(xmlFolder);
            if (store == null)
            {
                return;
            }
            store.StrictMode = opt.StrictMode;

            WriteLine("Building loaded files...");
            store.Build();
            if (!string.IsNullOrEmpty(opt.RepoRootPath) && !string.IsNullOrEmpty(opt.GitBaseUrl))
            {
                store.TranslateSourceLocation(opt.RepoRootPath, opt.GitBaseUrl);
            }
            
            WriteLine("Loaded {0} namespaces.", store.Namespaces.Count);
            WriteLine("Loaded {0} types.", store.TypesByFullName.Count);
            WriteLine("Loaded {0} members.", store.MembersByUid.Count);
            WriteLine("Loaded {0} extension methods.", store.ExtensionMethodsByMemberDocId?.Values?.Count ?? 0);
            WriteLine("Loaded {0} attribute filters.", store.FilterStore?.AttributeFilters?.Count ?? 0);

            if (!string.IsNullOrEmpty(opt.UndocumentedApiReport))
            {
                UndocumentedApi.ReportGenerator.GenerateReport(store, opt.UndocumentedApiReport.BackSlashToForwardSlash(), opt.CurrentBranch);
            }

            IDictionary<string, List<string>> fileMapping = null;
            if (opt.SDPMode)
            {
                fileMapping = SDPYamlGenerator.Generate(store, opt.OutputFolder, opt.Flatten);
                YamlUtility.Serialize(Path.Combine(opt.OutputFolder, "toc.yml"), SDPTOCGenerator.Generate(store), YamlMime.TableOfContent);
            }
            else
            {
                WriteLine("Generating Yaml models...");
                var nsPages = TopicGenerator.GenerateNamespacePages(store);
                var typePages = TopicGenerator.GenerateTypePages(store);

                if (!string.IsNullOrEmpty(opt.MetadataFolder))
                {
                    WriteLine("Loading metadata overwrite files...");
                    var metadataDict = YamlHeaderParser.LoadOverwriteMetadata(opt.MetadataFolder);
                    var nsCount = ApplyMetadata(nsPages, metadataDict);
                    if (nsCount > 0)
                    {
                        WriteLine("Applied metadata overwrite for {0} namespaces", nsCount);
                    }
                    var typeCount = ApplyMetadata(typePages, metadataDict);
                    if (typeCount > 0)
                    {
                        WriteLine("Applied metadata overwrite for {0} items", typeCount);
                    }
                }

                WriteLine("Writing Yaml files...");
                string overwriteFolder = Path.Combine(opt.OutputFolder, "overwrites");
                if (!Directory.Exists(overwriteFolder))
                {
                    Directory.CreateDirectory(overwriteFolder);
                }

                fileMapping = new ConcurrentDictionary<string, List<string>>();
                ParallelOptions po = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
                Parallel.ForEach(store.Namespaces, po, ns =>
                {
                    var nsFolder = Path.Combine(opt.OutputFolder, ns.Key);
                    if (!string.IsNullOrEmpty(ns.Key))
                    {
                        var nsFileName = Path.Combine(opt.OutputFolder, ns.Key + ".yml");
                        if (!string.IsNullOrEmpty(ns.Value.SourceFileLocalPath))
                        {
                            fileMapping.Add(ns.Value.SourceFileLocalPath, new List<string> { nsFileName });
                        }
                        YamlUtility.Serialize(nsFileName, nsPages[ns.Key], YamlMime.ManagedReference);
                    }

                    if (!opt.Flatten && !Directory.Exists(nsFolder))
                    {
                        Directory.CreateDirectory(nsFolder);
                    }

                    foreach (var t in ns.Value.Types)
                    {
                        var typePage = typePages[t.Uid];
                        var tFileName = Path.Combine(opt.Flatten ? opt.OutputFolder : nsFolder, t.Uid.Replace('`', '-') + ".yml");
                        if (!string.IsNullOrEmpty(t.SourceFileLocalPath))
                        {
                            fileMapping.Add(t.SourceFileLocalPath, new List<string> { tFileName });
                        }
                        YamlUtility.Serialize(tFileName, typePage, YamlMime.ManagedReference);
                        if (t.Overloads != null && t.Overloads.Any(o => o.Docs != null))
                        {
                            foreach (var overload in t.Overloads.Where(o => o.Docs != null))
                            {
                                YamlHeaderWriter.WriteOverload(overload, overwriteFolder);
                            }
                        }

                        YamlHeaderWriter.WriteCustomContentIfAny(t.Uid, t.Docs, overwriteFolder);
                        if (t.Members != null)
                        {
                            foreach (var m in t.Members)
                            {
                                YamlHeaderWriter.WriteCustomContentIfAny(m.Uid, m.Docs, overwriteFolder);
                            }
                        }
                    }
                });

                //Write TOC
                YamlUtility.Serialize(Path.Combine(opt.OutputFolder, "toc.yml"), TOCGenerator.Generate(store), YamlMime.TableOfContent);
            }

            //Translate change list or save mapping file
            if (opt.ChangeListFiles.Count > 0)
            {
                foreach(var changeList in opt.ChangeListFiles)
                {
                    if (File.Exists(changeList))
                    {
                        var count = ChangeListUpdater.TranslateChangeList(changeList, fileMapping);
                        WriteLine("Translated {0} file entries in {1}.", count, changeList);
                    }
                }
            }
            else
            {
                var mappingFolder = string.IsNullOrEmpty(opt.LogFilePath) ? opt.OutputFolder : Path.GetDirectoryName(opt.LogFilePath);
                JsonUtility.Serialize(Path.Combine(mappingFolder, "XmlYamlMapping.json"), fileMapping, Newtonsoft.Json.Formatting.Indented);
            }
            
            //Save fallback file list as skip publish
            if (!string.IsNullOrEmpty(opt.SkipPublishFilePath) && loader.FallbackFiles?.Count > 0)
            {
                List<string> list = new List<string>();
                if (File.Exists(opt.SkipPublishFilePath))
                {
                    list = JsonUtility.Deserialize<List<string>>(opt.SkipPublishFilePath);
                    WriteLine("Read {0} entries in {1}.", list.Count, opt.SkipPublishFilePath);
                }
                list.AddRange(loader.FallbackFiles
                    .Where(path => fileMapping.ContainsKey(path))
                    .SelectMany(path => fileMapping[path].Select(p => p.Replace(opt.RepoRootPath, "").TrimStart('\\')))
                    );
                JsonUtility.Serialize(opt.SkipPublishFilePath, list, Newtonsoft.Json.Formatting.Indented);
                WriteLine("Write {0} entries to {1}.", list.Count, opt.SkipPublishFilePath);
            }

            WriteLine("Done writing Yaml files.");
        }

        static void MapFolder(CommandLineOptions opt)
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(AssemblyLoader.CurrentDomain_ReflectionOnlyAssemblyResolve);
            var settings = new AppDomainSetup
            {
                ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
            };
            if (Directory.Exists(opt.SourceFolder))
            {
                List<Tuple<string, string>> MonikerAssemblyPairs = new List<Tuple<string, string>>();
                foreach (var monikerFolder in Directory.GetDirectories(opt.SourceFolder))
                {
                    var childDomain = AppDomain.CreateDomain(Guid.NewGuid().ToString(), null, settings);

                    var handle = Activator.CreateInstance(childDomain,
                               typeof(AssemblyLoader).Assembly.FullName,
                               typeof(AssemblyLoader).FullName,
                               false, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, null, CultureInfo.CurrentCulture, new object[0]);

                    var loader = (AssemblyLoader)handle.Unwrap();

                    //This operation is executed in the new AppDomain
                    var paths = loader.LoadExceptFacade(monikerFolder);
                    MonikerAssemblyPairs.AddRange(paths);

                    AppDomain.Unload(childDomain);
                }
                var moniker2Assembly = MonikerAssemblyPairs.GroupBy(t => t.Item1).ToDictionary(g => g.Key, g => g.Select(t => t.Item2).ToArray());
                if (moniker2Assembly != null)
                {
                    var targetFile = Path.Combine(opt.OutputFolder, "_moniker2Assembly.json");
                    File.WriteAllText(targetFile, JsonConvert.SerializeObject(moniker2Assembly, Formatting.Indented));
                }
            }
        }

        static int ApplyMetadata(Dictionary<string, PageViewModel> pages, Dictionary<string, Dictionary<string, object>> metadataDict)
        {
            int count = 0;
            foreach(var page in pages)
            {
                if (page.Value != null)
                {
                    foreach(var item in page.Value.Items)
                    {
                        if (metadataDict.ContainsKey(item.Uid))
                        {
                            if (item.Metadata == null)
                            {
                                item.Metadata = new Dictionary<string, object>();
                            }
                            foreach(var mtaPair in metadataDict[item.Uid])
                            {
                                if (mtaPair.Key == "langs" || mtaPair.Key == "dev_langs")
                                {
                                    item.SupportedLanguages = JsonUtility.FromJsonString<string[]>(mtaPair.Value.ToJsonString());
                                }
                                else
                                {
                                    item.Metadata[mtaPair.Key] = mtaPair.Value;
                                }
                            }
                            count++;
                        }
                    }
                    foreach (var item in page.Value.References)
                    {
                        if (item.Uid.EndsWith("*") && metadataDict.ContainsKey(item.Uid))
                        {
                            foreach (var mtaPair in metadataDict[item.Uid])
                            {
                                item.Additional.Add(mtaPair.Key, mtaPair.Value);
                            }
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        static void WriteLine(string format, params object[] args)
        {
            string timestamp = string.Format("[{0}]", DateTime.Now.ToString());
            Console.WriteLine(timestamp + string.Format(format, args));
        }
    }
}
