﻿using ECMA2Yaml.Models;
using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.DataContracts.ManagedReference;
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
                    LoadAndConvert(opt);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex.ToString());
                OPSLogger.LogSystemError(ex.ToString());
            }
            finally
            {
                OPSLogger.Flush(opt.LogFilePath);
            }
        }

        static void LoadAndConvert(CommandLineOptions opt)
        {
            ECMALoader loader = new ECMALoader();
            WriteLine("Loading ECMAXML files...");
            var store = loader.LoadFolder(opt.SourceFolder);
            if (store == null)
            {
                return;
            }

            WriteLine("Building loaded files...");
            if (!string.IsNullOrEmpty(opt.RepoRootPath) && !string.IsNullOrEmpty(opt.GitBaseUrl))
            {
                store.TranslateSourceLocation(opt.RepoRootPath, opt.GitBaseUrl);
            }
            store.Build();
            
            WriteLine("Loaded {0} namespaces.", store.Namespaces.Count);
            WriteLine("Loaded {0} types.", store.TypesByFullName.Count);
            WriteLine("Loaded {0} members.", store.MembersByUid.Count);

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
                    WriteLine("Applied metadata overwrite for {0} types", typeCount);
                }
            }

            WriteLine("Writing Yaml files...");
            ConcurrentDictionary<string, string> fileMapping = new ConcurrentDictionary<string, string>();
            ParallelOptions po = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
            Parallel.ForEach(store.Namespaces, po, ns =>
            {
                var nsFolder = Path.Combine(opt.OutputFolder, ns.Key);
                var nsFileName = Path.Combine(opt.OutputFolder, ns.Key + ".yml");
                if (ns.Value.Metadata.ContainsKey(OPSMetadata.XMLLocalPath))
                {
                    fileMapping.TryAdd(ns.Value.Metadata[OPSMetadata.XMLLocalPath].ToString(), nsFileName);
                }
                YamlUtility.Serialize(nsFileName, nsPages[ns.Key], YamlMime.ManagedReference);
                
                if (!opt.Flatten)
                {
                    if (!Directory.Exists(nsFolder))
                    {
                        Directory.CreateDirectory(nsFolder);
                    }
                }

                foreach (var t in store.Namespaces[ns.Key].Types)
                {
                    var typePage = typePages[t.Uid];
                    var tFileName = Path.Combine(opt.Flatten ? opt.OutputFolder : nsFolder, t.Uid.Replace('`', '-') + ".yml");
                    if (t.Metadata.ContainsKey(OPSMetadata.XMLLocalPath))
                    {
                        fileMapping.TryAdd(t.Metadata[OPSMetadata.XMLLocalPath].ToString(), tFileName);
                    }
                    YamlUtility.Serialize(tFileName, typePage, YamlMime.ManagedReference);
                }
            });
            YamlUtility.Serialize(Path.Combine(opt.OutputFolder, "toc.yml"), TOCGenerator.Generate(store), YamlMime.TableOfContent);
            var mappingFolder = string.IsNullOrEmpty(opt.LogFilePath) ? opt.OutputFolder : Path.GetDirectoryName(opt.LogFilePath);
            JsonUtility.Serialize(Path.Combine(mappingFolder, "XmlYamlMapping.json"), fileMapping, Newtonsoft.Json.Formatting.Indented);
            WriteLine("Done writing Yaml files.");
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
                                item.Metadata.Add(mtaPair.Key, mtaPair.Value);
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
