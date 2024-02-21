// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using SwiftReflector.ExceptionTools;
using SwiftReflector.SwiftXmlReflection;
using System.IO;
using SyntaxDynamo.CSLang;
using SwiftRuntimeLibrary;

namespace SwiftReflector.TypeMapping
{
    public class CSKeywords
    {
        static HashSet<string> keyWords;
        static string[] keyArr = new string[] {
            "abstract", "as", "base", "bool", "break", "byte",
            "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do",
            "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach",
            "goto", "if", "implicit", "in", "int", "interface", "internal",
            "is", "lock", "long", "namespace", "new", "null", "object",
            "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
            "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
            "add", "alias", "ascending", "async", "await", "descending", "dynamic",
            "from", "get", "global", "group", "into", "join", "let", "orderby",
            "partial", "remove", "select", "set", "value", "var", "where", "yield"
        };
        static CSKeywords()
        {
            keyWords = new HashSet<string>();
            foreach (string s in keyArr)
            {
                keyWords.Add(s);
            }
        }

        public static bool IsKeyword(string s)
        {
            ArgumentNullException.ThrowIfNull(s, nameof(s));
            return keyWords.Contains(s);
        }
    }

    public class TypeMapper
    {
        public TypeDatabase TypeDatabase { get; private set; }
        UnicodeMapper unicodeMapper;
        HashSet<string> loadedFiles = new HashSet<string>();

        public TypeMapper(List<string> typeDatabasePaths, UnicodeMapper unicodeMapper)
        {
            TypeDatabase = new TypeDatabase();
            this.unicodeMapper = unicodeMapper;

            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string xmlFilePath = Path.Combine(basePath, "SwiftCore.xml");
            AddTypeDatabase(xmlFilePath);
        }

        public void AddTypeDatabase(string fileName)
        {
            if (loadedFiles.Contains(fileName))
                return;
            loadedFiles.Add(fileName);
            var errors = TypeDatabase.Read(fileName);
            if (errors.AnyErrors)
                throw new AggregateException(errors.Errors.Select((v) => v.Exception));
        }

        public Entity GetEntityForSwiftClassName(string fullyQualifiedName)
        {
            return TypeDatabase.EntityForSwiftName(fullyQualifiedName);
        }

        public Entity TryGetEntityForSwiftClassName(string fullyQualifiedName)
        {
            return TypeDatabase.TryGetEntityForSwiftName(fullyQualifiedName);
        }

        public Entity GetEntityForTypeSpec(TypeSpec spec)
        {
            if (spec == null)
                return null;
            var ns = spec as NamedTypeSpec;
            if (ns == null || spec.IsDynamicSelf)
                return null;
            return GetEntityForSwiftClassName(ns.Name);
        }

        public EntityType GetEntityTypeForTypeSpec(TypeSpec spec)
        {
            if (spec == null)
                return EntityType.None;

            if (spec is NamedTypeSpec)
            {
                if (spec.IsDynamicSelf)
                    return EntityType.DynamicSelf;
                var ent = GetEntityForTypeSpec(spec);
                return ent != null ? ent.EntityType : EntityType.None;
            }
            if (spec is TupleTypeSpec)
            {
                return EntityType.Tuple;
            }
            if (spec is ClosureTypeSpec)
            {
                return EntityType.Closure;
            }
            if (spec is ProtocolListTypeSpec)
            {
                return EntityType.ProtocolList;
            }
            throw new ArgumentOutOfRangeException(spec.GetType().Name);
        }

        static string ComeUpWithAName(string given, string typeName, int index)
        {
            if (String.IsNullOrEmpty(given))
            {
                return String.Format("{0}{1}", Char.ToLower(typeName[0]), index);
            }
            else
            {
                return given;
            }
        }

        static NetParam ToNamedParam(ParameterItem st, NetTypeBundle bundle, int index)
        {
            string targetName = ComeUpWithAName(st.NameIsRequired ? st.PublicName : st.PrivateName, bundle.Type, index);
            return new NetParam(targetName, bundle);
        }

        static bool IsUnique(NetParam p, List<NetParam> pl, int startPoint)
        {
            for (int i = startPoint; i < pl.Count; i++)
            {
                if (p.Name == pl[i].Name)
                    return false;
            }
            return true;
        }

