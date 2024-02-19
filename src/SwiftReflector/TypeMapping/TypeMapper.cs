// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using SwiftReflector.ExceptionTools;
using SwiftReflector.Inventory;
using SwiftReflector.SwiftXmlReflection;
using System.IO;
using SyntaxDynamo.CSLang;
using SwiftRuntimeLibrary;

namespace SwiftReflector.TypeMapping
{
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

        DotNetName PreRegisterEntityName(string swiftClassName, EntityType entityKind)
        {

            var en = TypeDatabase.EntityForSwiftName(swiftClassName);
            if (en != null)
                return en.GetFullType();


            var netClassName = MakeDotNetClassName(swiftClassName, entityKind == EntityType.Protocol);

            en = new Entity
            {
                SharpNamespace = netClassName.Namespace,
                SharpTypeName = netClassName.TypeName,
                Type = new ShamDeclaration(swiftClassName, entityKind),
                EntityType = entityKind
            };
            TypeDatabase.Add(en);
            return netClassName;
        }

        public bool IsRegistered(SwiftClassName cl)
        {
            return TypeDatabase.Contains(cl);
        }

        public bool IsRegistered(string fullyQualifiedName)
        {
            return TypeDatabase.Contains(fullyQualifiedName);
        }

        public DotNetName PreRegisterEntityName(SwiftClassName cl)
        {
            var swiftClassName = cl.ToFullyQualifiedName();
            var entity = EntityType.None;
            // FIXME
            if (cl.IsClass)
                entity = EntityType.Class;
            else if (cl.IsStruct)
                entity = EntityType.Struct;
            else if (cl.IsEnum)
                entity = EntityType.Enum;
            else
            {
                throw new NotImplementedException();
            }
            return PreRegisterEntityName(swiftClassName, entity);
        }

        public DotNetName GetDotNetNameForSwiftClassName(SwiftClassName cl)
        {
            return TypeDatabase.EntityForSwiftName(cl).GetFullType();
        }

        public DotNetName GetDotNetNameForTypeSpec(TypeSpec spec)
        {
            var named = spec as NamedTypeSpec;
            if (named == null)
                return null;
            return GetDotNetNameForSwiftClassName(named.Name);
        }

        public DotNetName GetDotNetNameForSwiftClassName(string fullyQualifiedName)
        {
            var entity = TypeDatabase.EntityForSwiftName(fullyQualifiedName);
            if (entity == null)
                return null;
            return entity.GetFullType();
        }

        public EntityType GetEntityTypeForSwiftClassName(string fullyQualifiedName)
        {
            var ent = TypeDatabase.EntityForSwiftName(fullyQualifiedName);
            return ent != null ? ent.EntityType : EntityType.None;
        }

