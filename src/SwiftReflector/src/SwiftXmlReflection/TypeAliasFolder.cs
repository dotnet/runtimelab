// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SwiftReflector.TypeMapping;

namespace SwiftReflector.SwiftXmlReflection
{
    public class TypeAliasFolder
    {
        Dictionary<string, TypeAliasDeclaration> aliases;

        public TypeAliasFolder(IList<TypeAliasDeclaration> aliases)
        {
            this.aliases = new Dictionary<string, TypeAliasDeclaration>();
            foreach (var alias in aliases)
            {
                this.aliases.Add(AliasKey(alias.TypeSpec), alias);
            }
        }

        public void AddDatabaseAliases(TypeDatabase typeDatabase)
        {
            if (typeDatabase == null)
                return;
            foreach (var moduleName in typeDatabase.ModuleNames)
            {
                var moduleDB = typeDatabase.ModuleDatabaseForModuleName(moduleName);
                foreach (var alias in moduleDB.TypeAliases)
                {
                    this.aliases.Add(AliasKey(alias.TypeSpec), alias);
                }
            }
        }

        public TypeSpec FoldAlias(BaseDeclaration context, TypeSpec original)
        {
            if (aliases.Count == 0)
                return original;

            var changed = false;
            while (true)
            {
                original = FoldAlias(context, original, out changed);
                if (!changed)
                    return original;
            }
        }

        TypeSpec FoldAlias(BaseDeclaration context, TypeSpec spec, out bool changed)
        {
            switch (spec.Kind)
            {
                case TypeSpecKind.Named:
                    return FoldAlias(context, spec as NamedTypeSpec, out changed);
                case TypeSpecKind.Closure:
                    return FoldAlias(context, spec as ClosureTypeSpec, out changed);
                case TypeSpecKind.ProtocolList:
                    return FoldAlias(context, spec as ProtocolListTypeSpec, out changed);
                case TypeSpecKind.Tuple:
                    return FoldAlias(context, spec as TupleTypeSpec, out changed);
                default:
                    throw new ArgumentOutOfRangeException(nameof(spec));
            }

        }

        TypeSpec FoldAlias(BaseDeclaration context, TupleTypeSpec spec, out bool changed)
        {
            changed = false;
            TypeSpec[] newContents = spec.Elements.ToArray();
            for (int i = 0; i < newContents.Length; i++)
            {
                var elemChanged = false;
                newContents[i] = FoldAlias(context, newContents[i], out elemChanged);
                changed = changed || elemChanged;
            }
            if (changed)
            {
                var newTuple = new TupleTypeSpec(newContents);
                newTuple.Attributes.AddRange(spec.Attributes);
                return newTuple;
            }
            return spec;
        }

        TypeSpec FoldAlias(BaseDeclaration context, ClosureTypeSpec spec, out bool changed)
        {
            var returnChanged = false;
            var returnSpec = FoldAlias(context, spec.ReturnType, out returnChanged);

            var argsChanged = false;
            var args = FoldAlias(context, spec.Arguments, out argsChanged);

            changed = returnChanged || argsChanged;
            if (changed)
            {
                var newSpec = new ClosureTypeSpec(args, returnSpec);
                newSpec.Attributes.AddRange(spec.Attributes);
                return newSpec;
            }
            return spec;
        }

        TypeSpec FoldAlias(BaseDeclaration context, ProtocolListTypeSpec spec, out bool changed)
        {
            changed = false;
            var protos = new NamedTypeSpec[spec.Protocols.Count];

            var protoChanged = false;
            var i = 0;
            foreach (var proto in spec.Protocols.Keys)
            {
                protos[i] = FoldAlias(context, proto, out protoChanged) as NamedTypeSpec;
                changed = changed || protoChanged;
            }
            if (changed)
            {
                var newProtoList = new ProtocolListTypeSpec(protos);
                newProtoList.Attributes.AddRange(spec.Attributes);
                return newProtoList;
            }
            return spec;
        }

        TypeSpec FoldAlias(BaseDeclaration context, NamedTypeSpec spec, out bool changed)
        {
            if (context == null || !context.IsTypeSpecGenericReference(spec))
            {
                TypeAliasDeclaration decl = null;
                if (aliases.TryGetValue(spec.Name, out decl))
                {
                    changed = true;
                    var newNamedSpec = RemapAliasedTypeSpec(spec, decl);
                    newNamedSpec.Attributes.AddRange(spec.Attributes);
                    return newNamedSpec;
                }
                else
                {
                    return FoldGenerics(context, spec, out changed);
                }
            }
            else
            {
                return FoldGenerics(context, spec, out changed);
            }
        }