        static NetParam Uniquify(NetParam p, List<NetParam> pl, int startPoint)
        {
            while (!IsUnique(p, pl, startPoint) || CSKeywords.IsKeyword(p.Name))
            {
                p = new NetParam(p.Name + '0', p.Type);
            }
            return p;
        }

        static List<NetParam> SanitizeParamNames(List<NetParam> parms)
        {
            List<NetParam> outlist = new List<NetParam>();
            bool changed = false;
            do
            {
                changed = false;
                outlist = new List<NetParam>();
                for (int i = 0; i < parms.Count; i++)
                {
                    NetParam q = Uniquify(parms[i], parms, i + 1);
                    changed = q.Name != parms[i].Name;
                    outlist.Add(q);
                }
                if (changed)
                    parms = outlist;
            } while (changed);
            return outlist;
        }

        NetTypeBundle RecastToReference(BaseDeclaration typeContext, NetTypeBundle netBundle, TypeSpec theElem, bool structsAndEnumsAreAlwaysRefs)
        {
            var entity = GetEntityForTypeSpec(theElem);
            if (entity != null && (entity.IsObjCStruct || entity.IsObjCEnum) && netBundle.IsReference)
                return netBundle;
            if (theElem.IsInOut && netBundle.Entity != EntityType.Struct)
                return netBundle;
            var objcStructType = ObjCStructOrEnumReferenceType(typeContext, theElem as NamedTypeSpec);
            if (objcStructType != null)
            {
                return MapType(typeContext, objcStructType, true);
            }
            if (entity != null && entity.EntityType == EntityType.Protocol && !entity.IsObjCProtocol)
            {
                return new NetTypeBundle(netBundle.NameSpace, netBundle.Type, netBundle.IsScalar, true, netBundle.Entity);
            }
            return netBundle;
        }


        NetParam MapParameterItem(BaseDeclaration context, ParameterItem parameter, int index, bool isPinvoke, bool structsAndEnumsAreAlwaysRefs)
        {
            var theType = parameter.TypeSpec;
            var t = MapType(context, theType, isPinvoke);
            if (isPinvoke)
            {
                t = RecastToReference(context, t, theType, structsAndEnumsAreAlwaysRefs);
            }
            return ToNamedParam(parameter, t, index);
        }

        public List<NetParam> MapParameterList(BaseDeclaration context, List<ParameterItem> st, bool isPinvoke,
            bool structsAndEnumsAreAlwaysRefs, CSGenericTypeDeclarationCollection extraProtocolTypes,
            CSGenericConstraintCollection extraProtocolContstraints, CSUsingPackages use)
        {
            var parms = new List<NetParam>();
            for (int i = 0; i < st.Count; i++)
            {
                    var entity = GetEntityForTypeSpec(st[i].TypeSpec);
                    parms.Add(MapParameterItem(context, st[i], i, isPinvoke, structsAndEnumsAreAlwaysRefs));
            }
            return SanitizeParamNames(parms);
        }

