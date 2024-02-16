// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using SwiftReflector.TypeMapping;
using System.Linq;
using System.Collections;

namespace SwiftReflector.SwiftXmlReflection
{
    public class TypeSpecAttribute
    {
        public TypeSpecAttribute(string name)
        {
            Name = name;
            Parameters = new List<string>();
        }
        public string Name { get; set; }
        public List<string> Parameters { get; private set; }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('@');
            sb.Append(Name);
            if (Parameters.Count > 0)
            {
                sb.Append('(');
                for (int i = 0; i < Parameters.Count; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append(Parameters[i]);
                }
                sb.Append(')');
            }
            return sb.ToString();
        }
    }

    public abstract class TypeSpec
    {
        protected TypeSpec(TypeSpecKind kind)
        {
            Kind = kind;
            GenericParameters = new List<TypeSpec>();
            Attributes = new List<TypeSpecAttribute>();
        }

        public TypeSpecKind Kind { get; private set; }
        public List<TypeSpec> GenericParameters { get; private set; }
        public bool ContainsGenericParameters { get { return GenericParameters.Count != 0; } }
        public List<TypeSpecAttribute> Attributes { get; private set; }
        public bool HasAttributes { get { return Attributes.Count != 0; } }
        public bool IsInOut { get; set; }
        public bool IsAny { get; set; }
        public virtual bool IsEmptyTuple { get { return false; } }
        protected abstract string LLToString(bool useFullName);
        protected virtual string LLFinalStringParts() { return ""; }
        protected abstract bool LLEquals(TypeSpec other, bool partialNameMatch);
        public string TypeLabel { get; set; }
        public bool IsArray
        {
            get
            {
                NamedTypeSpec ns = this as NamedTypeSpec;
                return ns != null && ns.Name == "Swift.Array";
            }
        }

        public bool IsBoundGeneric(BaseDeclaration context, TypeMapper mapper)
        {
            switch (this.Kind)
            {
                case TypeSpecKind.Named:
                    NamedTypeSpec ns = (NamedTypeSpec)this;
                    Entity en = mapper.TryGetEntityForSwiftClassName(ns.Name);
                    if (en == null)
                    {
                        if (context.IsTypeSpecGeneric(ns))
                            return false; // unbound
                    }
                    foreach (TypeSpec genParm in GenericParameters)
                    {
                        if (genParm.IsUnboundGeneric(context, mapper))
                            return false; // unbound
                    }
                    return true;
                case TypeSpecKind.Closure:
                    ClosureTypeSpec cs = (ClosureTypeSpec)this;
                    return cs.Arguments.IsBoundGeneric(context, mapper) && cs.ReturnType.IsBoundGeneric(context, mapper);
                case TypeSpecKind.Tuple:
                    TupleTypeSpec ts = (TupleTypeSpec)this;
                    foreach (TypeSpec elem in ts.Elements)
                    {
                        if (elem.IsUnboundGeneric(context, mapper))
                            return false;
                    }
                    return true;
                default:
                    throw new NotSupportedException("unknown TypeSpecKind " + this.Kind.ToString());
            }
        }

        public bool IsUnboundGeneric(BaseDeclaration context, TypeMapper mapper)
        {
            switch (Kind)
            {
                case TypeSpecKind.Named:
                    NamedTypeSpec ns = (NamedTypeSpec)this;
                    if (context.IsTypeSpecGeneric(ns.ToString()))
                        return true;
                    foreach (TypeSpec genparm in GenericParameters)
                    {
                        if (genparm.IsUnboundGeneric(context, mapper))
                            return true;
                    }
                    return false;
                case TypeSpecKind.Closure:
                    ClosureTypeSpec cs = (ClosureTypeSpec)this;
                    return cs.Arguments.IsUnboundGeneric(context, mapper) && cs.ReturnType.IsUnboundGeneric(context, mapper);
                case TypeSpecKind.Tuple:
                    TupleTypeSpec ts = (TupleTypeSpec)this;
                    foreach (TypeSpec elem in ts.Elements)
                    {
                        if (elem.IsUnboundGeneric(context, mapper))
                            return true;
                    }
                    return false;
                case TypeSpecKind.ProtocolList:
                    return false;
                default:
                    throw new NotSupportedException("unknown TypeSpecKind " + this.Kind.ToString());
            }
        }


        public override bool Equals(object obj)
        {
            TypeSpec spec = obj as TypeSpec;
            if (spec == null)
                return false;
            if (Kind != spec.Kind)
                return false;
            if (!ListEqual(GenericParameters, spec.GenericParameters, false))
                return false;
            if (IsInOut != spec.IsInOut)
                return false;
            // Don't compare IsAny - it's really not important (yet)
            return LLEquals(spec, false);
        }

        public bool EqualsPartialMatch(TypeSpec spec)
        {
            if (spec == null)
                return false;
            if (Kind != spec.Kind)
                return false;
            if (!ListEqual(GenericParameters, spec.GenericParameters, true))
                return false;
            if (IsInOut != spec.IsInOut)
                return false;
            // Don't compare IsAny - it's really not important (yet)
            return LLEquals(spec, true);
        }

        public virtual bool EqualsReferenceInvaraint(TypeSpec type)
        {
            var a = ProjectAsNonReference(this);
            var b = ProjectAsNonReference(type);

            if (b.Kind != a.Kind)
                return false;
            if (b.GetType() != a.GetType())
                return false;
            // shouldn't do Name equality except in functions
            return a.LLEquals(b, false);
        }

        public TypeSpec NonReferenceCloneOf()
        {
            if (!IsInOut)
                return this;
            var ty = MemberwiseClone() as TypeSpec;
            ty.IsInOut = false;
            return ty;
        }

        static TypeSpec ProjectAsNonReference(TypeSpec a)
        {
            if (a.IsInOut)
            {
                return a.NonReferenceCloneOf();
            }
            var namedType = a as NamedTypeSpec;
            if (namedType != null && namedType.GenericParameters.Count == 1)
            {
                if (namedType.Name == "Swift.UnsafePointer" || namedType.Name == "Swift.UnsafeMutablePointer")
                    return namedType.GenericParameters[0];
            }
            return a;
        }


        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        protected static bool ListEqual(List<TypeSpec> one, List<TypeSpec> two, bool partialNameMatch)
        {
            if (one.Count != two.Count)
                return false;
            for (int i = 0; i < one.Count; i++)
            {
                if (partialNameMatch)
                {
                    if (!one[i].EqualsPartialMatch(two[i]))
                        return false;
                }
                else
                {
                    if (!one[i].Equals(two[i]))
                        return false;
                }
            }
            return true;
        }

        public static bool IsNullOrEmptyTuple(TypeSpec spec)
        {
            return spec == null || spec.IsEmptyTuple;
        }

        public static bool BothNullOrEqual(TypeSpec one, TypeSpec two)
        {
            if (one == null && two == null)
                return true;
            if (one == null || two == null)
                return false;
            return one.Equals(two);
        }

        public bool ContainsBoundGenericClosure()
        {
            return ContainsBoundGenericClosure(0);
        }

        bool ContainsBoundGenericClosure(int depth)
        {
            if (this is NamedTypeSpec namedTypeSpec)
            {
                foreach (var subSpec in namedTypeSpec.GenericParameters)
                {
                    if (subSpec.ContainsBoundGenericClosure(depth + 1))
                        return true;
                }
            }
            else if (this is TupleTypeSpec tupleSpec)
            {
                foreach (var subSpec in tupleSpec.Elements)
                {
                    if (subSpec.ContainsBoundGenericClosure(depth + 1))
                        return true;
                }
            }
            else if (this is ClosureTypeSpec closureSpec)
            {
                return depth > 0;
            }
            return false;
        }

        public override string ToString()
        {
            return ToString(true);
        }

        public string ToString(bool useFullNames)
        {
            StringBuilder builder = new StringBuilder();

            foreach (var attr in Attributes)
            {
                builder.Append(attr.ToString());
                builder.Append(' ');
            }
            if (IsInOut)
                builder.Append("inout ");

            if (IsAny)
                builder.Append("any ");

            if (TypeLabel != null)
            {
                builder.Append(TypeLabel).Append(": ");
            }
            builder.Append(LLToString(useFullNames));

            if (ContainsGenericParameters)
            {
                builder.Append('<');
                for (int i = 0; i < GenericParameters.Count; i++)
                {
                    if (i > 0)
                        builder.Append(", ");
                    builder.Append(GenericParameters[i].ToString(useFullNames));
                }
                builder.Append('>');
            }
            builder.Append(LLFinalStringParts());

            return builder.ToString();
        }

        static string[] intNames = {
            "Swift.Int", "Swift.UInt", "Swift.Int8", "Swift.UInt8",
            "Swift.Int16", "Swift.UInt16", "Swift.Int32", "Swift.UInt32",
            "Swift.Int64", "Swift.UInt64", "Swift.Char"
        };

        public static bool IsIntegral(TypeSpec ts)
        {
            NamedTypeSpec named = ts as NamedTypeSpec;
            if (named == null)
                return false;
            return Array.IndexOf(intNames, named.Name) >= 0;
        }

        public static bool IsFloatingPoint(TypeSpec ts)
        {
            NamedTypeSpec named = ts as NamedTypeSpec;
            if (named == null)
                return false;
            return named.Name == "Swift.Float" || named.Name == "Swift.Double" || named.Name == "CoreGraphics.CGFloat";
        }

        public static bool IsBoolean(TypeSpec ts)
        {
            NamedTypeSpec named = ts as NamedTypeSpec;
            if (named == null)
                return false;
            return named.Name == "Swift.Bool";
        }

        public static bool IsBuiltInValueType(TypeSpec ts)
        {
            return IsIntegral(ts) || IsFloatingPoint(ts) || IsBoolean(ts);
        }

        public TypeSpec WithInOutSet()
        {
            var theSpec = TypeSpecParser.Parse(this.ToString());
            theSpec.IsInOut = true;
            return theSpec;
        }

        public bool IsDynamicSelf
        {
            get
            {
                return this is NamedTypeSpec ns && ns.Name == "Self";
            }
        }

        public abstract bool HasDynamicSelf
        {
            get;
        }

        public static bool AnyHasDynamicSelf(List<TypeSpec> types)
        {
            return types.Any(t => t.HasDynamicSelf);
        }

        public TypeSpec ReplaceName(string toFind, string replacement)
        {
            var result = this;
            if (!String.IsNullOrEmpty(replacement))
                ReplaceName(this, toFind, replacement, ref result);
            return result;
        }

        static bool ReplaceName(TypeSpec original, string toFind, string replacement, ref TypeSpec result)
        {
            result = original;
            var changed = false;
            switch (original.Kind)
            {
                case TypeSpecKind.Named:
                    changed = ReplaceName(original as NamedTypeSpec, toFind, replacement, ref result);
                    break;
                case TypeSpecKind.ProtocolList:
                    changed = ReplaceName(original as ProtocolListTypeSpec, toFind, replacement, ref result);
                    break;
                case TypeSpecKind.Closure:
                    changed = ReplaceName(original as ClosureTypeSpec, toFind, replacement, ref result);
                    break;
                case TypeSpecKind.Tuple:
                    changed = ReplaceName(original as TupleTypeSpec, toFind, replacement, ref result);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown TypeSpec kind {original.Kind}");
            }
            if (changed)
            {
                result.Attributes.AddRange(original.Attributes);
                result.TypeLabel = original.TypeLabel;
                result.IsInOut = original.IsInOut;
                result.IsAny = original.IsAny;
            }
            return changed;
        }

        static bool ReplaceName(NamedTypeSpec named, string toFind, string replacement, ref TypeSpec result)
        {
            result = named;
            var changed = false;
            if (named.Name == toFind)
            {
                changed = true;
                result = new NamedTypeSpec(replacement);
            }
            var resultGenerics = new List<TypeSpec>(named.GenericParameters.Count);
            var changedGenerics = ReplaceName(named.GenericParameters, toFind, replacement, resultGenerics);

            if (changedGenerics)
            {
                if (!changed)
                {
                    result = new NamedTypeSpec(named.Name);
                }
                result.GenericParameters.AddRange(resultGenerics);
            }
            else
            {
                if (changed)
                    result.GenericParameters.AddRange(named.GenericParameters);
            }
            return changed || changedGenerics;
        }

        static bool ReplaceName(List<TypeSpec> originalTypes, string toFind, string replacement, List<TypeSpec> resultTypes)
        {
            var changed = false;
            foreach (var type in originalTypes)
            {
                var result = type;
                changed = ReplaceName(type, toFind, replacement, ref result) || changed;
                resultTypes.Add(result);
            }
            return changed;
        }

        static bool ReplaceName(TupleTypeSpec tuple, string toFind, string replacement, ref TypeSpec result)
        {
            List<TypeSpec> resultTypes = new List<TypeSpec>(tuple.Elements.Count);
            if (ReplaceName(tuple.Elements, toFind, replacement, resultTypes))
            {
                result = new TupleTypeSpec(resultTypes);
                return true;
            }
            result = tuple;
            return false;
        }

        static bool ReplaceName(ProtocolListTypeSpec protolist, string toFind, string replacement, ref TypeSpec result)
        {
            var originalProtos = new List<TypeSpec>(protolist.Protocols.Count);
            var resultProtos = new List<TypeSpec>(protolist.Protocols.Count);
            originalProtos.AddRange(protolist.Protocols.Keys);
            if (ReplaceName(originalProtos, toFind, replacement, resultProtos))
            {
                result = new ProtocolListTypeSpec(resultProtos.OfType<NamedTypeSpec>());
                return true;
            }
            return false;
        }

        static bool ReplaceName(ClosureTypeSpec closure, string toFind, string replacement, ref TypeSpec result)
        {
            var resultArgs = closure.Arguments;
            var resultReturn = closure.ReturnType;

            var argsChanged = ReplaceName(closure.Arguments, toFind, replacement, ref resultArgs);
            var returnChanged = ReplaceName(closure.ReturnType, toFind, replacement, ref resultReturn);
            if (argsChanged || returnChanged)
            {
                result = new ClosureTypeSpec(resultArgs, resultReturn);
                return true;
            }
            return false;
        }
    }


    public class NamedTypeSpec : TypeSpec
    {
        public NamedTypeSpec(string name)
            : base(TypeSpecKind.Named)
        {
            name = SwiftInterfaceReflector.SwiftInterfaceReflector.UnTick(name);
            // Hack filter.
            // For whatever reason, Any and AnyObject are not
            // strictly in the Swift module. But they are.
            // But they're not.
            // What do I mean by this?
            // Apple's demangler will print these as Swift.Any or
            // Swift.AnyObject if the options are set to print
            // fully qualified names, so I feel no remorse for doing
            // this.
            if (name == "Any")
                name = "Swift.Any";
            else if (name == "AnyObject")
                name = "Swift.AnyObject";
            Name = name;
        }

        public NamedTypeSpec(string name, params TypeSpec[] genericSpecialization)
            : this(name)
        {
            GenericParameters.AddRange(genericSpecialization);
        }

        public NamedTypeSpec InnerType { get; set; }

        public bool IsProtocolList { get { return Name == "protocol"; } }
        public string Name { get; private set; }

        protected override string LLToString(bool useFullName)
        {
            return useFullName ? Name : NameWithoutModule;
        }

        protected override string LLFinalStringParts()
        {
            if (InnerType == null)
                return "";
            return "." + InnerType;
        }

        protected override bool LLEquals(TypeSpec other, bool partialNameMatch)
        {
            NamedTypeSpec spec = other as NamedTypeSpec;
            if (spec == null)
                return false;
            var innersMatch = (InnerType == null && spec.InnerType == null) || (InnerType != null && InnerType.LLEquals(spec.InnerType, partialNameMatch));
            if (partialNameMatch)
            {
                return NameWithoutModule == spec.NameWithoutModule && innersMatch;
            }
            else
            {
                return Name == spec.Name && innersMatch;
            }
        }

        public bool HasModule(BaseDeclaration context, TypeMapper typeMapper)
        {
            if (Name.Contains("."))
            {
                return !context.IsProtocolWithAssociatedTypesFullPath(new NamedTypeSpec(Name), typeMapper);
            }
            else
            {
                return false;
            }
        }
        public string Module
        {
            get
            {
                return Name.Substring(0, Name.IndexOf('.'));
            }
        }
        public string NameWithoutModule
        {
            get
            {
                return Name.IndexOf('.') >= 0 ? Name.Substring(Name.IndexOf('.') + 1) : Name;
            }
        }

        public override bool HasDynamicSelf
        {
            get
            {
                if (Name == "Self")
                    return true;
                return TypeSpec.AnyHasDynamicSelf(GenericParameters);
            }
        }
    }


    public class TupleTypeSpec : TypeSpec
    {
        public TupleTypeSpec()
            : base(TypeSpecKind.Tuple)
        {
            Elements = new List<TypeSpec>();
        }

        public TupleTypeSpec(TupleTypeSpec other)
            : base(TypeSpecKind.Tuple)
        {
            Elements = new List<TypeSpec>();
            Elements.AddRange(other.Elements);
            if (other.HasAttributes)
                Attributes.AddRange(other.Attributes);
            if (other.ContainsGenericParameters)
                GenericParameters.AddRange(other.GenericParameters);
            IsInOut = other.IsInOut;
        }

        public TupleTypeSpec(IEnumerable<TypeSpec> elements)
            : this()
        {
            Elements.AddRange(elements);
        }

        public TupleTypeSpec(TypeSpec single)
            : this()
        {
            Elements.Add(single);
        }

        public List<TypeSpec> Elements { get; private set; }

        protected override string LLToString(bool useFullName)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('(');
            for (int i = 0; i < Elements.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(Elements[i].ToString(useFullName));
            }
            builder.Append(')');
            return builder.ToString();
        }

        protected override bool LLEquals(TypeSpec other, bool partialNameMatch)
        {
            TupleTypeSpec spec = other as TupleTypeSpec;
            if (spec == null)
                return false;
            return ListEqual(Elements, spec.Elements, partialNameMatch);
        }

        public override bool IsEmptyTuple
        {
            get
            {
                return Elements.Count == 0;
            }
        }

        public override bool HasDynamicSelf => TypeSpec.AnyHasDynamicSelf(Elements);

        static TupleTypeSpec empty = new TupleTypeSpec();
        public static TupleTypeSpec Empty { get { return empty; } }
    }

    public class ClosureTypeSpec : TypeSpec
    {
        public ClosureTypeSpec()
            : base(TypeSpecKind.Closure)
        {
        }

        public ClosureTypeSpec(TypeSpec arguments, TypeSpec returnType)
            : this()
        {
            Arguments = arguments;
            ReturnType = returnType;
        }

        static ClosureTypeSpec voidVoid = new ClosureTypeSpec(TupleTypeSpec.Empty, TupleTypeSpec.Empty);

        public static ClosureTypeSpec VoidVoid { get { return voidVoid; } }

        public TypeSpec Arguments { get; set; }
        public TypeSpec ReturnType { get; set; }
        public bool Throws { get; set; }
        public bool IsAsync { get; set; }

        public bool HasReturn()
        {
            return ReturnType != null && !ReturnType.IsEmptyTuple;
        }

        public bool HasArguments()
        {
            return Arguments != null && !Arguments.IsEmptyTuple;
        }

        public TupleTypeSpec ArgumentsAsTuple
        {
            get
            {
                if (Arguments is TupleTypeSpec tuple)
                    return tuple;
                return new TupleTypeSpec(Arguments);
            }
        }

        public int ArgumentCount()
        {
            if (!HasArguments())
                return 0;
            if (Arguments is TupleTypeSpec tupe)
            {
                return tupe.Elements.Count;
            }
            return 1;
        }

        public IEnumerable<TypeSpec> EachArgument()
        {
            if (!HasArguments())
                yield break;
            TupleTypeSpec argList = Arguments as TupleTypeSpec;
            if (argList != null)
            {
                foreach (TypeSpec arg in argList.Elements)
                    yield return arg;
            }
            else
            {
                yield return Arguments;
            }
        }

        public TypeSpec GetArgument(int index)
        {
            if (index < 0 || index >= ArgumentCount())
                throw new ArgumentOutOfRangeException(nameof(index));
            if (Arguments is TupleTypeSpec tuple)
                return tuple.Elements[index];
            return Arguments;
        }

        public bool IsEscaping
        {
            get
            {
                return HasAttributes && Attributes.Exists(attr => attr.Name == "escaping");
            }
        }

        public bool IsAutoClosure
        {
            get
            {
                return HasAttributes && Attributes.Exists(attr => attr.Name == "autoclosure");
            }
        }

        protected override string LLToString(bool useFullName)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Arguments.ToString(useFullName));
            if (Throws)
                builder.Append(" throws -> ");
            else
                builder.Append(" -> ");
            builder.Append(ReturnType.ToString(useFullName));
            return builder.ToString();
        }

        protected override bool LLEquals(TypeSpec obj, bool partialNameMatch)
        {
            ClosureTypeSpec spec = obj as ClosureTypeSpec;
            if (spec == null)
                return false;

            if (partialNameMatch)
            {
                if (Arguments == null && spec.Arguments == null &&
                    ReturnType == null && spec.ReturnType == null)
                {
                    return true;
                }
                if (Arguments == null || spec.Arguments == null)
                    return false;
                if (ReturnType == null || spec.ReturnType == null)
                    return false;
                return Arguments.EqualsPartialMatch(spec.Arguments) &&
                ReturnType.EqualsPartialMatch(spec.ReturnType);

            }
            else
            {
                var specArgs = spec.Arguments is TupleTypeSpec ? spec.Arguments : new TupleTypeSpec(spec.Arguments);
                var thisArgs = Arguments is TupleTypeSpec ? Arguments : new TupleTypeSpec(Arguments);
                return BothNullOrEqual(thisArgs, specArgs) &&
                BothNullOrEqual(ReturnType, spec.ReturnType);
            }
        }

        public override bool HasDynamicSelf
        {
            get
            {
                if (Arguments.HasDynamicSelf)
                    return true;
                if (!IsNullOrEmptyTuple(ReturnType) && ReturnType.HasDynamicSelf)
                    return true;
                return false;
            }
        }
    }

    public class ProtocolListTypeSpec : TypeSpec
    {

        class SpecComparer : IComparer<TypeSpec>
        {
            public int Compare(TypeSpec x, TypeSpec y)
            {
                if (x == null)
                    throw new ArgumentNullException(nameof(x));
                if (y == null)
                    throw new ArgumentNullException(nameof(y));

                return StringComparer.Ordinal.Compare(x.ToString(), y.ToString());
            }
        }

        class SpecEqComparer : IEqualityComparer<TypeSpec>
        {
            bool partialNameMatch;
            public SpecEqComparer(bool partialNameMatch)
            {
                this.partialNameMatch = partialNameMatch;
            }

            public bool Equals(TypeSpec x, TypeSpec y)
            {
                if (partialNameMatch)
                    return x.EqualsPartialMatch(y);
                return x.Equals(y);
            }

            public int GetHashCode(TypeSpec obj)
            {
                throw new NotImplementedException();
            }
        }

        public ProtocolListTypeSpec()
            : base(TypeSpecKind.ProtocolList)
        {
            Protocols = new SortedList<NamedTypeSpec, bool>(new SpecComparer());
        }

        public ProtocolListTypeSpec(IEnumerable<NamedTypeSpec> protos)
            : this()
        {
            foreach (var proto in protos)
                Protocols.Add(proto, false);
        }

        public SortedList<NamedTypeSpec, bool> Protocols { get; private set; }

        protected override bool LLEquals(TypeSpec other, bool partialNameMatch)
        {
            var otherProtos = other as ProtocolListTypeSpec;
            if (otherProtos == null)
                return false;
            if (otherProtos.Protocols.Count != Protocols.Count)
                return false;
            var eqComparer = new SpecEqComparer(partialNameMatch);

            return Protocols.Keys.SequenceEqual(otherProtos.Protocols.Keys, eqComparer);
        }

        protected override string LLToString(bool useFullName)
        {
            return Protocols.Keys.Select(proto => proto.ToString()).InterleaveStrings(" & ");
        }

        public override bool HasDynamicSelf => false;
    }
}

