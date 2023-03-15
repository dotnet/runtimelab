// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using ILCompiler;
using ILCompiler.DependencyAnalysis;

using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.TypesDebugInfo;

namespace Internal.JitInterface
{
    sealed unsafe partial class CorInfoImpl : ITypesDebugInfoWriter
    {
        // We want to reuse the code inside UserDefinedTypeDescriptor for our debug info writing. That code assumes a  "push"
        // model, where something (object writer) is emitting types as it goes along. This is in contrast to the "pull" model
        // that the Jit/EE interface has, with the Jit asking us questions about debug shapes of types. To work around this,
        // we "push" the info on the shapes of types to the "_debugTypesShapes" array, to be queried when giving answers:
        //
        //  [Jit] getDebugShape(type) -> [EE] UDT.GetIndexForType -> [UDT] (emit required shapes with EE.Get<Type>Index) ->|
        //                                                                                                                 |
        //  [Jit] emitDebugType(shape) <- [EE] _definedTypesData[index] <- (note the shape may have already been emitted) <-
        //
        // An additional complication in this scheme is the fact we have no efficient way to go from a debug shape index to
        // the type it represents, and so have to use the index as the type representative across the Jit/EE interface, to
        // support cases where the shape needs to reference types.
        //
        private UserDefinedTypeDescriptor _debugTypesDescriptor;
        private ArrayBuilder<DebugTypeShape> _debugTypesShapes;
        private ArrayBuilder<MemberFunctionIdTypeDescriptor> _debugFunctions;
        private Dictionary<string, uint> _incompleteTypesMap;

        private UserDefinedTypeDescriptor DebugTypesDescriptor => _debugTypesDescriptor ??= InitializeLlvmDebugInfo();

        private UserDefinedTypeDescriptor InitializeLlvmDebugInfo()
        {
            // Make zero an illegal shape index.
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_COUNT, null));
            _incompleteTypesMap = new();

