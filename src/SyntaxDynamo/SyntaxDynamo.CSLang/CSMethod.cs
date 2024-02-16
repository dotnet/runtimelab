// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SyntaxDynamo;

namespace SyntaxDynamo.CSLang
{
    public class CSMethod : CodeElementCollection<ICodeElement>
    {
        public CSMethod(CSVisibility vis, CSMethodKind kind, CSType type, CSIdentifier name, CSParameterList parms, CSCodeBlock body)
            : this(vis, kind, type, name, parms, null, false, body)
        {

        }
        public CSMethod(CSVisibility vis, CSMethodKind kind, CSType type, CSIdentifier name,
                 CSParameterList parms, CSBaseExpression[] baseOrThisCallParms, bool callsBase, CSCodeBlock body, bool isSealed = false,
                 bool isAsync = false)
        {
            GenericParameters = new CSGenericTypeDeclarationCollection();
            GenericConstraints = new CSGenericConstraintCollection();
            Visibility = vis;
            Kind = kind;
            Type = type; // no throw on null - could be constructor
            Name = Exceptions.ThrowOnNull(name, nameof(name));
            Parameters = Exceptions.ThrowOnNull(parms, nameof(parms));
            CallsBase = callsBase;
            BaseOrThisCallParameters = baseOrThisCallParms;

            Body = body; // can be null
            IsSealed = isSealed;
            IsAsync = isAsync;

            LineCodeElementCollection<ICodeElement> lc = new LineCodeElementCollection<ICodeElement>(new ICodeElement[0], false, true);
            if (vis != CSVisibility.None)
            {
                lc.And(new SimpleElement(VisibilityToString(vis))).And(SimpleElement.Spacer);
            }

            if (isSealed)
            {
                lc.And(new SimpleElement("sealed")).And(SimpleElement.Spacer);
            }

            if (isAsync)
            {
                lc.And(new SimpleElement("async")).And(SimpleElement.Spacer);
            }

            lc.And(new SimpleElement(MethodKindToString(kind))).And(SimpleElement.Spacer);

            if (type != null)
            {
                lc.And(type).And(SimpleElement.Spacer);
            }

            lc.And(name).And(GenericParameters).And(new SimpleElement("(")).And(parms).And(new SimpleElement(")")).And(GenericConstraints);
            if (body == null)
            {
                if (!(kind == CSMethodKind.StaticExtern || kind == CSMethodKind.Interface))
                    throw new ArgumentException("Method body is only optional when method kind kind is either StaticExtern or Interface",
                                    nameof(body));
                lc.Add(new SimpleElement(";"));
            }
            Add(lc);
            if (BaseOrThisCallParameters != null)
            {
                Add(new CSFunctionCall(CallsBase ? ": base" : ": this", false, BaseOrThisCallParameters));
            }
            if (body != null)
                Add(body);
        }

        public CSVisibility Visibility { get; private set; }
        public CSMethodKind Kind { get; private set; }
        public CSType Type { get; private set; }
        public CSIdentifier Name { get; private set; }
        public CSParameterList Parameters { get; private set; }
        public bool CallsBase { get; private set; }
        public CSBaseExpression[] BaseOrThisCallParameters { get; private set; }
        public CSCodeBlock Body { get; private set; }
        public CSGenericTypeDeclarationCollection GenericParameters { get; private set; }
        public CSGenericConstraintCollection GenericConstraints { get; private set; }
        public bool IsSealed { get; private set; }
        public bool IsAsync { get; private set; }

        public CSMethod AsSealed()
        {
            var sealedMethod = new CSMethod(Visibility, Kind, Type, Name, Parameters, BaseOrThisCallParameters, CallsBase, Body, true);
            return CopyGenerics(this, sealedMethod);
        }

        public CSMethod AsOverride()
        {
            var overrideMethod = new CSMethod(Visibility, CSMethodKind.Override, Type, Name, Parameters, BaseOrThisCallParameters, CallsBase, Body, IsSealed);
            return CopyGenerics(this, overrideMethod);
        }

        public CSMethod AsPrivate()
        {
            var privateMethod = new CSMethod(CSVisibility.None, Kind, Type, Name, Parameters, BaseOrThisCallParameters, CallsBase, Body, IsSealed);
            return CopyGenerics(this, privateMethod);
        }

        public static CSMethod CopyGenerics(CSMethod from, CSMethod to)
        {
            to.GenericParameters.AddRange(from.GenericParameters);
            to.GenericConstraints.AddRange(from.GenericConstraints);
            return to;
        }

        public static CSMethod RemoveGenerics(CSMethod from)
        {
            var newMethod = new CSMethod(from.Visibility, from.Kind, from.Type, from.Name, from.Parameters, from.Body);
            return newMethod;
        }

