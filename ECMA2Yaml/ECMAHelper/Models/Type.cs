﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ECMA2Yaml.Models
{
    public class Type : ReflectionItem
    {
        public string FullName { get; set; }
        public BaseType BaseType { get; set; }
        public List<string> InheritanceUids { get; set; }
        public Dictionary<string, string> InheritedMembers { get; set; }
        public List<string> IsA { get; set; }
        public List<string> Interfaces { get; set; }
        public List<Member> Members { get; set; }
        public List<Member> Overloads { get; set; }
        public List<string> ExtensionMethods { get; set; }
        public Dictionary<string, object> ExtendedMetadata { get; set; }
        private static Regex GenericRegex = new Regex("<[^<>]+>", RegexOptions.Compiled);

        public override void Build(ECMAStore store)
        {
            if (string.IsNullOrEmpty(Id))
            {
                Id = Name.Replace('+', '.');
                if (Id.Contains('<'))
                {
                    Id = GenericRegex.Replace(Id, match => "`" + (match.Value.Count(c => c == ',') + 1));
                }
            }
        }
    }
}
