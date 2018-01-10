﻿using ECMA2Yaml.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Microsoft.OpenPublishing.FileAbstractLayer;
using Path = System.IO.Path;

namespace ECMA2Yaml
{
    public partial class ECMALoader
    {
        private FilterStore LoadFilters(string path)
        {
            var filterFile = Path.Combine(path, "_filter.xml");
            if (_fileAccessor.Exists(filterFile))
            {
                var filterStore = new FilterStore();
                XDocument filterDoc = XDocument.Parse(_fileAccessor.ReadAllText(filterFile));
                var attrFilter = filterDoc.Root.Element("attributeFilter");
                if (attrFilter != null && attrFilter.Attribute("apply").Value == "true")
                {
                    var attrFilterElements = attrFilter.Elements("namespaceFilter");
                    if (attrFilterElements != null)
                    {
                        filterStore.AttributeFilters = new List<AttributeFilter>();
                        foreach (var fElement in attrFilterElements)
                        {
                            AttributeFilter filter = new AttributeFilter()
                            {
                                Namespace = fElement.Attribute("name").Value,
                                TypeFilters = new Dictionary<string, bool>(),
                                DefaultValue = true
                            };
                            foreach (var tFiler in fElement.Elements("typeFilter"))
                            {
                                bool expose = false;
                                bool.TryParse(tFiler.Attribute("expose").Value, out expose);
                                string name = tFiler.Attribute("name").Value;
                                if (name == "*")
                                {
                                    filter.DefaultValue = expose;
                                }
                                else
                                {
                                    filter.TypeFilters[name] = expose;
                                }
                            }
                            filterStore.AttributeFilters.Add(filter);
                        }
                    }
                }
                var apiFilter = filterDoc.Root.Element("apiFilter");
                if (apiFilter != null && apiFilter.Attribute("apply").Value == "true")
                {
                    var apiFilterElements = apiFilter.Elements("namespaceFilter");
                    if (apiFilterElements != null)
                    {
                        filterStore.TypeFilters = new List<TypeFilter>();
                        filterStore.MemberFilters = new List<MemberFilter>();
                        foreach (var fElement in apiFilterElements)
                        {
                            var nsName = fElement.Attribute("name").Value?.Trim();
                            foreach(var tElement in fElement.Elements("typeFilter"))
                            {
                                var tFilter = new TypeFilter(tElement)
                                {
                                    Namespace = nsName
                                };
                                filterStore.TypeFilters.Add(tFilter);

                                var memberFilterElements = tElement.Elements("memberFilter");
                                if (memberFilterElements != null)
                                {
                                    foreach(var mElement in memberFilterElements)
                                    {
                                        filterStore.MemberFilters.Add(new MemberFilter(mElement)
                                        {
                                            Parent = tFilter
                                        });
                                    }
                                }
                            }
                            
                        }
                    }
                }
                return filterStore;
            }
            
            return null;
        }

        private Dictionary<string, List<string>> LoadFrameworks(string folder)
        {
            var frameworkFolderGlob = Path.Combine(folder, "FrameworksIndex/*.xml");
            Dictionary<string, List<string>> frameworks = new Dictionary<string, List<string>>();
            foreach (var fxFile in ListFiles(frameworkFolderGlob))
            {
                XDocument fxDoc = XDocument.Load(fxFile.AbsolutePath);
                var fxName = fxDoc.Root.Attribute("Name").Value;
                foreach (var nsElement in fxDoc.Root.Elements("Namespace"))
                {
                    var ns = nsElement.Attribute("Name").Value;
                    frameworks.AddWithKey(ns, fxName);
                    foreach (var tElement in nsElement.Elements("Type"))
                    {
                        var t = tElement.Attribute("Id").Value;
                        frameworks.AddWithKey(t, fxName);
                        foreach (var mElement in tElement.Elements("Member"))
                        {
                            var m = mElement.Attribute("Id").Value;
                            frameworks.AddWithKey(m, fxName);
                        }
                    }
                }
            }

            return frameworks;
        }

        private Dictionary<string, string> LoadMonikerPackageMapping(string folder)
        {
            var file = Path.Combine(folder, "_moniker2nuget.json");
            if (_fileAccessor.Exists(file))
            {
                try
                {
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(_fileAccessor.ReadAllText(file));
                }
                catch(Exception ex)
                {
                    OPSLogger.LogUserError("Unable to load moniker to nuget mapping: " + ex.ToString(), file);
                    return null;
                }
            }
            return null;
        }

        private Dictionary<string, List<string>> LoadMonikerAssemblyMapping(string folder)
        {
            var file = Path.Combine(folder, "_moniker2Assembly.json");
            if (_fileAccessor.Exists(file))
            {
                try
                {
                    return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(_fileAccessor.ReadAllText(file));
                }
                catch (Exception ex)
                {
                    OPSLogger.LogUserError("Unable to load moniker to assembly mapping: " + ex.ToString(), file);
                    return null;
                }
            }
            return null;
        }

        private List<ExtensionMethod> LoadExtensionMethods(string path)
        {
            var indexFile = Path.Combine(path, "index.xml");
            if (!_fileAccessor.Exists(indexFile))
            {
                return null;
            }

            var extensionMethods = new List<ExtensionMethod>();
            XDocument idxDoc = XDocument.Parse(_fileAccessor.ReadAllText(indexFile));
            var emElements = idxDoc?.Root?.Element("ExtensionMethods")?.Elements("ExtensionMethod");
            if (emElements != null)
            {
                foreach(var em in emElements)
                {
                    extensionMethods.Add(new ExtensionMethod()
                    {
                        TargetDocId = em.Element("Targets").Element("Target").Attribute("Type").Value,
                        MemberDocId = em.Element("Member").Element("Link").Attribute("Member").Value,
                        ParentTypeString = em.Element("Member").Element("Link").Attribute("Type").Value
                    });
                }
            }
            
            return extensionMethods;
        }

        private IEnumerable<FileItem> ListFiles(string glob)
        {
            return _fileAccessor.ListFiles(new string[] { glob });
        }
    }
}
