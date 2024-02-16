// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SwiftReflector.Demangling
{
    public class Node
    {
        Node(NodeKind kind, PayloadKind payload)
        {
            Kind = kind;
            PayloadKind = payload;
            Children = new List<Node>();
        }

        public Node(NodeKind kind)
            : this(kind, PayloadKind.None)
        {
        }

        public Node(NodeKind kind, string text)
            : this(kind, PayloadKind.Text)
        {
            stringPayload = text;
        }

        public Node(NodeKind kind, long index)
            : this(kind, PayloadKind.Index)
        {
            indexPayload = index;
        }

        string stringPayload = null;
        long indexPayload = 0;

        public List<Node> Children { get; private set; }

        public NodeKind Kind { get; private set; }
        public PayloadKind PayloadKind { get; private set; }

        public bool HasText { get { return PayloadKind == PayloadKind.Text; } }
        public string Text
        {
            get
            {
                if (!HasText)
                    throw new InvalidOperationException($"Expected a text payload, but this has a {PayloadKind} payload");
                return stringPayload;
            }
        }

        public bool HasIndex { get { return PayloadKind == PayloadKind.Index; } }
        public long Index
        {
            get
            {
                if (!HasIndex)
                    throw new InvalidOperationException($"Expected an index payload, but this has a {PayloadKind} payload");
                return indexPayload;
            }
        }

        public void AddChild(Node child)
        {
            Children.Add(child);
        }

        public void RemoveChildAt(int pos)
        {
            Children.RemoveAt(pos);
        }

        public void ReverseChildren(int startingAt = 0)
        {
            var last = Children.Count - 1;
            if (startingAt < 0 || startingAt > Children.Count)
                throw new ArgumentOutOfRangeException(nameof(startingAt));
            while (startingAt < last)
            {
                Node temp = Children[startingAt];
                Children[startingAt] = Children[last];
                Children[last] = temp;
                startingAt++;
                last--;
            }
        }


        public static bool IsDeclName(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Identifier:
                case NodeKind.LocalDeclName:
                case NodeKind.PrivateDeclName:
                case NodeKind.RelatedEntityDeclName:
                case NodeKind.PrefixOperator:
                case NodeKind.PostfixOperator:
                case NodeKind.InfixOperator:
                case NodeKind.TypeSymbolicReference:
                case NodeKind.ProtocolSymbolicReference:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsContext(NodeKind kind)
        {
            var type = typeof(NodeKind);
            var memberInfo = type.GetMember(kind.ToString());
            var attrs = memberInfo[0].GetCustomAttributes(typeof(ContextAttribute), false);
            return attrs != null && attrs.Length == 1;
        }

        public static bool IsAnyGeneric(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Structure:
                case NodeKind.Class:
                case NodeKind.Enum:
                case NodeKind.Protocol:
                case NodeKind.ProtocolSymbolicReference:
                case NodeKind.OtherNominalType:
                case NodeKind.TypeAlias:
                case NodeKind.TypeSymbolicReference:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsNominal(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Structure:
                case NodeKind.Class:
                case NodeKind.Enum:
                case NodeKind.Protocol:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsEntity(NodeKind kind)
        {
            if (kind == NodeKind.Type)
                return true;
            return IsContext(kind);
        }

        public static bool IsRequirement(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.DependentGenericSameTypeRequirement:
                case NodeKind.DependentGenericLayoutRequirement:
                case NodeKind.DependentGenericConformanceRequirement:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsFunctionAttribute(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.FunctionSignatureSpecialization:
                case NodeKind.GenericSpecialization:
                case NodeKind.InlinedGenericFunction:
                case NodeKind.GenericSpecializationNotReAbstracted:
                case NodeKind.GenericPartialSpecialization:
                case NodeKind.GenericPartialSpecializationNotReAbstracted:
                case NodeKind.ObjCAttribute:
                case NodeKind.NonObjCAttribute:
                case NodeKind.DynamicAttribute:
                case NodeKind.DirectMethodReferenceAttribute:
                case NodeKind.VTableAttribute:
                case NodeKind.PartialApplyForwarder:
                case NodeKind.PartialApplyObjCForwarder:
                case NodeKind.OutlinedVariable:
                case NodeKind.OutlinedBridgedMethod:
                case NodeKind.MergedFunction:
                case NodeKind.DynamicallyReplaceableFunctionImpl:
                case NodeKind.DynamicallyReplaceableFunctionKey:
                case NodeKind.DynamicallyReplaceableFunctionVar:
                    return true;
                default:
                    return false;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            ToString(0, sb);
            return sb.ToString();
        }

        void ToString(int indent, StringBuilder sb)
        {
            for (int i = 0; i < indent; i++)
            {
                sb.Append(' ');
            }
            sb.Append("->").Append(Kind.ToString());
            switch (PayloadKind)
            {
                case PayloadKind.None:
                    sb.Append(Environment.NewLine);
                    break;
                case PayloadKind.Index:
                    sb.Append($" ({Index})\n");
                    break;
                case PayloadKind.Text:
                    sb.Append($" (\"{Text}\")\n");
                    break;
            }
            foreach (var node in Children)
            {
                node.ToString(indent + 2, sb);
            }
        }

        public bool IsAttribute()
        {
            switch (Kind)
            {
                case NodeKind.ObjCAttribute:
                case NodeKind.DynamicAttribute:
                case NodeKind.NonObjCAttribute:
                case NodeKind.ImplFunctionAttribute:
                case NodeKind.DirectMethodReferenceAttribute:
                    return true;
                default:
                    return false;
            }
        }

        public SwiftTypeAttribute ExtractAttribute()
        {
            switch (Kind)
            {
                case NodeKind.ObjCAttribute: return SwiftTypeAttribute.ObjC;
                case NodeKind.DynamicAttribute: return SwiftTypeAttribute.Dynamic;
                case NodeKind.NonObjCAttribute: return SwiftTypeAttribute.NonObjC;
                case NodeKind.ImplFunctionAttribute: return SwiftTypeAttribute.ImplFunction;
                case NodeKind.DirectMethodReferenceAttribute: return SwiftTypeAttribute.DirectMethodReference;
                default:
                    throw new NotSupportedException($"{Kind} is not an attribute");
            }
        }
    }
}
