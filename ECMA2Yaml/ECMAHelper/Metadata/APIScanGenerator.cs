﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ECMA2Yaml.Models;
using ECMA2Yaml.Models.SDP;
using Microsoft.DocAsCode.Plugins;

namespace ECMA2Yaml
{
    public sealed class ApiScanGenerator
    {
        private static readonly IDictionary<ItemType, Func<ReflectionItem, IEnumerable<string>>> ApiNameMapper = new Dictionary<ItemType, Func<ReflectionItem, IEnumerable<string>>>
        {
            { ItemType.Class, item => GetTypeApiNamesCore(item, string.Empty) },
            { ItemType.Struct, item => GetTypeApiNamesCore(item, string.Empty) },
            { ItemType.Interface, item => GetTypeApiNamesCore(item, string.Empty) },
            { ItemType.Enum, item => GetTypeApiNamesCore(item, string.Empty) },
            { ItemType.Delegate, item => GetTypeApiNamesCore(item, string.Empty, "..ctor", ".Invoke", ".BeginInvoke", ".EndInvoke") },
            { ItemType.Constructor, item => GenerateMemberApiNames(item, ".") },
            { ItemType.Method, item => GenerateMemberApiNames(item, ".") },
            { ItemType.Field, item => GenerateMemberApiNames(item, ".") },
            { ItemType.Operator, item => GenerateMemberApiNames(item, ".", ".op_") },
            { ItemType.Property, item => GenerateMemberApiNames(item, ".", new Separator(".get_", i => i.Modifiers?.Count > 0 && i.Modifiers["csharp"].Contains("get")), new Separator(".set_", i => i.Modifiers?.Count > 0 && i.Modifiers["csharp"].Contains("set"))) },
            { ItemType.AttachedProperty, item => GenerateMemberApiNames(item, ".", new Separator(".get_", i => i.Modifiers?.Count > 0 && i.Modifiers["csharp"].Contains("get")), new Separator(".set_", i => i.Modifiers?.Count > 0 && i.Modifiers["csharp"].Contains("set"))) },
            { ItemType.Event, item => GenerateMemberApiNames(item, ".", ".add_", ".remove_") },
            { ItemType.AttachedEvent, item => GenerateMemberApiNames(item, ".", ".add_", ".remove_") },
        };

        private static readonly Regex TypeFormatter = new Regex("<[^<>]+>", RegexOptions.Compiled);

        private const string APISCAN_APINAME = "api_name";
        private const string APISCAN_APILOCATION = "api_location";
        private const string APISCAN_TOPICTYPE = "topic_type";
        private const string APISCAN_APITYPE = "api_type";

        public static void Generate(ItemSDPModelBase model, ReflectionItem item)
        {
            var apiNames = GetApiNames(item).ToList();
            if (apiNames.Count > 0)
            {
                if (!model.Metadata.ContainsKey(APISCAN_APINAME))
                {
                    model.Metadata[APISCAN_APINAME] = apiNames;
                }
                if (!model.Metadata.ContainsKey(APISCAN_APILOCATION))
                {
                    model.Metadata[APISCAN_APILOCATION] = model.Assemblies.Select(a => a + ".dll").ToList();
                }
                if (!model.Metadata.ContainsKey(APISCAN_TOPICTYPE))
                {
                    model.Metadata[APISCAN_TOPICTYPE] = new List<string> { "apiref" };
                }
                if (!model.Metadata.ContainsKey(APISCAN_APITYPE))
                {
                    model.Metadata[APISCAN_APITYPE] = new List<string> { "Assembly" };
                }
            }
        }

        private static IEnumerable<string> GetApiNames(ReflectionItem item)
        {
            if (ApiNameMapper.TryGetValue(item.ItemType, out Func<ReflectionItem, IEnumerable<string>> func))
            {
                return func(item);
            }

            return Enumerable.Empty<string>();
        }

        private static IEnumerable<string> GetTypeApiNamesCore(ReflectionItem item, params Separator[] separators)
        {
            string type = item.Uid;
            foreach (var separator in separators)
            {
                if (separator.Condition(item))
                {
                    yield return $"{type}{separator}";
                }
            }
        }

        private static IEnumerable<string> GenerateMemberApiNames(ReflectionItem item, params Separator[] separators)
        {
            foreach (var separator in separators)
            {
                if (separator.Condition(item))
                {
                    yield return $"{item.Parent.Uid}{separator}{item.Name}";
                }
            }
        }

        internal class Separator
        {
            public Func<ReflectionItem, bool> Condition { get; set; }

            public string Value { get; set; }

            public Separator(string value = "", Func<ReflectionItem, bool> func = null)
            {
                Value = value;
                Condition = func ?? (model => true);
            }

            public static implicit operator Separator(string value)
            {
                return new Separator(value);
            }

            public override string ToString()
            {
                return Value;
            }
        }
    }
}