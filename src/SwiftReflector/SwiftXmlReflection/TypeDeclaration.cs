// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SwiftReflector.ExceptionTools;
using SwiftReflector.TypeMapping;
using System.Text;

namespace SwiftReflector.SwiftXmlReflection
{
    public class TypeDeclaration : BaseDeclaration, IXElementConvertible
    {
        public TypeDeclaration()
            : base()
        {
            Kind = TypeKind.Unknown;
            InnerClasses = new List<ClassDeclaration>();
            InnerStructs = new List<StructDeclaration>();
            InnerEnums = new List<EnumDeclaration>();
            Members = new List<Member>();
            Inheritance = new List<Inheritance>();
            TypeAliases = new List<TypeAliasDeclaration>();
        }

        public TypeKind Kind { get; set; }
        public List<Inheritance> Inheritance { get; set; }
        public List<Member> Members { get; set; }
        public List<ClassDeclaration> InnerClasses { get; set; }
        public List<StructDeclaration> InnerStructs { get; set; }
        public List<EnumDeclaration> InnerEnums { get; set; }
        public List<TypeAliasDeclaration> TypeAliases { get; set; }
        public bool IsObjC { get; set; }
        public bool IsFinal { get; set; }
        public bool IsDeprecated { get; set; }
        public bool IsUnavailable { get; set; }
        public bool IsUnrooted { get; protected set; }

        public TypeDeclaration MakeUnrooted()
        {
            if (IsUnrooted)
                return this;

            TypeDeclaration unrooted = UnrootedFactory();
            unrooted.unrootedName = ToFullyQualifiedName(false);
            unrooted.fullUnrootedName = ToFullyQualifiedName(true);
            unrooted.Kind = Kind;
            unrooted.Inheritance.AddRange(Inheritance);
            unrooted.Members.AddRange(Members);
            unrooted.IsObjC = IsObjC;
            unrooted.IsFinal = IsFinal;
            unrooted.IsUnrooted = true;
            unrooted.Name = Name;
            unrooted.Access = Access;
            unrooted.Module = Module.MakeUnrooted();
            unrooted.Generics.AddRange(Generics);
            CompleteUnrooting(unrooted);
            return unrooted;
        }

        protected virtual TypeDeclaration UnrootedFactory()
        {
            throw new NotImplementedException();
        }

        public bool IsObjCOrInheritsObjC(TypeMapper typeMapper)
        {
            if (IsObjC)
                return true;
            if (Inheritance == null || Inheritance.Count == 0)
                return false;
            foreach (var inheritance in Inheritance)
            {
                if (inheritance.InheritanceKind != InheritanceKind.Class)
                    continue;
                var entity = typeMapper.GetEntityForTypeSpec(inheritance.InheritedTypeSpec);
                if (entity == null)
                    throw ErrorHelper.CreateError(ReflectorError.kCompilerBase + 4, $"Unable to find entity for class inheritance type {inheritance.InheritedTypeName}");
                return entity.Type.IsObjC || entity.Type.IsObjCOrInheritsObjC(typeMapper);
            }
            return false;
        }

        public bool IsSwiftBaseClass()
        {
            // this predicate determines if a TypeDeclaration is:
            // * a ClassDeclaration
            // * an all swift object
            // * no inheritance or any inheritance is not class inheritance
            if (!(this is ClassDeclaration))
                return false;
            if (IsObjC)
                return false;
            return Inheritance == null || !Inheritance.Any(inh => inh.InheritanceKind == InheritanceKind.Class);
        }

        public bool ProtectedObjCCtorIsInThis(TypeMapper typeMapper)
        {
            // this predicate determines if this type has a protected objc ctor in this.
            // it's used to determine if, when writing the C# binding, if we need to call base () or this ()
            var classDecl = this as ClassDeclaration;
            if (classDecl == null)
                return false;
            if (!IsObjC)
                return false;
            // no inheritance
            // this : (nothing) -> IsImportedBinding
            if (Inheritance == null || Inheritance.FirstOrDefault(inh => inh.InheritanceKind == InheritanceKind.Class) == null)
            {
                // if there's no inheritance, then the protected ctor is in this if it wasn't imported
                return !classDecl.IsImportedBinding;
            }
            // this : import -> true
            // this : binding : binding : import -> false
            var classInherit = Inheritance.First(inh => inh.InheritanceKind == InheritanceKind.Class);
            var entity = typeMapper.GetEntityForTypeSpec(classInherit.InheritedTypeSpec);
            if (entity == null)
                throw ErrorHelper.CreateError(ReflectorError.kCompilerBase + 9, $"Unable to find entity for class inheritance on type {classInherit.InheritedTypeName}");
            var inheritedClass = entity.Type as ClassDeclaration;
            if (inheritedClass == null)
                throw ErrorHelper.CreateError(ReflectorError.kCompilerBase + 10, $"Expected a ClassDeclaration in inheritance chain but got {entity.Type.ToFullyQualifiedName(true)} of {entity.Type.GetType().Name}");
            return inheritedClass.IsImportedBinding;
        }