        public EntityType GetEntityTypeForDotNetName(DotNetName netClassName)
        {
            var ent = TypeDatabase.EntityForDotNetName(netClassName);
            return ent != null ? ent.EntityType : EntityType.None;
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

        public Entity GetEntityForDotNetName(DotNetName netName)
        {
            return TypeDatabase.EntityForDotNetName(netName);
        }

        public DotNetName RegisterClass(TypeDeclaration t)
        {
            t = t.MakeUnrooted();
            var isProtocol = false;
            // FIXME
            var entity = EntityType.None;
            // don't reorder - until I change this, ProtocolDeclaration extends from ClassDeclaration
            if (t is ProtocolDeclaration)
            {
                entity = EntityType.Protocol;
                isProtocol = true;
            }
            else if (t is ClassDeclaration)
            {
                entity = EntityType.Class;
            }
            else if (t is StructDeclaration)
            {
                entity = EntityType.Struct;
            }
            else
            {
                EnumDeclaration eDecl = t as EnumDeclaration;
                entity = (eDecl.IsTrivial || (eDecl.IsIntegral && eDecl.IsHomogenous && eDecl.Inheritance.Count == 0)) ? EntityType.TrivialEnum : EntityType.Enum;
            }
            var sharpName = MakeDotNetClassName(t.ToFullyQualifiedName(true), isProtocol);

            var en = new Entity
            {
                SharpNamespace = sharpName.Namespace,
                SharpTypeName = sharpName.TypeName,
                Type = t,
                EntityType = entity
            };

            TypeDatabase.Update(en);
            return sharpName;
        }

        public IEnumerable<DotNetName> RegisterClasses(IEnumerable<TypeDeclaration> decl)
        {
            var allReg = new List<DotNetName>();
            foreach (TypeDeclaration cl in decl)
            {
                DotNetName reg = RegisterClass(cl);
                allReg.Add(reg);
            }
            return allReg;
        }

        DotNetName MakeDotNetClassName(string fullyQualifiedName, bool isProtocol)
        {
            var parts = fullyQualifiedName.Split('.');
            var sb = new StringBuilder();
            var namesp = MapModuleToNamespace(parts[0]);
            for (int i = 1; i < parts.Length; i++)
            {
                if (i > 1)
                    sb.Append('.');
                if (isProtocol && i == parts.Length - 1)
                    sb.Append('I');
                sb.Append(SanitizeIdentifier(parts[i]));
            }
            return new DotNetName(namesp, sb.ToString());
        }

        string MakeDotNetClassName(SwiftClassName name)
        {
            var sb = new StringBuilder();
            var namesp = MapModuleToNamespace(name.Module);
            sb.Append(namesp);
            foreach (SwiftName sn in name.NestingNames)
            {
                sb.Append('.');
                sb.Append(SanitizeIdentifier(sn.Name));
            }
            return sb.ToString();
        }

        public string MapModuleToNamespace(SwiftName name)
        {
            string finalName = null;
            finalName = UserModuleMap(name);
            if (finalName == null)
            {
                finalName = SanitizeIdentifier(name.Name);
            }
            return finalName;
        }

        public string MapModuleToNamespace(string name)
        {
            string finalName = null;
            finalName = UserModuleMap(name);
            if (finalName == null)
            {
                finalName = SanitizeIdentifier(name);
            }
            return finalName;
        }


        string UserModuleMap(SwiftName name)
        {
            return null;
        }

        string UserModuleMap(string name)
        {
            return null;
        }

        static UnicodeCategory[] validStarts = {
            UnicodeCategory.UppercaseLetter,
            UnicodeCategory.LowercaseLetter,
            UnicodeCategory.TitlecaseLetter,
            UnicodeCategory.ModifierLetter,
            UnicodeCategory.OtherLetter,
            UnicodeCategory.LetterNumber
        };

        static bool ValidIdentifierStart(UnicodeCategory cat)
        {
            return validStarts.Contains(cat);
        }

        static UnicodeCategory[] validContent = {
            UnicodeCategory.DecimalDigitNumber,
            UnicodeCategory.ConnectorPunctuation,
            UnicodeCategory.Format
        };

        static bool ValidIdentifierContent(UnicodeCategory cat)
        {
            return ValidIdentifierStart(cat) || validContent.Contains(cat);
        }

        static bool IsValidIdentifier(int position, UnicodeCategory cat)
        {
            if (position == 0)
                return ValidIdentifierStart(cat);
            else
                return ValidIdentifierContent(cat);
        }

        static bool IsHighUnicode(string s)
        {
            // Steve says: this is arbitrary, but it solves an issue
            // with mcs and csc not liking certain Ll and Lu class
            // unicode characters (for now).
            // Open issue: https://github.com/dotnet/roslyn/issues/27986
            var encoding = Encoding.UTF32;
            var bytes = encoding.GetBytes(s);
            var utf32Value = BitConverter.ToUInt32(bytes, 0);
            return utf32Value > 0xffff;
        }

        public string SanitizeIdentifier(string name)
        {
            var sb = new StringBuilder();

            var characterEnum = StringInfo.GetTextElementEnumerator(name);
            while (characterEnum.MoveNext())
            {
                string c = characterEnum.GetTextElement();
                int i = characterEnum.ElementIndex;

                var cat = CharUnicodeInfo.GetUnicodeCategory(name, i);

                if (IsValidIdentifier(i, cat) && !IsHighUnicode(c))
                    sb.Append(i == 0 && cat == UnicodeCategory.LowercaseLetter ? c.ToUpper() : c);
                else
                    sb.Append(unicodeMapper.MapToUnicodeName(c));
            }

            if (CSKeywords.IsKeyword(sb.ToString()))
                sb.Append('_');
            return sb.ToString();
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

        public List<string> GetLineage(SwiftUncurriedFunctionType uft)
        {
            var lineage = new List<string>();
            if (uft != null)
            {
                var theClass = uft.UncurriedParameter as SwiftClassType;
                if (theClass == null)
                {
                    var meta = uft.UncurriedParameter as SwiftMetaClassType;
                    if (meta == null)
                        throw ErrorHelper.CreateError(ReflectorError.kTypeMapBase + 17, $"Expected a SwiftClassType or a SwiftMetaClassType as the uncurried parameter in a function, but got {uft.UncurriedParameter.GetType().Name}.");
                    theClass = meta.Class;
                }
                var sb = new StringBuilder().Append(theClass.ClassName.Module.Name);
                foreach (SwiftName name in theClass.ClassName.NestingNames)
                {
                    sb.Append('.').Append(name.Name);
                    lineage.Add(sb.ToString());
                }
            }
            return lineage;
        }

        internal object GetEntityForTypeSpec(object inheritsTypeSpec)
        {
            throw new NotImplementedException();
        }


        NetTypeBundle RecastToReference(BaseDeclaration typeContext, NetTypeBundle netBundle, TypeSpec theElem, bool structsAndEnumsAreAlwaysRefs)
        {
            if (typeContext.IsTypeSpecGenericReference(theElem) || typeContext.IsProtocolWithAssociatedTypesFullPath(theElem as NamedTypeSpec, this))
                return netBundle;
            if (theElem is ClosureTypeSpec)
                return netBundle;
            if (theElem is ProtocolListTypeSpec)
                return new NetTypeBundle(netBundle.NameSpace, netBundle.Type, netBundle.IsScalar, true, netBundle.Entity);
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
            var isStructOrEnum = netBundle.Entity == EntityType.Struct || netBundle.Entity == EntityType.Enum;
            if (isStructOrEnum)
            {
                if (MustForcePassByReference(typeContext, theElem) || netBundle.Entity == EntityType.Enum || structsAndEnumsAreAlwaysRefs)
                {
                    return new NetTypeBundle("System", "IntPtr", netBundle.IsScalar, false, netBundle.Entity);
                }
            }
            else if (entity != null && entity.EntityType == EntityType.Protocol && !entity.IsObjCProtocol)
            {
                return new NetTypeBundle(netBundle.NameSpace, netBundle.Type, netBundle.IsScalar, true, netBundle.Entity);
            }
            return netBundle;
        }


        NetParam MapParameterItem(BaseDeclaration context, ParameterItem parameter, int index, bool isPinvoke, bool structsAndEnumsAreAlwaysRefs)
        {
            var theType = parameter.TypeSpec; //parameter.IsInOut && !parameter.TypeSpec.IsInOut ? parameter.TypeSpec.WithInOutSet () : parameter.TypeSpec;
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
                if (st[i].TypeSpec is ProtocolListTypeSpec plitem && !isPinvoke)
                {
                    var genprotoName = new CSIdentifier($"TProto{i}");
                    extraProtocolTypes.Add(new CSGenericTypeDeclaration(genprotoName));
                    extraProtocolContstraints.Add(new CSGenericConstraint(genprotoName, ToConstraintIDs(context, plitem.Protocols.Keys, isPinvoke)));
                    var netBundle = new NetTypeBundle("", genprotoName.Name, false, st[i].IsInOut, EntityType.ProtocolList);

                    parms.Add(ToNamedParam(st[i], netBundle, i));
                }
                else
                {
                    var entity = !context.IsTypeSpecGeneric(st[i].TypeSpec) ? GetEntityForTypeSpec(st[i].TypeSpec) : null;
                    if (entity != null && entity.EntityType == EntityType.Protocol && !isPinvoke)
                    {
                        var proto = entity.Type as ProtocolDeclaration;
                        // if (proto.HasDynamicSelf) {
                        // 	extraProtocolTypes.Add (new CSGenericTypeDeclaration (BindingsCompiler.kGenericSelf));
                        // 	var csProtoBundle = MapType (context, st [i].TypeSpec, isPinvoke);
                        // 	var csType = csProtoBundle.ToCSType (use);
                        // 	extraProtocolContstraints.Add (new CSGenericConstraint (BindingsCompiler.kGenericSelf, new CSIdentifier (csType.ToString ())));
                        // }
                    }

                    parms.Add(MapParameterItem(context, st[i], i, isPinvoke, structsAndEnumsAreAlwaysRefs));
                }
            }
            return SanitizeParamNames(parms);
        }

        public IEnumerable<CSIdentifier> ToConstraintIDs(BaseDeclaration context, IEnumerable<NamedTypeSpec> types, bool isPinvoke)
        {
            foreach (var ns in types)
            {
                var cstype = MapType(context, ns, isPinvoke);
                yield return new CSIdentifier(cstype.ToString());
            }
        }

        public static bool IsCompoundProtocolListType(SwiftType st)
        {
            return st is SwiftProtocolListType pt && pt.Protocols.Count > 1;
        }

        public static bool IsCompoundProtocolListType(TypeSpec sp)
        {
            return sp is ProtocolListTypeSpec pl && pl.Protocols.Count > 1;
        }

        public NetTypeBundle MapType(SwiftType st, bool isPinvoke, bool isReturnValue = false)
        {
            if (IsCompoundProtocolListType(st) && !isPinvoke)
                throw new NotImplementedException("Check for a protocol list type first because you need to promote the method to a generic");
            switch (st.Type)
            {
                case CoreCompoundType.Scalar:
                    return ToScalar((SwiftBuiltInType)st);
                case CoreCompoundType.Tuple:
                    return ToTuple((SwiftTupleType)st, isPinvoke);
                case CoreCompoundType.MetaClass:
                    return ToMetaClass((SwiftMetaClassType)st);
                case CoreCompoundType.Class:
                    if (st.IsStruct)
                        return ToStruct((SwiftClassType)st, isPinvoke);
                    else if (st.IsClass)
                        return ToClass((SwiftClassType)st, isPinvoke);
                    else if (st.IsEnum)
                        return ToEnum((SwiftClassType)st, isPinvoke, isReturnValue);
                    else if (st.IsProtocol)
                        return ToProtocol((SwiftClassType)st, isPinvoke);
                    else
                        throw new NotImplementedException();
                case CoreCompoundType.ProtocolList:
                    return ToProtocol((SwiftProtocolListType)st, isPinvoke);
                case CoreCompoundType.BoundGeneric:
                    return ToBoundGeneric((SwiftBoundGenericType)st, isPinvoke);
                case CoreCompoundType.GenericReference:
                    return ToGenericReference((SwiftGenericArgReferenceType)st, isPinvoke);
                case CoreCompoundType.Function:
                    return ToClosure((SwiftBaseFunctionType)st, isPinvoke, isReturnValue);
                default:
                    throw new NotImplementedException();
            }
        }

        public NetTypeBundle MapType(BaseDeclaration context, TypeSpec spec, bool isPinvoke, bool isReturnValue = false,
            Tuple<int, int> selfDepthIndex = null)
        {
            if (IsCompoundProtocolListType(spec) && !isPinvoke)
                throw new NotImplementedException("Check for a protocol list type first because you need to promote the method to a generic");
            switch (spec.Kind)
            {
                case TypeSpecKind.Named:
                    var named = (NamedTypeSpec)spec;
                    if (IsScalar(named.Name))
                    {
                        return ToScalar(named.Name, spec.IsInOut);
                    }
                    if (context.IsProtocolWithAssociatedTypesFullPath(named, this))
                    {
                        if (isPinvoke)
                        {
                            return new NetTypeBundle("System", "IntPtr", false, false, EntityType.None);
                        }
                        else
                        {
                            var assocType = context.AssociatedTypeDeclarationFromGenericWithFullPath(named, this);
                            var owningProtocol = context.OwningProtocolFromGenericWithFullPath(named, this);
                            return new NetTypeBundle(owningProtocol, assocType, false);
                        }
                    }
                    else if (context.IsEqualityConstrainedByAssociatedType(named, this))
                    {
                        if (isPinvoke)
                        {
                            return new NetTypeBundle("System", "IntPtr", false, false, EntityType.None);
                        }
                        else
                        {
                            var assocType = context.AssociatedTypeDeclarationFromConstrainedGeneric(named, this);
                            var owningProtocol = context.OwningProtocolFromConstrainedGeneric(named, this);
                            return new NetTypeBundle(owningProtocol, assocType, false);
                        }
                    }
                    else if (context.IsTypeSpecGeneric(spec) && (!spec.ContainsGenericParameters || isPinvoke))
                    {
                        if (context.IsTypeSpecGenericMetatypeReference(spec))
                        {
                            return new NetTypeBundle("SwiftRuntimeLibrary", "SwiftMetatype", false, spec.IsInOut, EntityType.None);
                        }
                        else
                        {
                            if (isPinvoke)
                            {
                                var isPAT = context.GetConstrainedProtocolWithAssociatedType(named, this) != null;
                                var isInOut = isPAT ? false : named.IsInOut;
                                return new NetTypeBundle("System", "IntPtr", false, isInOut, EntityType.None);
                            }
                            else
                            {
                                var depthIndex = context.GetGenericDepthAndIndex(spec);
                                return new NetTypeBundle(depthIndex.Item1, depthIndex.Item2);
                            }
                        }
                    }
                    else if (context.IsTypeSpecAssociatedType(named))
                    {
                        if (isPinvoke)
                        {
                            return new NetTypeBundle("System", "IntPtr", false, named.IsInOut, EntityType.None);
                        }
                        else
                        {
                            if (named.ContainsGenericParameters)
                            {
                                var en = TypeDatabase.EntityForSwiftName(named.Name);
                                var retval = new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, spec.IsInOut, en.EntityType);
                                foreach (var gen in named.GenericParameters)
                                {
                                    var genNtb = MapType(context, gen, isPinvoke, isReturnValue);
                                    retval.GenericTypes.Add(genNtb);
                                }
                                return retval;
                            }
                            else
                            {
                                var assocType = context.AssociatedTypeDeclarationFromNamedTypeSpec(named);
                                return new NetTypeBundle(context.AsProtocolOrParentAsProtocol(), assocType, named.IsInOut);
                            }
                        }
                    }
                    // else if (named.Name == "Self") {
                    // 	if (isPinvoke) {
                    // 		return new NetTypeBundle ("System", "IntPtr", false, named.IsInOut, EntityType.None);
                    // 	} else {
                    // 		return new NetTypeBundle (BindingsCompiler.kGenericSelfName, named.IsInOut);
                    // 	}
                    // }
                    else
                    {
                        Entity en = TypeDatabase.EntityForSwiftName(named.Name);
                        if (en != null)
                        {
                            if (isPinvoke)
                            {
                                switch (en.EntityType)
                                {
                                    case EntityType.Class:
                                    case EntityType.Enum:
                                    case EntityType.Tuple:
                                        if (en.IsObjCEnum)
                                        {
                                            return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, true, en.EntityType);
                                        }
                                        return new NetTypeBundle("System", "IntPtr", false, spec.IsInOut, EntityType.None);
                                    case EntityType.TrivialEnum:
                                        return ToTrivialEnumType(en, en.Type as EnumDeclaration, isPinvoke, isReturnValue, spec.IsInOut);
                                    case EntityType.Protocol:
                                        if (en.IsObjCProtocol)
                                        {
                                            return new NetTypeBundle("System", "IntPtr", false, spec.IsInOut, EntityType.None);
                                        }
                                        else
                                        {
                                            if (spec is ProtocolListTypeSpec protocolList)
                                            {
                                                var container = $"SwiftExistentialContainer{protocolList.Protocols.Count}";
                                                return new NetTypeBundle("SwiftRuntimeLibrary", container, false, true, EntityType.None);
                                            }
                                            else
                                            {
                                                return new NetTypeBundle("SwiftRuntimeLibrary", "SwiftExistentialContainer1", false, true,
                                                        EntityType.None);
                                            }
                                        }
                                    case EntityType.Struct:
                                        var protocolListRef = GetBoundProtocolListType(named);
                                        if (protocolListRef != null)
                                        {
                                            var container = $"SwiftExistentialContainer{protocolListRef.Protocols.Count}";
                                            return new NetTypeBundle("SwiftRuntimeLibrary", container, false, true, EntityType.None);

                                        }
                                        en = MapToObjCStructOrEnumReference(context, named) ?? en;
                                        if (en.IsObjCStruct || en.IsObjCEnum)
                                        {
                                            return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, true, en.EntityType);
                                        }
                                        en = MapToScalarReference(context, named) ?? en;
                                        if (en.EntityType == EntityType.Scalar)
                                        {
                                            return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, true, en.EntityType);
                                        }
                                        else
                                        {
                                            return new NetTypeBundle("System", "IntPtr", false, spec.IsInOut, EntityType.None);
                                        }
                                    case EntityType.Scalar:
                                        return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, spec.IsInOut, en.EntityType);
                                    default:
                                        throw ErrorHelper.CreateError(ReflectorError.kCantHappenBase + 21, "Can't happen - shouldn't ever get to this case in type mapping.");
                                }
                            }
                            else
                            {
                                var retval = new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, spec.IsInOut, en.EntityType);
                                if (en.EntityType == EntityType.Protocol && en.Type is ProtocolDeclaration proto)
                                {
                                    // if (proto.HasDynamicSelf) {
                                    // 	if (selfDepthIndex != null) {
                                    // 		retval.GenericTypes.Add (new NetTypeBundle (selfDepthIndex.Item1, selfDepthIndex.Item2));
                                    // 	} else {
                                    // 		retval.GenericTypes.Add (new NetTypeBundle (BindingsCompiler.kGenericSelfName, spec.IsInOut));
                                    // 	}
                                    // }
                                    foreach (var assoc in proto.AssociatedTypes)
                                    {
                                        var genMap = new NetTypeBundle(proto, assoc, spec.IsInOut);
                                        retval.GenericTypes.Add(genMap);
                                    }
                                }
                                else
                                {
                                    foreach (var gen in spec.GenericParameters)
                                    {
                                        retval.GenericTypes.Add(MapType(context, gen, isPinvoke));
                                    }
                                }
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
                case TypeSpecKind.Closure:
                    if (isPinvoke)
                    {
                        return new NetTypeBundle("SwiftRuntimeLibrary", isReturnValue ? "BlindSwiftClosureRepresentation" : "SwiftClosureRepresentation", false, false, EntityType.Closure);
                    }
                    else
                    {
                        ClosureTypeSpec ft = spec as ClosureTypeSpec;
                        var throws = !isPinvoke && ft.Throws;
                        var arguments = ft.EachArgument().Select(parm => MapType(context, parm, false)).ToList();

                        string delegateName = "Action";
                        if (ft.ReturnType != null && !ft.ReturnType.IsEmptyTuple)
                        {
                            var returnBundle = MapType(context, ft.ReturnType, false, true);
                            arguments.Add(returnBundle);
                            delegateName = "Func";
                        }

                        if (arguments.Count == 0)
                            return new NetTypeBundle("System", delegateName, false, false, EntityType.Closure, swiftThrows: throws);
                        else
                            return new NetTypeBundle("System", delegateName, EntityType.Closure, false, arguments, swiftThrows: throws);
                    }
                case TypeSpecKind.ProtocolList:
                    var pl = (ProtocolListTypeSpec)spec;
                    return new NetTypeBundle("SwiftRuntimeLibrary", $"SwiftExistentialContainer{pl.Protocols.Count}",
                        false, true, EntityType.None);