        TypeSpec FoldGenerics(BaseDeclaration context, NamedTypeSpec spec, out bool changed)
        {
            changed = false;
            if (!spec.ContainsGenericParameters)
                return spec;
            var genericsChanged = false;
            var newGenerics = spec.GenericParameters.ToArray();
            for (int i = 0; i < newGenerics.Length; i++)
            {
                var genericChanged = false;
                newGenerics[i] = FoldAlias(context, newGenerics[i], out genericChanged);
                genericsChanged = genericsChanged || genericChanged;
            }
            if (genericsChanged)
            {
                changed = true;
                var newNamedSpec = new NamedTypeSpec(spec.Name, newGenerics);
                newNamedSpec.Attributes.AddRange(spec.Attributes);
                return newNamedSpec;
            }
            return spec;
        }

        TypeSpec RemapAliasedTypeSpec(NamedTypeSpec source, TypeAliasDeclaration decl)
        {
            // OK  - in the Decl, we're going to have something like:
            // Name = SomeOtherType
            // or we'll have
            // Name<gen1, gen2, ...> = SomeOtherType<t1, t2, ...>
            // The first case is easy. In the second case we need to look
            // at the t1 and find out where it comes from in Name<...>
            // and remap it using what was provided in the source.
            // But of course this get complicated.
            // You could have something like this:
            // typealias Foo<T> = UnsafeMutablePointer<(Int, T)>
            // So we need a map from each generic argument in Foo<T> to
            // each generic argument in source.
            // Then we need to build a new TypeSpec using the declaration's target
            // type substituting in elements from the map.
            // and it gets more complicated because the thing we're looking at may
            // be an associated type.
            //
            // Here's an example:
            //public protocol KVPish
            //{
            //    associatedtype Key : Hashable
            //    associatedtype Value
            //    func contains(a: Key) -> Bool
            //    func get(a: Key) -> Value
            //}
            //
            //public typealias KPHolder<a: KVPish> = Dictionary<a.Key, a.Value>
            var genericMap = new Dictionary<string, TypeSpec>();
            if (decl.TypeSpec.ContainsGenericParameters)
            {
                for (int i = 0; i < decl.TypeSpec.GenericParameters.Count; i++)
                {
                    // the "parts" here are part of a formal generic declaration
                    // and they HAVE to be named type specs and they themselves
                    // won't ever be generic. They're going to just be a name.
                    // Future Steve: trust me.
                    var part = decl.TypeSpec.GenericParameters[i] as NamedTypeSpec;
                    genericMap.Add(part.Name, source.GenericParameters[i]);
                }
            }
            return RemapTypeSpec(decl.TargetTypeSpec, genericMap);
        }

        TypeSpec RemapTypeSpec(TypeSpec spec, Dictionary<string, TypeSpec> nameMap)
        {
            switch (spec.Kind)
            {
                case TypeSpecKind.Closure:
                    return RemapTypeSpec(spec as ClosureTypeSpec, nameMap);
                case TypeSpecKind.Named:
                    return RemapTypeSpec(spec as NamedTypeSpec, nameMap);
                case TypeSpecKind.ProtocolList:
                    return RemapTypeSpec(spec as ProtocolListTypeSpec, nameMap);
                case TypeSpecKind.Tuple:
                    return RemapTypeSpec(spec as TupleTypeSpec, nameMap);
                default:
                    throw new NotImplementedException($"Unknown type spec kind {spec.Kind}");
            }
        }

        TypeSpec RemapTypeSpec(TupleTypeSpec tuple, Dictionary<string, TypeSpec> nameMap)
        {
            var tupleElems = tuple.Elements.ToArray();
            for (int i = 0; i < tupleElems.Length; i++)
            {
                tupleElems[i] = RemapTypeSpec(tupleElems[i], nameMap);
            }
            return new TupleTypeSpec(tupleElems);
        }

        TypeSpec RemapTypeSpec(ClosureTypeSpec clos, Dictionary<string, TypeSpec> nameMap)
        {
            var returnType = RemapTypeSpec(clos.ReturnType, nameMap);
            var args = RemapTypeSpec(clos.Arguments, nameMap);
            return new ClosureTypeSpec(args, returnType);
        }

        TypeSpec RemapTypeSpec(ProtocolListTypeSpec proto, Dictionary<string, TypeSpec> nameMap)
        {
            return new ProtocolListTypeSpec(proto.Protocols.Keys.Select(k => RemapTypeSpec(k, nameMap) as NamedTypeSpec));
        }

        TypeSpec RemapTypeSpec(NamedTypeSpec named, Dictionary<string, TypeSpec> nameMap)
        {
            var parts = named.Name.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                TypeSpec replacement;
                if (nameMap.TryGetValue(parts[i], out replacement))
                {
                    parts[i] = replacement.ToString();
                }
            }
            var newName = parts.InterleaveStrings(".");
            if (named.ContainsGenericParameters)
            {
                var newParams = named.GenericParameters.Select(p => RemapTypeSpec(p, nameMap)).ToArray();
                return new NamedTypeSpec(newName, newParams);
            }
            else
            {
                return new NamedTypeSpec(newName);
            }
        }

        static string AliasKey(TypeSpec spec)
        {
            if (spec is NamedTypeSpec named)
                return named.Name;
            return spec.ToString();
        }
    }
}