        protected virtual void CompleteUnrooting(TypeDeclaration unrooted)
        {
        }

        protected string unrootedName = null;
        protected string fullUnrootedName = null;
        public override string ToFullyQualifiedName(bool includeModule = true)
        {
            if (IsUnrooted)
            {
                return includeModule ? fullUnrootedName : unrootedName;
            }
            else
            {
                return base.ToFullyQualifiedName(includeModule);
            }
        }

        #region IXElementConvertible implementation

        public XElement ToXElement()
        {
            if (!IsUnrooted)
                throw ErrorHelper.CreateError(ReflectorError.kCantHappenBase + 0, "TypeDeclarations must be unrooted to create from XML.");

            var xobjects = new List<XObject>();
            GatherXObjects(xobjects);
            XElement typeDecl = new XElement("typedeclaration", xobjects.ToArray());
            return typeDecl;
        }

        protected virtual void GatherXObjects(List<XObject> xobjects)
        {
            XElement generics = Generics.ToXElement();
            if (generics != null)
                xobjects.Add(generics);
            xobjects.Add(new XAttribute("kind", ToString(Kind)));
            xobjects.Add(new XAttribute("name", fullUnrootedName));
            xobjects.Add(new XAttribute("module", Module.Name));
            xobjects.Add(new XAttribute("accessibility", TypeDeclaration.ToString(Access)));
            xobjects.Add(new XAttribute("isObjC", IsObjC ? "true" : "false"));
            xobjects.Add(new XAttribute("isFinal", IsFinal ? "true" : "false"));
            xobjects.Add(new XAttribute("isDeprecated", IsDeprecated ? "true" : "false"));
            xobjects.Add(new XAttribute("isUnavailable", IsUnavailable ? "true" : "false"));
            // DO NOT INCLUDE Inner[Classes,Structs,Enums]
            List<XObject> memcontents = new List<XObject>(Members.Select(m => m.ToXElement()));
            xobjects.Add(new XElement("members", memcontents.ToArray()));
            List<XObject> inherits = new List<XObject>(Inheritance.Select(i => i.ToXElement()));
            xobjects.Add(new XElement("inherits", inherits.ToArray()));
            if (TypeAliases.Count > 0)
            {
                var aliases = new List<XObject>(TypeAliases.Select(a => a.ToXElement()));
                xobjects.Add(new XElement("typealiases", aliases.ToArray()));
            }
        }

        #endregion

