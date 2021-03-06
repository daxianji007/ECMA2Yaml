﻿using ECMA2Yaml.Models;
using ECMA2Yaml.Models.SDP;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECMA2Yaml
{
    public partial class SDPYamlConverter
    {
        public DelegateSDPModel FormatDelegate(Type t)
        {
            var sdpDelegate = InitWithBasicProperties<DelegateSDPModel>(t);

            sdpDelegate.TypeParameters = ConvertTypeParameters(t);
            sdpDelegate.Inheritances = t.InheritanceUids?.Select(uid => UidToTypeMDString(uid, _store)).ToList(); ;

            if (t.ReturnValueType != null
                && !string.IsNullOrEmpty(t.ReturnValueType.Type)
                && t.ReturnValueType.Type != "System.Void"
                && t.ItemType != ItemType.Event)
            {
                sdpDelegate.Returns = ConvertParameter<TypeReference>(t.ReturnValueType);
            }

            sdpDelegate.Parameters = t.Parameters?.Select(p =>
            {
                var r = ConvertParameter<ParameterReference>(p, t.TypeParameters, showGenericType:false);
                r.Name = p.Name;
                return r;
            });

            if (t.Attributes != null
                && t.Attributes.Any(attr => attr.Declaration == "System.CLSCompliant(false)"))
            {
                sdpDelegate.IsNotClsCompliant = true;
            }
            sdpDelegate.AltCompliant = t.Docs.AltCompliant.ResolveCommentId(_store)?.Uid;

            if (t.ExtensionMethods?.Count > 0)
            {
                sdpDelegate.ExtensionMethods = t.ExtensionMethods.Select(im => ConvertTypeMemberLink(null, _store.MembersByUid[im])).ToList();
            }

            return sdpDelegate;
        }
    }
}
