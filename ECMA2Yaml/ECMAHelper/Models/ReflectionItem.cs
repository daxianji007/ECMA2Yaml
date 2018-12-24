﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECMA2Yaml.Models
{
    public enum ItemType
    {
        Default,
        Toc,
        Assembly,
        Namespace,
        Class,
        Interface,
        Struct,
        Delegate,
        Enum,
        Field,
        Property,
        Event,
        Constructor,
        Method,
        Operator,
        Container,
        AttachedEvent,
        AttachedProperty
    }

    public abstract class ReflectionItem
    {
        private string _id;
        private string _uid;
        public string Name { get; set; }
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
                _uid = null;
            }
        }
        public ItemType ItemType { get; set; }
        public string Uid
        {
            get
            {
                if (string.IsNullOrEmpty(_uid))
                {
                    _uid = (string.IsNullOrEmpty(Parent?.Uid)) ? Id : (Parent.Uid + "." + Id);
                }
                return _uid;
            }
        }
        public string DocId { get; set; }
        public string CommentId
        {
            get
            {
                if (!string.IsNullOrEmpty(Uid))
                {

                    if (Uid.EndsWith("*") && string.IsNullOrEmpty(DocId))
                    {
                        return "Overload:" + Uid.Trim('*');
                    }
                    if (!string.IsNullOrEmpty(DocId) && DocId.Contains(':'))
                    {
                        return DocId.Substring(0, DocId.IndexOf(':')) + ":" + Uid;
                    }
                    if (ItemType == ItemType.Namespace)
                    {
                        return "N:" + Uid;
                    }
                }
                return null;
            }
        }
        public List<Parameter> TypeParameters { get; set; }
        public List<Parameter> Parameters { get; set; }
        public Parameter ReturnValueType { get; set; }
        public Dictionary<string, string> Signatures { get; set; }
        public List<ECMAAttribute> Attributes { get; set; }
        public ReflectionItem Parent { get; set; }
        public Docs Docs { get; set; }
        public string SourceFileLocalPath { get; set; }

        public Dictionary<string, object> Metadata { get; set; }
        public SortedList<string, List<string>> Modifiers { get; set; }
        public bool? IsStatic
        {
            get
            {
                if (Modifiers != null && Modifiers.ContainsKey("csharp"))
                {
                    return Modifiers["csharp"].Contains("static");
                }
                return null;
            }
        }
        public List<AssemblyInfo> AssemblyInfo { get; set; }
        public VersionedProperty<AssemblyInfo> VersionedAssemblyInfo { get; set; }

        public ReflectionItem()
        {
            Metadata = new Dictionary<string, object>();
        }

        public abstract void Build(ECMAStore store);
    }
}