        public static TypeDeclaration TypeFromXElement(TypeAliasFolder folder, XElement elem, ModuleDeclaration module, BaseDeclaration parent /* can be null */)
        {
            var decl = FromKind((string)elem.Attribute("kind"));
            bool isUnrooted = elem.Attribute("module") != null;
            decl.Module = module;
            decl.Parent = parent;
            if (isUnrooted)
            {
                decl.IsUnrooted = true;
                decl.fullUnrootedName = (string)elem.Attribute("name");
                decl.unrootedName = decl.fullUnrootedName.NameWithoutModule();
                decl.Name = decl.fullUnrootedName.Contains('.') ? decl.fullUnrootedName.Substring(decl.fullUnrootedName.LastIndexOf('.') + 1)
                    : decl.fullUnrootedName;
            }
            else
            {
                decl.Name = (string)elem.Attribute("name");
            }
            decl.Access = AccessibilityFromString((string)elem.Attribute("accessibility"));
            decl.IsObjC = elem.BoolAttribute("isObjC");
            decl.IsFinal = elem.BoolAttribute("isFinal");
            decl.IsDeprecated = elem.BoolAttribute("isDeprecated");
            decl.IsUnavailable = elem.BoolAttribute("isUnavailable");

            decl.InnerClasses.AddRange(InnerFoo<ClassDeclaration>(folder, elem, "innerclasses", module, decl));
            decl.InnerStructs.AddRange(InnerFoo<StructDeclaration>(folder, elem, "innerstructs", module, decl));
            decl.InnerEnums.AddRange(InnerFoo<EnumDeclaration>(folder, elem, "innerenums", module, decl));
            if (elem.Element("members") != null)
            {
                var members = from mem in elem.Element("members").Elements()
                              select Member.FromXElement(folder, mem, module, decl) as Member;
                decl.Members.AddRange(members);
            }
            if (elem.Element("inherits") != null)
            {
                var inherits = from inherit in elem.Element("inherits").Elements()
                               select SwiftReflector.SwiftXmlReflection.Inheritance.FromXElement(folder, inherit) as Inheritance;
                decl.Inheritance.AddRange(inherits);
            }
            var typealiases = elem.Element("typealiases");
            if (typealiases != null)
            {
                var aliases = from alias in typealiases.Elements()
                              select TypeAliasDeclaration.FromXElement(module.Name, alias);
                decl.TypeAliases.AddRange(aliases);
            }
            EnumDeclaration edecl = decl as EnumDeclaration;
            if (edecl != null)
            {
                var enumElements = (from enumElement in elem.Element("elements").Elements()
                                    select new EnumElement((string)enumElement.Attribute("name"), (string)enumElement.Attribute("type"),
                                 (long?)enumElement.Attribute("intValue"))).ToList(); ;
                edecl.Elements.AddRange(enumElements);
                if (elem.Attribute("rawType") != null)
                {
                    var rawType = TypeSpecParser.Parse((string)elem.Attribute("rawType"));
                    edecl.RawTypeName = folder.FoldAlias(parent, rawType).ToString();
                }
            }

            var protoDecl = decl as ProtocolDeclaration;
            if (protoDecl != null)
            {
                if (elem.Element("associatedtypes") != null)
                {
                    var assocElements = from assocElem in elem.Element("associatedtypes").Elements()
                                        select AssociatedTypeDeclaration.FromXElement(folder, assocElem);
                    protoDecl.AssociatedTypes.AddRange(assocElements);
                }
            }

            return decl;
        }

        static IEnumerable<T> InnerFoo<T>(TypeAliasFolder folder, XElement parent, string innerName, ModuleDeclaration module, BaseDeclaration parDecl) where T : TypeDeclaration
        {
            var inner = parent.Elements(innerName).SelectMany(el => el.Elements("typedeclaration"));
            var innerList = inner.Select(elem => FromXElement(folder, elem, module, parDecl)).ToList();
            var innerCast = innerList.Cast<T>().ToList();
            return innerCast;
        }

        static TypeDeclaration FromKind(string kind)
        {
            switch (kind)
            {
                case "class":
                    return new ClassDeclaration();
                case "struct":
                    return new StructDeclaration();
                case "enum":
                    return new EnumDeclaration();
                case "protocol":
                    return new ProtocolDeclaration();
                default:
                    return new TypeDeclaration();
            }
        }