                default:
                    throw new NotImplementedException();
            }
        }

        bool IsProtocolListTypeReference(BaseDeclaration context, NamedTypeSpec spec)
        {
            var boundType = GetBoundProtocolListType(spec);

            return boundType != null;
        }

        Entity MapToScalarReference(BaseDeclaration context, NamedTypeSpec spec)
        {
            var boundType = GetBoundPointerType(spec);
            if (boundType == null)
                return null;
            if (context.IsTypeSpecGenericReference(boundType))
                return null;
            if (context.IsProtocolWithAssociatedTypesFullPath(boundType, this))
                return null;
            var entity = TypeDatabase.EntityForSwiftName(boundType.Name);
            return entity.EntityType == EntityType.Scalar || SwiftType.IsStructScalar(boundType.Name) ? entity : null;
        }

        Entity MapToObjCStructOrEnumReference(BaseDeclaration context, NamedTypeSpec spec)
        {
            var boundType = GetBoundPointerType(spec);
            if (boundType == null)
                return null;
            if (context.IsTypeSpecGenericReference(boundType))
                return null;
            if (context.IsProtocolWithAssociatedTypesFullPath(boundType, this))
                return null;
            var entity = TypeDatabase.EntityForSwiftName(boundType.Name);
            return entity.IsObjCStruct || entity.IsObjCEnum ? entity : null;
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

        static ProtocolListTypeSpec GetBoundProtocolListType(NamedTypeSpec spec)
        {
            if (spec == null)
                return null;
            if (spec.Name != "Swift.UnsafePointer" && spec.Name != "Swift.UnsafeMutablePointer")
                return null;
            return spec.GenericParameters[0] as ProtocolListTypeSpec;
        }

        NetTypeBundle ToTuple(SwiftTupleType tt, bool isPinvoke)
        {
            if (tt.IsEmpty)
                return NetTypeBundle.Void;
            if (tt.Contents.Count == 1)
            {
                return MapType(tt.Contents[0], isPinvoke);
            }
            var lt = tt.Contents.Select(t => MapType(t, isPinvoke)).ToList();
            return new NetTypeBundle(lt, tt.IsReference);
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
            return ScalarIndex(Exceptions.ThrowOnNull(builtIn, "builtIn")) >= 0;
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

        NetTypeBundle ToMetaClass(SwiftMetaClassType mt)
        {
            return new NetTypeBundle("SwiftRuntimeLibrary", "SwiftMetatype", false, mt.IsReference, EntityType.None);
        }

        NetTypeBundle ToClass(SwiftClassType ct, bool isPinvoke)
        {
            if (isPinvoke)
            {
                return new NetTypeBundle("System", "IntPtr", false, ct.IsReference, EntityType.None);
            }
            else
            {
                var en = TypeDatabase.EntityForSwiftName(ct.ClassName);
                if (en != null)
                {
                    return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, ct.IsReference, en.EntityType);
                }
                else
                {
                    throw ErrorHelper.CreateError(ReflectorError.kTypeMapBase + 9, $"Unable to find swift class '{ct.ClassName.ToFullyQualifiedName()}'.");
                }
            }
        }

        NetTypeBundle ToProtocol(SwiftProtocolListType lt, bool isPinvoke)
        {
            if (lt.Protocols.Count > 1)
            {
                return ToProtocols(lt, isPinvoke);
            }
            return ToProtocol(lt.Protocols[0], isPinvoke);
        }

        NetTypeBundle ToProtocols(SwiftProtocolListType lt, bool isPinvoke)
        {
            if (!isPinvoke)
                throw new NotImplementedException("If you're not doing a PInvoke, this has to be handled specially");
            return new NetTypeBundle("SwiftRuntimeLibrary", $"SwiftExistentialContainer{lt.Protocols.Count}", false, true, EntityType.ProtocolList);
        }

        NetTypeBundle ToProtocol(SwiftClassType proto, bool isPinvoke)
        {
            var en = GetEntityForSwiftClassName(proto.ClassName.ToFullyQualifiedName(true));
            if (!en.Type.IsObjC)
            {
                if (isPinvoke)
                {
                    return new NetTypeBundle("SwiftRuntimeLibrary", "SwiftExistentialContainer1", false, isPinvoke || proto.IsReference,
                        EntityType.None);
                }
                return ToClass(proto, false);
            }
            else
            {
                return ToClass(proto, isPinvoke);
            }
        }

        NetTypeBundle ToGenericReference(SwiftGenericArgReferenceType st, bool isPinvoke)
        {
            if (isPinvoke)
            {
                return NetTypeBundle.IntPtr;
            }
            else
            {
                return new NetTypeBundle(st.Depth, st.Index);
            }
        }

        NetTypeBundle ToClosure(SwiftBaseFunctionType ft, bool isPinvoke, bool isReturnValue)
        {
            if (isPinvoke)
            {
                return new NetTypeBundle("SwiftRuntimeLibrary",
                                          isReturnValue ? "BlindSwiftClosureRepresentation" : "SwiftClosureRepresentation",
                                          false, false, EntityType.Closure);
            }
            else
            {
                var arguments = ft.EachParameter.Select(parm => MapType(parm, false)).ToList();

                string delegateName = "Action";
                if (ft.ReturnType != null && !ft.ReturnType.IsEmptyTuple)
                {
                    var returnBundle = MapType(ft.ReturnType, false, true);
                    arguments.Add(returnBundle);
                    delegateName = "Func";
                }

                if (arguments.Count == 0)
                    return new NetTypeBundle("System", delegateName, false, false, EntityType.Closure);
                else
                    return new NetTypeBundle("System", delegateName, EntityType.Closure, false, arguments);
            }
        }

        NetTypeBundle ToBoundGeneric(SwiftBoundGenericType gt, bool isPinvoke)
        {
            var ct = gt.BaseType as SwiftClassType;
            if (ct != null && IsSwiftPointerType(ct.ClassName.ToFullyQualifiedName(true)))
            {
                if (gt.BoundTypes[0] is SwiftClassType boundType)
                {
                    var entity = GetEntityForSwiftClassName(boundType.ClassName.ToFullyQualifiedName(true));
                    if (entity != null && entity.IsObjCStruct)
                        return MapType(boundType, isPinvoke);
                }
                return new NetTypeBundle("System", "IntPtr", false, gt.IsReference, EntityType.None);
            }

            if (isPinvoke)
            {
                return new NetTypeBundle("System", "IntPtr", false, false, EntityType.None);
            }

            var baseType = MapType(ct, false);
            var genericTypes = gt.BoundTypes.Select(bt => MapType(bt, false));
            return new NetTypeBundle(baseType.NameSpace, baseType.Type, baseType.Entity, baseType.IsReference, genericTypes);
        }

        NetTypeBundle ToStruct(SwiftClassType st, bool isPinvoke)
        {
            var en = TypeDatabase.EntityForSwiftName(st.ClassName);
            if (en != null)
            {
                if (isPinvoke && !SwiftType.IsStructScalar(st))
                {
                    if (en.Type.IsObjC)
                    {
                        return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, true, en.EntityType);
                    }
                    else
                    {
                        return NetTypeBundle.IntPtr;
                    }
                }
                else
                {
                    return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, st.IsReference, en.EntityType);
                }
            }
            else
            {
                throw ErrorHelper.CreateError(ReflectorError.kTypeMapBase + 10, $"Unable to find swift struct '{st.ClassName.ToFullyQualifiedName()}'.");
            }
        }

        NetTypeBundle ToEnum(SwiftClassType st, bool isPinvoke, bool isReturnValue)
        {
            var en = TypeDatabase.EntityForSwiftName(st.ClassName);
            if (en == null)
            {
                throw ErrorHelper.CreateError(ReflectorError.kTypeMapBase + 11, $"Unable to find swift enum '{st.ClassName.ToFullyQualifiedName()}'");
            }
            if (en.EntityType == EntityType.TrivialEnum)
            {
                return ToTrivialEnumType(en, en.Type as EnumDeclaration, isPinvoke, isReturnValue, st.IsReference);
            }
            else
            {
                if (isPinvoke)
                {
                    return NetTypeBundle.IntPtr;
                }
                else
                {
                    return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, st.IsReference, en.EntityType);
                }
            }
        }

        public bool TargetPlatformIs64Bit { get; private set; }

        public int MachinePointerSize { get { return TargetPlatformIs64Bit ? 8 : 4; } }


        bool MustForcePassByReference(Entity en)
        {
            if (en == null)
                throw ErrorHelper.CreateError(ReflectorError.kCantHappenBase + 4, "Null entity.");
            // can't big structs, non-blitable structs, or plain enums
            if (en.EntityType == EntityType.Scalar)
                return false;
            return en.IsStructOrEnum || (en.EntityType == EntityType.Protocol && !en.IsObjCProtocol);
        }

        public bool MustForcePassByReference(TypeDeclaration decl)
        {
            var en = TypeDatabase.EntityForSwiftName(decl.ToFullyQualifiedName());
            if (en == null)
                return false;
            return MustForcePassByReference(en);
        }

        public bool MustForcePassByReference(SwiftType st)
        {
            if (st is SwiftUnboundGenericType)
                return true;
            if (st is SwiftGenericArgReferenceType)
                return true;
            if (st is SwiftBaseFunctionType)
                return false;
            var protList = st as SwiftProtocolListType;
            if (protList != null)
            {
                if (protList.Protocols.Count > 1)
                    return true;
                return MustForcePassByReference(protList.Protocols[0]);
            }
            var classType = st as SwiftClassType;
            if (classType == null)
            {
                SwiftTupleType tuple = st as SwiftTupleType;
                if (tuple != null)
                {
                    return tuple.Contents.Count > 0;
                }
                SwiftBuiltInType bit = st as SwiftBuiltInType;
                if (bit != null)
                {
                    return false;
                }
                SwiftBoundGenericType bgt = st as SwiftBoundGenericType;
                if (bgt == null)
                    throw new NotImplementedException();
                classType = bgt.BaseType as SwiftClassType;
            }

            var en = TypeDatabase.EntityForSwiftName(classType.ClassName);
            if (en == null)
            {
                if (!classType.IsClass)
                {
                    var module = classType.ClassName.Module.Name;
                    return !(classType.IsEnum && (module == "__C" || module == "__ObjC"));
                }
                return false;
            }
            return MustForcePassByReference(en);
        }

        public bool MustForcePassByReference(BaseDeclaration context, TypeSpec sp)
        {
            if (sp.IsEmptyTuple)
                return false;
            if (sp is TupleTypeSpec tuple)
                return tuple.Elements.Count > 1;
            if (sp is ClosureTypeSpec)
                return false;
            if (sp is ProtocolListTypeSpec protolist)
            {
                if (protolist.Protocols.Count > 1)
                    return true;
                return MustForcePassByReference(context, protolist.Protocols.ElementAt(0).Key);
            }
            if (context.IsTypeSpecGeneric(sp) && context.IsTypeSpecGenericReference(sp))
                return true;
            if (context.IsProtocolWithAssociatedTypesFullPath(sp as NamedTypeSpec, this))
                return true;
            if (sp.IsDynamicSelf)
                return true;
            var en = GetEntityForTypeSpec(sp);
            if (en == null)
                return false;
            if (sp.IsUnboundGeneric(context, this))
                return en.EntityType != EntityType.Class;
            return MustForcePassByReference(en);
        }

        public static bool IsSwiftPointerType(string fullTypeName)
        {
            return fullTypeName == "Swift.UnsafePointer" || fullTypeName == "Swift.UnsafeMutablePointer"
                || fullTypeName == "Swift.UnsafeRawPointer" || fullTypeName == "Swift.UnsafeMutableRawPointer";
        }

        static NetTypeBundle ToTrivialEnumType(Entity en, EnumDeclaration decl, bool isPinvoke, bool isReturnValue, bool isReference)
        {
            if (decl.HasRawType)
            {
                if (decl.RawTypeName == "Swift.Int" || decl.RawTypeName == "Swift.UInt")
                {
                    return new NetTypeBundle("System", decl.RawTypeName == "Swift.Int" ? "nint" : "nuint", false, false, EntityType.None);
                }
                else
                {
                    return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, isReference, en.EntityType);
                }
            }
            else
            {
                if (isPinvoke)
                {
                    if (isReturnValue)
                    {
                        var enType = en.Type as EnumDeclaration;
                        if (enType.IsTrivial && !enType.HasRawType)
                        {
                            if (enType.Elements.Count < 256)
                                return new NetTypeBundle("System", "byte", false, false, EntityType.None);
                            else if (enType.Elements.Count < 65536)
                            {
                                return new NetTypeBundle("System", "ushort", false, false, EntityType.None);
                            }
                        }
                    }
                    return new NetTypeBundle("System", "IntPtr", false, false, EntityType.None);
                }
                else
                {
                    return new NetTypeBundle(en.SharpNamespace, en.SharpTypeName, false, isReference, en.EntityType);
                }
            }
        }
    }
}

