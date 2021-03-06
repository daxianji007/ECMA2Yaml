﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ECMA2Yaml.Models
{
    public class Parameter
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string OriginalTypeString { get; set; }
        public string RefType { get; set; }
        public string Description { get; set; }
        public string Index { get; set; }
        public string[] FrameworkAlternate { get; set; }

        public static Parameter FromXElement(XElement p)
        {
            if (p == null)
            {
                return null;
            }
            var typeStr = p.Attribute("Type")?.Value;
            return new Parameter
            {
                Name = p.Attribute("Name")?.Value,
                Type = typeStr?.TrimEnd('&'),
                OriginalTypeString = typeStr,
                RefType = p.Attribute("RefType")?.Value,
                Index = p.Attribute("Index")?.Value,
                FrameworkAlternate = p.Attribute("FrameworkAlternate")?.Value?.Split(';')
            };
        }
    }
}