            return new(this, _compilation.NodeFactory);
        }

        public uint GetEnumTypeIndex(EnumTypeDescriptor enumTypeDescriptor, EnumRecordTypeDescriptor[] typeRecords)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_ENUM, (enumTypeDescriptor, typeRecords)));
            return index;
        }

        public uint GetClassTypeIndex(ClassTypeDescriptor classTypeDescriptor)
        {
            // Multiple type forwards may be requested for the same underlying type. Return the same index for all of of them to support later
            // updating the entry with a complete type.
            ref uint index = ref CollectionsMarshal.GetValueRefOrAddDefault(_incompleteTypesMap, classTypeDescriptor.Name, out bool exists);
            if (exists)
            {
                return index;
            }

            index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_UNDEF, null));
            return index;
        }

        public uint GetCompleteClassTypeIndex(ClassTypeDescriptor classTypeDescriptor, ClassFieldsTypeDescriptor classFieldsTypeDescriptor, DataFieldDescriptor[] fields, StaticDataFieldDescriptor[] statics)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_COMPOSITE, (classTypeDescriptor, classFieldsTypeDescriptor, fields, statics)));

            if (_incompleteTypesMap.TryGetValue(classTypeDescriptor.Name, out uint incompleteTypeIndex))
            {
                _debugTypesShapes[(int)incompleteTypeIndex] = new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_UNDEF, index);
            }
            return index;
        }

        public uint GetArrayTypeIndex(ClassTypeDescriptor classDescriptor, ArrayTypeDescriptor arrayTypeDescriprtor)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_ARRAY, (classDescriptor, arrayTypeDescriprtor)));
            return index;
        }

        public uint GetPointerTypeIndex(PointerTypeDescriptor pointerDescriptor)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_POINTER, pointerDescriptor));
            return index;
        }

        public uint GetMemberFunctionTypeIndex(MemberFunctionTypeDescriptor memberDescriptor, uint[] argumentTypes)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_FUNCTION, (memberDescriptor, argumentTypes)));
            return index;
        }

        public uint GetMemberFunctionId(MemberFunctionIdTypeDescriptor memberIdDescriptor)
        {
            // Note that function descriptors reside in a namespace distinct from "types".
            uint index = (uint)_debugFunctions.Count;
            _debugFunctions.Add(memberIdDescriptor);
            return index;
        }

        public uint GetPrimitiveTypeIndex(TypeDesc type)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_PRIMITIVE, type));
            return index;
        }

        public string GetMangledName(TypeDesc type)
        {
            return _compilation.NodeFactory.NameMangler.GetMangledTypeName(type);
        }

        struct DebugTypeShape
        {
            public CorInfoLlvmDebugTypeKind Kind;
            public object Data;

            public DebugTypeShape(CorInfoLlvmDebugTypeKind kind, object data) => (Kind, Data) = (kind, data);
        }

        enum CORINFO_LLVM_DEBUG_TYPE_HANDLE { }

        enum CorInfoLlvmDebugTypeKind
        {
            CORINFO_LLVM_DEBUG_TYPE_UNDEF,
            CORINFO_LLVM_DEBUG_TYPE_PRIMITIVE,
            CORINFO_LLVM_DEBUG_TYPE_COMPOSITE,
            CORINFO_LLVM_DEBUG_TYPE_ENUM,
            CORINFO_LLVM_DEBUG_TYPE_ARRAY,
            CORINFO_LLVM_DEBUG_TYPE_POINTER,
            CORINFO_LLVM_DEBUG_TYPE_FUNCTION,
            CORINFO_LLVM_DEBUG_TYPE_COUNT
        }

        struct CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO
        {
            public byte* Name;
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
            public uint Offset;
        }

        struct CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO
        {
            public byte* Name;
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
            public byte* BaseSymbolName;
            public uint StaticOffset;
            public int IsStaticDataInObject;
        }

        struct CORINFO_LLVM_COMPOSITE_TYPE_DEBUG_INFO
        {
            public byte* Name;
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE BaseClass;
            public uint Size;

            public uint InstanceFieldCount;
            public CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO* InstanceFields;

            public uint StaticFieldCount;
            public CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO* StaticFields;
        }

        struct CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO
        {
            public byte* Name;
            public ulong Value;
        }

        struct CORINFO_LLVM_ENUM_TYPE_DEBUG_INFO
        {
            public byte* Name;
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE ElementType;
            public ulong ElementCount;
            public CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO* Elements;
        }

        struct CORINFO_LLVM_ARRAY_TYPE_DEBUG_INFO
        {
            public byte* Name;
            public uint Rank;
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE ElementType;
            public int IsMultiDimensional;
        }

        struct CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO
        {
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE ElementType;
            public int IsReference;
        }

        struct CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO
        {
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE TypeOfThisPointer;
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE ReturnType;
            public uint NumberOfArguments;
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE* ArgumentTypes;
        }

        struct CORINFO_LLVM_TYPE_DEBUG_INFO
        {
            public CorInfoLlvmDebugTypeKind Kind;
            public CORINFO_LLVM_TYPE_DEBUG_INFO_UNION Union;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct CORINFO_LLVM_TYPE_DEBUG_INFO_UNION
        {
            [FieldOffset(0)]
            public CorInfoType PrimitiveType;
            [FieldOffset(0)]
            public CORINFO_LLVM_COMPOSITE_TYPE_DEBUG_INFO CompositeInfo;
            [FieldOffset(0)]
            public CORINFO_LLVM_ENUM_TYPE_DEBUG_INFO EnumInfo;
            [FieldOffset(0)]
            public CORINFO_LLVM_ARRAY_TYPE_DEBUG_INFO ArrayInfo;
            [FieldOffset(0)]
            public CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO PointerInfo;
            [FieldOffset(0)]
            public CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO FunctionInfo;
        }

        CORINFO_LLVM_DEBUG_TYPE_HANDLE GetDebugTypeForType(CORINFO_CLASS_STRUCT_* typeHandle)
        {
            TypeDesc type = HandleToObject(typeHandle);
            uint index = DebugTypesDescriptor.GetVariableTypeIndex(type);

            return IndexToDebugTypeHandle(index);
        }

        void GetDebugInfoForDebugType(CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle, CORINFO_LLVM_TYPE_DEBUG_INFO* pInfo)
        {
            int index = (int)DebugTypeHandleToIndex(debugTypeHandle);
            DebugTypeShape shape = _debugTypesShapes[index];

            pInfo->Kind = shape.Kind;
            switch (shape.Kind)
            {
                case CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_PRIMITIVE:
                    GetDebugInfoForPrimitiveType(shape.Data, &pInfo->Union.PrimitiveType);
                    break;
                case CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_COMPOSITE:
                    GetDebugInfoForCompositeType(shape.Data, &pInfo->Union.CompositeInfo);
                    break;
                case CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_ENUM:
                    GetDebugInfoForEnumType(shape.Data, &pInfo->Union.EnumInfo);
                    break;
                case CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_ARRAY:
                    GetDebugInfoForArrayType(shape.Data, &pInfo->Union.ArrayInfo);
                    break;
                case CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_POINTER:
                    GetDebugInfoForPointerType(shape.Data, &pInfo->Union.PointerInfo);
                    break;
                case CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_FUNCTION:
                    GetDebugInfoForFunctionType(shape.Data, &pInfo->Union.FunctionInfo);
                    break;
                default:
                    throw new UnreachableException();
            }
        }

        private void GetDebugInfoForPrimitiveType(object data, CorInfoType* pType)
        {
            TypeDesc type = (TypeDesc)data;
            Debug.Assert(type.IsPrimitive);

            *pType = asCorInfoType(type);
        }

        private void GetDebugInfoForCompositeType(object data, CORINFO_LLVM_COMPOSITE_TYPE_DEBUG_INFO* pInfo)
        {
            var (descriptor, _, fields, statics) = ((ClassTypeDescriptor, ClassFieldsTypeDescriptor, DataFieldDescriptor[], StaticDataFieldDescriptor[]))data;

            pInfo->Name = ToPinnedUtf8String(descriptor.Name);
            pInfo->BaseClass = IndexToDebugTypeHandle(descriptor.BaseClassId);
            pInfo->Size = checked((uint)descriptor.InstanceSize);

            int instanceFieldCount = fields.Length - statics.Length;
            var instanceFieldsInfo = instanceFieldCount != 0 ? new CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO[instanceFieldCount] : null;
            int staticFieldCount = statics.Length;
            var staticFieldsInfo = staticFieldCount != 0 ? new CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO[staticFieldCount] : null;

            for (int i = 0, s = 0, j = 0; j < fields.Length; j++)
            {
                DataFieldDescriptor field = fields[j];
                byte* pName = ToPinnedUtf8String(field.Name);

                if (field.Offset == 0xFFFFFFFF) // Static field.
                {
                    StaticDataFieldDescriptor staticField = statics[s];
                    ref CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO fieldInfo = ref staticFieldsInfo[s++];
                    fieldInfo.Name = pName;
                    fieldInfo.Type = IndexToDebugTypeHandle(field.FieldTypeIndex);
                    fieldInfo.BaseSymbolName = ToPinnedUtf8String(staticField.StaticDataName);
                    fieldInfo.StaticOffset = checked((uint)staticField.StaticOffset);
                    fieldInfo.IsStaticDataInObject = staticField.IsStaticDataInObject;
                }
                else
                {
                    ref CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO fieldInfo = ref instanceFieldsInfo[i++];
                    fieldInfo.Name = pName;
                    fieldInfo.Type = IndexToDebugTypeHandle(field.FieldTypeIndex);
                    fieldInfo.Offset = checked((uint)field.Offset);
                }
            }

            pInfo->InstanceFieldCount = (uint)instanceFieldCount;
            pInfo->InstanceFields = instanceFieldsInfo != null ? (CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO*)GetPin(instanceFieldsInfo) : null;
            pInfo->StaticFieldCount = (uint)staticFieldCount;
            pInfo->StaticFields = staticFieldsInfo != null ? (CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO*)GetPin(staticFieldsInfo) : null;
        }

        private void GetDebugInfoForEnumType(object data, CORINFO_LLVM_ENUM_TYPE_DEBUG_INFO* pInfo)
        {
            var (descriptor, elements) = ((EnumTypeDescriptor, EnumRecordTypeDescriptor[]))data;

            pInfo->ElementType = IndexToDebugTypeHandle(descriptor.ElementType);
            pInfo->Name = ToPinnedUtf8String(descriptor.Name);
            pInfo->ElementCount = descriptor.ElementCount;

            CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO[] elementsInfo = new CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO[elements.Length];
            for (int i = 0; i < elementsInfo.Length; i++)
            {
                EnumRecordTypeDescriptor element = elements[i];
                ref CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO elementInfo = ref elementsInfo[i];
                elementInfo.Name = ToPinnedUtf8String(element.Name);
                elementInfo.Value = element.Value;
            }

            pInfo->Elements = (CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO*)GetPin(elementsInfo);
        }

        private void GetDebugInfoForArrayType(object data, CORINFO_LLVM_ARRAY_TYPE_DEBUG_INFO* pInfo)
        {
            var (classDescriptor, arrayDescriptor) = ((ClassTypeDescriptor, ArrayTypeDescriptor))data;

            pInfo->Name = ToPinnedUtf8String(classDescriptor.Name);
            pInfo->Rank = arrayDescriptor.Rank;
            pInfo->ElementType = IndexToDebugTypeHandle(arrayDescriptor.ElementType);
            pInfo->IsMultiDimensional = arrayDescriptor.IsMultiDimensional;
        }

        private void GetDebugInfoForPointerType(object data, CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO* pInfo)
        {
            var descriptor = (PointerTypeDescriptor)data;

            pInfo->ElementType = IndexToDebugTypeHandle(descriptor.ElementType);
            pInfo->IsReference = descriptor.IsReference;
        }

        private void GetDebugInfoForFunctionType(object data, CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO* pInfo)
        {
            var (descriptor, argumentTypes) = ((MemberFunctionTypeDescriptor, uint[]))data;

            uint voidTypeIndex = DebugTypesDescriptor.GetTypeIndex(_compilation.TypeSystemContext.GetWellKnownType(WellKnownType.Void), needsCompleteType: true);
            if (descriptor.TypeIndexOfThisPointer == voidTypeIndex)
            {
                pInfo->TypeOfThisPointer = 0;
            }
            else
            {
                pInfo->TypeOfThisPointer = IndexToDebugTypeHandle(descriptor.TypeIndexOfThisPointer);
            }

            pInfo->ReturnType = IndexToDebugTypeHandle(descriptor.ReturnType);
            pInfo->NumberOfArguments = descriptor.NumberOfArguments;
            pInfo->ArgumentTypes = (CORINFO_LLVM_DEBUG_TYPE_HANDLE*)GetPin(argumentTypes);
        }

        struct CORINFO_LLVM_VARIABLE_DEBUG_INFO
        {
            public byte* Name;
            public uint VarNumber;
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
        }

        struct CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO
        {
            public uint ILOffset;
            public uint LineNumber;
        }

        struct CORINFO_LLVM_METHOD_DEBUG_INFO
        {
            public byte* Name;
            public byte* Directory;
            public byte* FileName;
            public uint LineNumberCount;
            public CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO* SortedLineNumbers;
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE OwnerType;
            public CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
            public uint VariableCount;
            public CORINFO_LLVM_VARIABLE_DEBUG_INFO* Variables;
        }

        private void GetDebugInfoForMethod(CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo)
        {
            MethodDesc method = _methodCodeNode.Method;
            int methodIndex = (int)DebugTypesDescriptor.GetMethodFunctionIdTypeIndex(method);
            MemberFunctionIdTypeDescriptor descriptor = _debugFunctions[methodIndex];

            pInfo->Name = ToPinnedUtf8String(descriptor.Name);
            pInfo->OwnerType = IndexToDebugTypeHandle(descriptor.ParentClass);
            pInfo->Type = IndexToDebugTypeHandle(descriptor.MemberFunction);

            string documentPath = null;
            ArrayBuilder<CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO> lineNumbersBuilder = new();
            foreach (ILSequencePoint sequencePoint in _debugInfo.GetSequencePoints())
            {
                lineNumbersBuilder.Add(new() { ILOffset = (uint)sequencePoint.Offset, LineNumber = (uint)sequencePoint.LineNumber });
                documentPath ??= sequencePoint.Document;
            }

            if (documentPath != null)
            {
                pInfo->Directory = ToPinnedUtf8String(Path.GetDirectoryName(documentPath));
                pInfo->FileName = ToPinnedUtf8String(Path.GetFileName(documentPath));
            }
            else
            {
                pInfo->Directory = null;
                pInfo->FileName = null;
            }

            CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO[] lineNumbers = lineNumbersBuilder.ToArray();
            Array.Sort(lineNumbers, static (x, y) => (int)(x.ILOffset - y.ILOffset));

            pInfo->LineNumberCount = (uint)lineNumbers.Length;
            pInfo->SortedLineNumbers = (CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO*)GetPin(lineNumbers);

            bool isStateMachineMoveNextMethod = _debugInfo.IsStateMachineMoveNextMethod;
            MethodSignature sig = method.Signature;
            int offset = sig.IsStatic ? 0 : 1;
            int parameterCount = sig.Length + offset;
            uint variableCount = 0;

            int i = 0;
            string[] parameterNames = new string[parameterCount];
            foreach (var paramName in _debugInfo.GetParameterNames())
            {
                string assignedName;
                if (!sig.IsStatic && i == 0)
                {
                    assignedName = isStateMachineMoveNextMethod ? "locals" : "this";
                }
                else
                {
                    assignedName = paramName;
                }

                if (assignedName != null)
                {
                    parameterNames[i] = assignedName;
                    variableCount++;
                }
                i++;
            }

            MethodIL methodIL = (MethodIL)HandleToObject(_methodScope);
            LocalVariableDefinition[] locals = methodIL.GetLocals();

            string[] localNames = new string[locals.Length];
            foreach (var local in _debugInfo.GetLocalVariables())
            {
                if (!local.CompilerGenerated && local.Slot < localNames.Length && localNames[local.Slot] != local.Name)
                {
                    localNames[local.Slot] = local.Name;
                    variableCount++;
                }
            }

            i = 0;
            CORINFO_LLVM_VARIABLE_DEBUG_INFO[] variables = new CORINFO_LLVM_VARIABLE_DEBUG_INFO[variableCount];
            for (int num = 0; num < parameterNames.Length + locals.Length; num++)
            {
                string name;
                uint typeIndex;
                if (num < parameterCount)
                {
                    // This is a parameter
                    if (!sig.IsStatic && num == 0)
                    {
                        // We emit special types for async state machines (see also "ObjectWriter.EmitDebugVar").
                        typeIndex = isStateMachineMoveNextMethod
                            ? DebugTypesDescriptor.GetStateMachineThisVariableTypeIndex(method.OwningType)
                            : DebugTypesDescriptor.GetThisTypeIndex(method.OwningType);
                    }
                    else
                    {
                        typeIndex = DebugTypesDescriptor.GetVariableTypeIndex(method.Signature[num - offset]);
                    }

                    name = parameterNames[num];
                }
                else
                {
                    // This is a local
                    int localNumber = num - parameterCount;
                    typeIndex = DebugTypesDescriptor.GetVariableTypeIndex(locals[localNumber].Type);
                    name = localNames[localNumber];
                }

                if (name != null)
                {
                    ref CORINFO_LLVM_VARIABLE_DEBUG_INFO info = ref variables[i++];
                    info.Name = ToPinnedUtf8String(name);
                    info.VarNumber = (uint)num;
                    info.Type = IndexToDebugTypeHandle(typeIndex);
                }
            }
            Debug.Assert(i == variableCount);

            pInfo->VariableCount = variableCount;
            pInfo->Variables = (CORINFO_LLVM_VARIABLE_DEBUG_INFO*)GetPin(variables);
        }

        private CORINFO_LLVM_DEBUG_TYPE_HANDLE IndexToDebugTypeHandle(uint index)
        {
            // Replace incomplete type references with complete ones so that the Jit never sees them.
            DebugTypeShape shape = _debugTypesShapes[(int)index];
            if (shape.Kind == CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_UNDEF)
            {
                Debug.Assert(shape.Data is not null);
                index = (uint)shape.Data;
            }

            return (CORINFO_LLVM_DEBUG_TYPE_HANDLE)index;
        }

        private static uint DebugTypeHandleToIndex(CORINFO_LLVM_DEBUG_TYPE_HANDLE handle) => (uint)handle;

        private byte* ToPinnedUtf8String(string name) => (byte*)GetPin(StringToUTF8(name));
    }
}