        public static string VisibilityToString(CSVisibility visibility)
        {
            switch (visibility)
            {
                case CSVisibility.None:
                    return "";
                case CSVisibility.Internal:
                    return "internal";
                case CSVisibility.Private:
                    return "private";
                case CSVisibility.Public:
                    return "public";
                case CSVisibility.Protected:
                    return "protected";
                default:
                    throw new ArgumentOutOfRangeException("vis");
            }
        }

        public static string MethodKindToString(CSMethodKind kind)
        {
            switch (kind)
            {
                case CSMethodKind.None:
                case CSMethodKind.Interface:
                    return "";
                case CSMethodKind.Extern:
                    return "extern";
                case CSMethodKind.New:
                    return "new";
                case CSMethodKind.Override:
                    return "override";
                case CSMethodKind.Static:
                    return "static";
                case CSMethodKind.StaticExtern:
                    return "static extern";
                case CSMethodKind.StaticNew:
                    return "static new";
                case CSMethodKind.Virtual:
                    return "virtual";
                case CSMethodKind.Abstract:
                    return "abstract";
                case CSMethodKind.Unsafe:
                    return "unsafe";
                case CSMethodKind.StaticUnsafe:
                    return "static unsafe";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static CSMethod PublicMethod(CSType type, string name, CSParameterList parms, CSCodeBlock body)
        {
            return new CSMethod(CSVisibility.Public, CSMethodKind.None, type, new CSIdentifier(name), parms, Exceptions.ThrowOnNull(body, "body"));
        }

        public static CSMethod PublicMethod(CSMethodKind kind, CSType type, string name, CSParameterList parms, CSCodeBlock body)
        {
            return new CSMethod(CSVisibility.Public, kind, type, new CSIdentifier(name), parms, Exceptions.ThrowOnNull(body, "body"));
        }

        public static CSMethod PublicConstructor(string name, CSParameterList parms, CSCodeBlock body)
        {
            return new CSMethod(CSVisibility.Public, CSMethodKind.None, null, new CSIdentifier(name), parms, Exceptions.ThrowOnNull(body, "body"));
        }

        public static CSMethod PublicConstructor(string name, CSParameterList parms, CSCodeBlock body, params CSBaseExpression[] baseParams)
        {
            return new CSMethod(CSVisibility.Public, CSMethodKind.None, null, new CSIdentifier(name), parms,
                         baseParams, true, Exceptions.ThrowOnNull(body, "body"));
        }

        public static CSMethod PrivateConstructor(string name, CSParameterList parms, CSCodeBlock body)
        {
            return new CSMethod(CSVisibility.None, CSMethodKind.None, null, new CSIdentifier(name), parms, Exceptions.ThrowOnNull(body, "body"));
        }

        public static CSMethod PrivateConstructor(string name, CSParameterList parms, CSCodeBlock body, params CSBaseExpression[] baseParams)
        {
            return new CSMethod(CSVisibility.None, CSMethodKind.None, null, new CSIdentifier(name), parms,
                         baseParams, true, Exceptions.ThrowOnNull(body, "body"));
        }

        public static CSMethod PInvoke(CSVisibility vis, CSType type, string name, string dllName, string externName, CSParameterList parms)
        {
            CSMethod method = new CSMethod(vis, CSMethodKind.StaticExtern, Exceptions.ThrowOnNull(type, "type"),
                new CSIdentifier(name), parms, null);

            CSAttribute.DllImport(dllName, externName).AttachBefore(method);

            return method;
        }

        public static CSMethod PInvoke(CSVisibility vis, CSType type, string name, CSBaseExpression dllName, string externName, CSParameterList parms)
        {
            CSMethod method = new CSMethod(vis, CSMethodKind.StaticExtern, Exceptions.ThrowOnNull(type, "type"),
                new CSIdentifier(name), parms, null);

            CSAttribute.DllImport(dllName, externName).AttachBefore(method);

            return method;
        }

        public static CSMethod PublicPInvoke(CSType type, string name, string dllName, string externName, CSParameterList parms)
        {
            return PInvoke(CSVisibility.Public, type, name, dllName, externName, parms);
        }

        public static CSMethod PrivatePInvoke(CSType type, string name, string dllName, string externName, CSParameterList parms)
        {
            return PInvoke(CSVisibility.None, type, name, dllName, externName, parms);
        }

        public static CSMethod InternalPInvoke(CSType type, string name, string dllName, string externName, CSParameterList parms)
        {
            return PInvoke(CSVisibility.Internal, type, name, dllName, externName, parms);
        }


    }
}