        internal static string ToString(TypeKind kind)
        {
            switch (kind)
            {
                case TypeKind.Class:
                    return "class";
                case TypeKind.Struct:
                    return "struct";
                case TypeKind.Enum:
                    return "enum";
                case TypeKind.Protocol:
                    return "protocol:";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static Accessibility AccessibilityFromString(string value)
        {
            if (value == null)
                return Accessibility.Unknown;
            Accessibility access;
            Enum.TryParse(value, out access);
            return access;
        }

        internal static string ToString(Accessibility access)
        {
            return access.ToString();
        }

        public List<FunctionDeclaration> AllVirtualMethods()
        {
            if (this is ProtocolDeclaration)
            {
                return Members.OfType<FunctionDeclaration>().Where(decl =>
                                                                     !decl.IsConstructorOrDestructor &&
                                                                     (!decl.IsFinal && !decl.IsStatic && decl.Access == Accessibility.Public)).ToList();
            }
            else
            {
                return Members.OfType<FunctionDeclaration>().Where(decl =>
                                                                     !decl.IsConstructorOrDestructor &&
                                                                     (!decl.IsFinal && !decl.IsStatic && decl.Access == Accessibility.Open)).ToList();
            }
        }

        public List<PropertyDeclaration> AllProperties()
        {
            return Members.OfType<PropertyDeclaration>().ToList();
        }

        public List<PropertyDeclaration> AllVirtualProperties()
        {
            Accessibility requiredAccessibility;
            if (this is ProtocolDeclaration)
            {
                requiredAccessibility = Accessibility.Public;
            }
            else
            {
                requiredAccessibility = Accessibility.Open;
            }
            return Members.OfType<PropertyDeclaration>().Where(decl =>
            {
                if (decl.IsStatic)
                    return false;
                if (decl.Access != requiredAccessibility)
                    return false;
                var getter = decl.GetGetter();
                if (getter == null)
                    return false;
                if (getter.IsDeprecated || getter.IsUnavailable)
                    return false;
                return true;
            }).ToList();
        }

        public List<FunctionDeclaration> AllFinalMethods()
        {
            return Members.OfType<FunctionDeclaration>().Where(decl =>
                !decl.IsConstructorOrDestructor && decl.IsFinal).ToList();
        }

        public List<FunctionDeclaration> AllMethodsNoCDTor()
        {
            return Members.OfType<FunctionDeclaration>().Where(decl => !decl.IsConstructorOrDestructor).ToList();
        }

        public List<FunctionDeclaration> AllConstructors()
        {
            return Members.OfType<FunctionDeclaration>().Where(decl => decl.IsConstructor).ToList();
        }

        public List<FunctionDeclaration> AllDestructors()
        {
            return Members.OfType<FunctionDeclaration>().Where(decl => decl.IsDestructor).ToList();
        }

        public List<SubscriptDeclaration> AllSubscripts()
        {
            var allSubFuncs = Members.OfType<FunctionDeclaration>().Where(decl => decl.IsSubscript).ToList();
            var allSubs = new List<SubscriptDeclaration>();
            while (allSubFuncs.Count > 0)
            {
                int i = allSubFuncs.Count - 1;
                FunctionDeclaration decl = allSubFuncs[i];
                allSubFuncs.RemoveAt(i);
                if (decl.IsSubscriptMaterializer)
                    continue;
                if (decl.IsSubscriptGetter)
                {
                    FunctionDeclaration setter = GetAndRemoveSetter(allSubFuncs, decl);
                    FunctionDeclaration materializer = GetAndRemoveMaterializer(allSubFuncs, decl);
                    allSubs.Add(new SubscriptDeclaration(decl, setter, materializer));
                }
                else if (decl.IsSubscriptSetter)
                {
                    FunctionDeclaration getter = GetAndRemoveGetter(allSubFuncs, decl);
                    FunctionDeclaration materializer = GetAndRemoveMaterializer(allSubFuncs, decl);
                    allSubs.Add(new SubscriptDeclaration(getter, decl, materializer));
                }
            }
            return allSubs;
        }

        public TypeSpec ToTypeSpec()
        {
            NamedTypeSpec ns = new NamedTypeSpec(ToFullyQualifiedName());
            ns.GenericParameters.AddRange(Generics.Select(gen => new NamedTypeSpec(gen.Name)));
            return ns;
        }

        static FunctionDeclaration GetAndRemoveMaterializer(List<FunctionDeclaration> decls, FunctionDeclaration other)
        {
            // FIXME - materializers don't have enough to match on with 100% confidence
            // Materializers are (probably) not needed by tom-swifty, so no big
            return null;
        }

        static FunctionDeclaration GetAndRemoveGetter(List<FunctionDeclaration> decls, FunctionDeclaration other)
        {
            var plToMatch = new List<ParameterItem>();
            TypeSpec returnToMatch = null;
            var selfToMatch = other.Parent;

            if (other.IsSetter)
            {
                // setter - 
                // The arguments to a setter are
                // value, arg1, arg2 ... argn
                // We want to match the type of the return of the getter to the value of the setter
                // as well as the parameters
                List<ParameterItem> pl = other.ParameterLists.Last();
                plToMatch.AddRange(pl.GetRange(1, pl.Count - 1));
                returnToMatch = pl[0].TypeSpec;
            }
            else
            {
                // materializer.
                // The arguments to a materializer are
                // buffer, callbackStoragebuffer, arg1, arg2 ... argn
                // We have no return to match. Oops.
                List<ParameterItem> pl = other.ParameterLists.Last();
                plToMatch.AddRange(pl.GetRange(2, pl.Count - 2));
                returnToMatch = null;
            }


            for (int i = 0; i < decls.Count; i++)
            {
                FunctionDeclaration getter = decls[i];
                if (getter.Parent != selfToMatch)
                    return null;
                if (!getter.IsSubscriptGetter)
                    continue;
                if ((returnToMatch != null && returnToMatch.Equals(getter.ReturnTypeSpec)) || returnToMatch == null)
                {
                    List<ParameterItem> targetPl = getter.ParameterLists.Last();
                    if (ParmsMatch(plToMatch, targetPl))
                    {
                        decls.RemoveAt(i);
                        return getter;
                    }
                }
            }
            return null;
        }

        static FunctionDeclaration GetAndRemoveSetter(List<FunctionDeclaration> decls, FunctionDeclaration other)
        {
            var plToMatch = new List<ParameterItem>();
            var selfToMatch = other.Parent;

            if (other.IsGetter)
            {
                // getter - 
                // The arguments to a getter are
                // arg1, arg2 ... argn
                // We want to match the type of the return of the getter to the value of the setter
                // as well as the parameters
                List<ParameterItem> pl = other.ParameterLists.Last();
                ParameterItem item = new ParameterItem();
                item.PublicName = "";
                item.PrivateName = "retval";
                item.TypeSpec = other.ReturnTypeSpec;
                item.TypeName = other.ReturnTypeName;
                plToMatch.Add(item);
                plToMatch.AddRange(pl);
            }
            else
            {
                // we don't have enough information to match on setter
                // and since we don't use the materializer, NBD.

                // materializer.
                // The arguments to a materializer are
                // buffer, callbackStoragebuffer, arg1, arg2 ... argn
                // We have no return to match. Oops.
                return null;
            }

            for (int i = 0; i < decls.Count; i++)
            {
                FunctionDeclaration setter = decls[i];
                if (!setter.IsSubscriptGetter)
                    continue;
                List<ParameterItem> targetPl = setter.ParameterLists.Last();
                if (ParmsMatch(plToMatch, targetPl))
                {
                    decls.RemoveAt(i);
                    return setter;
                }
            }
            return null;
        }

        public bool VirtualMethodExistsInInheritedBoundType(FunctionDeclaration func, TypeMapper typeMapper)
        {
            // virtual methods are only in classes
            if (!(this is ClassDeclaration))
                return false;
            var classInheritance = Inheritance.FirstOrDefault(inh => inh.InheritanceKind == InheritanceKind.Class);
            if (classInheritance == null)
                return false;

            var inheritedEntity = typeMapper.GetEntityForTypeSpec(classInheritance.InheritedTypeSpec);
            if (inheritedEntity == null)
                throw ErrorHelper.CreateError(ReflectorError.kTypeMapBase + 18, $"Unable to find type database entry for class {classInheritance.InheritedTypeName} while searching inheritance.");

            // if we get here, the Type has to be a ClassDeclaration
            var inheritedClass = inheritedEntity.Type as ClassDeclaration;

            var methods = inheritedClass.AllVirtualMethods().FindAll(fn => fn.Name == func.Name
                                           && fn.ParameterLists.Last().Count == func.ParameterLists.Last().Count).ToList();
            foreach (var method in methods)
            {
                if (ParmsMatchWithNames(method.ParameterLists.Last(), func.ParameterLists.Last())
                    && method.ReturnTypeSpec.Equals(func.ReturnTypeSpec))
                    return true;
            }
            return inheritedClass.VirtualMethodExistsInInheritedBoundType(func, typeMapper);
        }

        static bool ParmsMatchWithNames(List<ParameterItem> pl1, List<ParameterItem> pl2)
        {
            if (pl1.Count != pl2.Count)
                return false;
            for (int i = 0; i < pl1.Count; i++)
            {
                if (pl1[i].PublicName != pl2[i].PublicName)
                    return false;
                if (pl1[i].IsInOut != pl2[i].IsInOut)
                    return false;
                if (!pl1[i].TypeSpec.Equals(pl2[i].TypeSpec))
                    return false;
            }
            return true;
        }

        static bool ParmsMatch(List<ParameterItem> pl1, List<ParameterItem> pl2)
        {
            if (pl1.Count != pl2.Count)
                return false;
            for (int i = 0; i < pl1.Count; i++)
            {
                if (pl1[i].IsInOut != pl2[i].IsInOut)
                    return false;
                if (!pl1[i].TypeSpec.Equals(pl2[i].TypeSpec))
                    return false;
            }
            return true;
        }


    }

}
