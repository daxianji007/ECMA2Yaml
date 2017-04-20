﻿using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.DataContracts.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ECMA2Yaml
{
    public class TOCMerger
    {
        public const string ChildrenMetadata = "children";

        public static void Merge(string topLevelTOCPath, string refTOCPath)
        {
            var topTOC = YamlUtility.Deserialize<TocViewModel>(topLevelTOCPath);
            var refTOC = YamlUtility.Deserialize<TocViewModel>(refTOCPath);
            var refTOCDict = refTOC.ToDictionary(t => t.Name);
            Stack<TocItemViewModel> itemsToGo = new Stack<TocItemViewModel>();
            topTOC.ForEach(t => itemsToGo.Push(t));
            while (itemsToGo.Count > 0)
            {
                var item = itemsToGo.Pop();
                if (item.Items != null)
                {
                    item.Items.ForEach(t => itemsToGo.Push(t));
                }
                if (item.Metadata != null && item.Metadata.ContainsKey(ChildrenMetadata))
                {
                    var children = (List<object>)item.Metadata[ChildrenMetadata];
                    foreach(var child in children.Cast<string>())
                    {
                        var regex = WildCardToRegex(child);
                        var matched = refTOCDict.Keys.Where(key => regex.IsMatch(key)).ToList();
                        if (matched.Count > 0)
                        {
                            if (item.Items == null)
                            {
                                item.Items = new TocViewModel();
                            }
                            foreach(var match in matched)
                            {
                                item.Items.Add(refTOCDict[match]);
                                refTOCDict.Remove(match);
                            }
                        }
                        else
                        {
                            OPSLogger.LogUserError(string.Format("Children pattern {0} cannot match any sub TOC", child), topLevelTOCPath);
                        }
                    }
                    item.Metadata.Remove(ChildrenMetadata);
                }
            }
            if (refTOCDict.Count > 0)
            {
                foreach(var remainingItem in refTOCDict.Values)
                {
                    topTOC.Add(remainingItem);
                }
            }

            YamlUtility.Serialize(refTOCPath, topTOC);
        }

        private static Regex WildCardToRegex(String value)
        {
            return new Regex("^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$", RegexOptions.Compiled);
        }
    }
}