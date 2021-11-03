// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    internal static partial class LazyGenericsSupport
    {
        private class ModuleCycleInfo
        {
            private readonly HashSet<TypeSystemEntity> _entitiesInCycles;

            public EcmaModule Module { get; }

            public ModuleCycleInfo(EcmaModule module, HashSet<TypeSystemEntity> entitiesInCycles)
            {
                Module = module;
                _entitiesInCycles = entitiesInCycles;
            }

            public bool FormsCycle(TypeSystemEntity owner)
            {
                Debug.Assert(owner is EcmaMethod || owner is EcmaType);
                TypeDesc ownerType = (owner as EcmaMethod)?.OwningType;
                return _entitiesInCycles.Contains(owner) || (ownerType != null && _entitiesInCycles.Contains(ownerType));
            }

            // Chosen rather arbitrarily. For the app that I was looking at, cutoff point of 7 compiled
            // more than 10 minutes on a release build of the compiler, and I lost patience.
            // Cutoff point of 5 produced an 1.7 GB object file.
            // Cutoff point of 4 produced an 830 MB object file.
            // Cutoff point of 3 produced an 470 MB object file.
            // We want this to be high enough so that it doesn't cut off too early. But also not too
            // high because things that are recursive often end up expanding laterally as well
            // through various other generic code the deep code calls into.
            private const int CutoffPoint = 4;

            public bool IsDeepPossiblyCyclicInstantiation(TypeSystemEntity entity)
            {
                if (entity is TypeDesc type)
                {
                    return IsDeepPossiblyCyclicInstantiation(type);
                }
                else
                {
                    return IsDeepPossiblyCyclicInstantiation((MethodDesc)entity);
                }
            }

            public bool IsDeepPossiblyCyclicInstantiation(TypeDesc type, List<TypeDesc> seenTypes = null)
            {
                switch (type.Category)
                {
                    case TypeFlags.Array:
                    case TypeFlags.SzArray:
                        return IsDeepPossiblyCyclicInstantiation(((ParameterizedType)type).ParameterType, seenTypes);
                    default:
                        TypeDesc typeDef = type.GetTypeDefinition();
                        if (type != typeDef)
                        {
                            (seenTypes ??= new List<TypeDesc>()).Add(typeDef);
                            for (int i = 0; i < seenTypes.Count; i++)
                            {
                                TypeDesc typeToFind = seenTypes[i];
                                int count = 1;
                                for (int j = i + 1; j < seenTypes.Count; j++)
                                {
                                    if (seenTypes[j] == typeToFind)
                                    {
                                        count++;
                                    }

                                    if (count > CutoffPoint)
                                    {
                                        return true;
                                    }
                                }
                            }

                            bool result = IsDeepPossiblyCyclicInstantiation(type.Instantiation, seenTypes);
                            seenTypes.RemoveAt(seenTypes.Count - 1);
                            return result;
                        }
                        return false;
                }
            }

            private bool IsDeepPossiblyCyclicInstantiation(Instantiation instantiation, List<TypeDesc> seenTypes = null)
            {
                foreach (TypeDesc arg in instantiation)
                {
                    if (IsDeepPossiblyCyclicInstantiation(arg, seenTypes))
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool IsDeepPossiblyCyclicInstantiation(MethodDesc method)
            {
                return IsDeepPossiblyCyclicInstantiation(method.Instantiation) || IsDeepPossiblyCyclicInstantiation(method.OwningType);
            }
        }

        private class CycleInfoHashtable : LockFreeReaderHashtable<EcmaModule, ModuleCycleInfo>
        {
            protected override bool CompareKeyToValue(EcmaModule key, ModuleCycleInfo value) => key == value.Module;
            protected override bool CompareValueToValue(ModuleCycleInfo value1, ModuleCycleInfo value2) => value1.Module == value2.Module;
            protected override int GetKeyHashCode(EcmaModule key) => key.GetHashCode();
            protected override int GetValueHashCode(ModuleCycleInfo value) => value.Module.GetHashCode();

            protected override ModuleCycleInfo CreateValueFromKey(EcmaModule key)
            {
                GraphBuilder gb = new GraphBuilder(key);
                Graph<EcmaGenericParameter> graph = gb.Graph;

                var formalsNeedingLazyGenerics = graph.ComputeVerticesInvolvedInAFlaggedCycle();
                var entitiesNeedingLazyGenerics = new HashSet<TypeSystemEntity>();

                foreach (EcmaGenericParameter formal in formalsNeedingLazyGenerics)
                {
                    var formalDefinition = key.MetadataReader.GetGenericParameter(formal.Handle);
                    if (formal.Kind == GenericParameterKind.Type)
                    {
                        entitiesNeedingLazyGenerics.Add(key.GetType(formalDefinition.Parent));
                    }
                    else
                    {
                        entitiesNeedingLazyGenerics.Add(key.GetMethod(formalDefinition.Parent));
                    }
                }

                return new ModuleCycleInfo(key, entitiesNeedingLazyGenerics);
            }
        }

        internal class GenericCycleDetector
        {
            private readonly CycleInfoHashtable _hashtable = new CycleInfoHashtable();

            public void DetectCycle(TypeSystemEntity owner, TypeSystemEntity referent)
            {
                // Not clear if generic recursion through fields is a thing
                if (referent is FieldDesc)
                {
                    return;
                }

                var ownerType = owner as TypeDesc;
                var ownerMethod = owner as MethodDesc;
                var ownerDefinition = ownerType?.GetTypeDefinition() ?? (TypeSystemEntity)ownerMethod.GetTypicalMethodDefinition();
                var referentType = referent as TypeDesc;
                var referentMethod = referent as MethodDesc;
                var referentDefinition = referentType?.GetTypeDefinition() ?? (TypeSystemEntity)referentMethod.GetTypicalMethodDefinition();

                // We don't track cycles in non-ecma entities.
                if ((referentDefinition is not EcmaMethod && referentDefinition is not EcmaType)
                    || (ownerDefinition is not EcmaMethod && ownerDefinition is not EcmaType))
                {
                    return;
                }

                EcmaModule ownerModule = (ownerDefinition as EcmaType)?.EcmaModule ?? ((EcmaMethod)ownerDefinition).Module;

                ModuleCycleInfo cycleInfo = _hashtable.GetOrCreateValue(ownerModule);
                if (cycleInfo.FormsCycle(ownerDefinition))
                {
                    // Just the presence of a cycle is not a problem, but once we start getting too deep,
                    // we need to cut our losses.
                    if (cycleInfo.IsDeepPossiblyCyclicInstantiation(referent))
                    {
                        if (referentType != null)
                        {
                            // TODO: better exception string ID?
                            ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, referentType);
                        }
                        else
                        {
                            // TODO: better exception string ID?
                            ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, referentMethod);
                        }
                    }
                }
            }
        }
    }
}