        public NetTypeBundle MapType(BaseDeclaration context, TypeSpec spec, bool isPinvoke, bool isReturnValue = false,
            Tuple<int, int> selfDepthIndex = null)
        {
            switch (spec.Kind)
            {
                case TypeSpecKind.Named:
                    var named = (NamedTypeSpec)spec;
                    if (IsScalar(named.Name))
                    {
                        return ToScalar(named.Name, spec.IsInOut);
                    }
                    else
                    {
                        Entity en = TypeDatabase.EntityForSwiftName(named.Name);
                        if (en != null)
                        {
                            if (isPinvoke)
                            {
                                switch (en.EntityType)
                                {
                                    case EntityType.Scalar:
                                        return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, spec.IsInOut, en.EntityType);
                                    default:
                                        throw ErrorHelper.CreateError(ReflectorError.kCantHappenBase + 21, "Can't happen - shouldn't ever get to this case in type mapping.");
                                }
                            }
                            else
                            {
                                var retval = new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, spec.IsInOut, en.EntityType);
                                return retval;
                            }
                        }
                        else
                        {
                            if (isPinvoke)
                            {
                                var namedSpec = spec as NamedTypeSpec;
                                if (namedSpec != null)
                                {
                                    if (TypeMapper.IsSwiftPointerType(namedSpec.Name))
                                    {
                                        return new NetTypeBundle("System", "IntPtr", false, false, EntityType.None);
                                    }
                                }
                            }
                            throw ErrorHelper.CreateError(ReflectorError.kTypeMapBase + 0, $"Unable to find C# reference for swift class '{spec.ToString()}'.");
                        }
                    }
                case TypeSpecKind.Tuple:
                    var tuple = (TupleTypeSpec)spec;
                    if (tuple.Elements.Count == 0)
                        return NetTypeBundle.Void;
                    var tupTypes = tuple.Elements.Select(ts => MapType(context, ts, isPinvoke)).ToList();
                    return new NetTypeBundle(tupTypes, tuple.IsInOut);
                default:
                    throw new NotImplementedException();
            }
        }

        TypeSpec ObjCStructOrEnumReferenceType(BaseDeclaration context, NamedTypeSpec spec)
        {
            var boundType = GetBoundPointerType(spec);
            if (boundType == null)
                return null;
            if (context.IsTypeSpecGenericReference(boundType))
                return null;
            if (context.IsProtocolWithAssociatedTypesFullPath(boundType, this))
                return null;
            var entity = TypeDatabase.EntityForSwiftName(boundType.Name);
            return entity.IsObjCStruct || entity.IsObjCEnum ? boundType : null;
        }

        static NamedTypeSpec GetBoundPointerType(NamedTypeSpec spec)
        {
            if (spec == null)
                return null;
            if (spec.Name != "Swift.UnsafePointer" && spec.Name != "Swift.UnsafeMutablePointer")
                return null;
            var boundType = spec.GenericParameters[0] as NamedTypeSpec;
            return boundType;
        }

        NetTypeBundle ToScalar(SwiftBuiltInType bit)
        {
            switch (bit.BuiltInType)
            {
                case CoreBuiltInType.Bool:
                    return ToScalar("Swift.Bool", bit.IsReference);
                case CoreBuiltInType.Double:
                    return ToScalar("Swift.Double", bit.IsReference);
                case CoreBuiltInType.Float:
                    return ToScalar("Swift.Float", bit.IsReference);
                case CoreBuiltInType.Int:
                    return new NetTypeBundle("System", "nint", true, bit.IsReference, EntityType.Scalar);
                case CoreBuiltInType.UInt:
                    return new NetTypeBundle("System", "nuint", true, bit.IsReference, EntityType.Scalar);
                default:
                    throw new ArgumentOutOfRangeException(nameof(bit));
            }
        }

        static string[] scalarNames = new string[] {
            "Swift.Bool",
            "Swift.Double",
            "Swift.Float",
            "Swift.Int",
            "Swift.UInt",
            "Swift.Int8",
            "Swift.UInt8",
            "Swift.Int16",
            "Swift.UInt16",
            "Swift.Int32",
            "Swift.UInt32",
            "Swift.Int64",
            "Swift.UInt64"
        };

        static int ScalarIndex(string builtIn)
        {
            return Array.IndexOf(scalarNames, builtIn);
        }

        public static bool IsScalar(string builtIn)
        {
            ArgumentNullException.ThrowIfNull(builtIn, nameof(builtIn));
            return ScalarIndex(builtIn) >= 0;
        }

        public static bool IsScalar(TypeSpec spec)
        {
            var ns = spec as NamedTypeSpec;
            if (ns == null)
                return false;
            return ScalarIndex(ns.Name) >= 0;
        }

        NetTypeBundle ToScalar(string builtIn, bool isReference)
        {
            int index = ScalarIndex(builtIn);
            if (index >= 0)
            {
                var en = TypeDatabase.EntityForSwiftName(builtIn);
                if (en != null)
                {
                    return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, isReference, en.EntityType);
                }
            }
            throw new ArgumentOutOfRangeException(nameof(builtIn));
        }

        public static bool IsSwiftPointerType(string fullTypeName)
        {
            return fullTypeName == "Swift.UnsafePointer" || fullTypeName == "Swift.UnsafeMutablePointer"
                || fullTypeName == "Swift.UnsafeRawPointer" || fullTypeName == "Swift.UnsafeMutableRawPointer";
        }
    }
}

