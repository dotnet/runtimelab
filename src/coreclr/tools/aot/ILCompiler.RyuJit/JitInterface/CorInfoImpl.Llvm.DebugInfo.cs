// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using ILCompiler;
using ILCompiler.DependencyAnalysis;

using Internal.IL;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.TypesDebugInfo;

namespace Internal.JitInterface
{
    // Our debug info support is structured as follows:
    // 1. When emitting code, we only emit definitions for basic types and pointers, everything else - enums, arrays, structs,
    //    becomes a declaration.
    // 2. At the end of the compilation, one "debug" module is emitted, consisting wholly of DWARF definitions for all of the
    //    various complex types.
    //
    // This design was driven by the following constraints:
    // 1) "UserDefinedTypeDescriptor" (aka UDTD), our DI 'backend', relies on the marking having been complete to determine
    //    things like what static and instance fields should be included.
    // 2) The way C++ DWARF is structured, a class definition must include all methods belonging to the class, as declarations.
    //    Since we do not want the compilation partitioning to be constrained by DI rules, the only way to achieve this while
    //    having methods spread across compile units is by emitting the definition after marking.
    // 3) We do not want to duplicate DI information; this slows everything down. Because we can run DI emit in parallel with
    //    object writing, which is single-threaded anyway, 'phase 2' is essentially free TP-wise.
    //
    // Notably, the current implementation has a lot of room for TP improvement: we don't need all the information UDTD provides
    // in the compilation phase, and we should probably switch to the "push" model there too. Changing the former would imply
    // changing upstream code, however.
    // TODO-LLVM-Upstream: augment UDTD to allow only requesting what we need.
    //
    internal sealed unsafe partial class CorInfoImpl : ITypesDebugInfoWriter
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

        private UserDefinedTypeDescriptor DebugTypes => _debugTypesDescriptor ??= InitializeLlvmDebugInfo();

        private UserDefinedTypeDescriptor InitializeLlvmDebugInfo()
        {
            // Make zero an illegal shape index.
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_COUNT, null));
            return new(this, _compilation.NodeFactory);
        }

        uint ITypesDebugInfoWriter.GetEnumTypeIndex(EnumTypeDescriptor enumType, EnumRecordTypeDescriptor[] typeRecords)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD, (enumType.Name, CorInfoLlvmDebugTypeForwardKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD_ENUM)));
            return index;
        }

        uint ITypesDebugInfoWriter.GetClassTypeIndex(ClassTypeDescriptor classType)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD, (classType.Name, CorInfoLlvmDebugTypeForwardKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD_STRUCT)));
            return index;
        }

        uint ITypesDebugInfoWriter.GetCompleteClassTypeIndex(ClassTypeDescriptor classType, ClassFieldsTypeDescriptor classFields, DataFieldDescriptor[] fields, StaticDataFieldDescriptor[] statics)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD, (classType.Name, CorInfoLlvmDebugTypeForwardKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD_STRUCT)));
            return index;
        }

        uint ITypesDebugInfoWriter.GetArrayTypeIndex(ClassTypeDescriptor classType, ArrayTypeDescriptor arrayType)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD, (classType.Name, CorInfoLlvmDebugTypeForwardKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD_STRUCT)));
            return index;
        }

        uint ITypesDebugInfoWriter.GetPointerTypeIndex(PointerTypeDescriptor pointerType)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_POINTER, pointerType));
            return index;
        }

        uint ITypesDebugInfoWriter.GetMemberFunctionTypeIndex(MemberFunctionTypeDescriptor funcType, uint[] argumentTypes)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_FUNCTION, (funcType, argumentTypes)));
            return index;
        }

        uint ITypesDebugInfoWriter.GetMemberFunctionId(MemberFunctionIdTypeDescriptor method)
        {
            // Note that function descriptors reside in a namespace distinct from "types".
            uint index = (uint)_debugFunctions.Count;
            _debugFunctions.Add(method);
            return index;
        }

        uint ITypesDebugInfoWriter.GetPrimitiveTypeIndex(TypeDesc type)
        {
            uint index = (uint)_debugTypesShapes.Count;
            _debugTypesShapes.Add(new(CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_PRIMITIVE, type));
            return index;
        }

        string ITypesDebugInfoWriter.GetMangledName(TypeDesc type) => GetDebugTypeName(type);

        private struct DebugTypeShape
        {
            public CorInfoLlvmDebugTypeKind Kind;
            public object Data;

            public DebugTypeShape(CorInfoLlvmDebugTypeKind kind, object data) => (Kind, Data) = (kind, data);
        }

        private CORINFO_LLVM_DEBUG_TYPE_HANDLE GetDebugTypeForType(CORINFO_CLASS_STRUCT_* typeHandle)
        {
            TypeDesc type = HandleToObject(typeHandle);
            uint index = DebugTypes.GetVariableTypeIndex(type);

            return IndexToDebugTypeHandle(index);
        }

        private void GetDebugInfoForDebugType(CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle, CORINFO_LLVM_TYPE_DEBUG_INFO* pInfo)
        {
            int index = (int)DebugTypeHandleToIndex(debugTypeHandle);
            DebugTypeShape shape = _debugTypesShapes[index];

            pInfo->Kind = shape.Kind;
            switch (shape.Kind)
            {
                case CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_PRIMITIVE:
                    GetDebugInfoForPrimitiveType(shape.Data, &pInfo->Union.PrimitiveType);
                    break;
                case CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_POINTER:
                    GetDebugInfoForPointerType(shape.Data, &pInfo->Union.PointerInfo);
                    break;
                case CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD:
                    GetDebugInfoForForwardType(shape.Data, &pInfo->Union.ForwardInfo);
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

        private void GetDebugInfoForPointerType(object data, CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO* pInfo)
        {
            var descriptor = (PointerTypeDescriptor)data;

            pInfo->ElementType = IndexToDebugTypeHandle(descriptor.ElementType);
            pInfo->IsReference = descriptor.IsReference;
        }

        private void GetDebugInfoForForwardType(object data, CORINFO_LLVM_FORWARD_TYPE_DEBUG_INFO* pInfo)
        {
            var (name, kind) = ((string, CorInfoLlvmDebugTypeForwardKind))data;

            pInfo->Kind = kind;
            pInfo->Name = ToPinnedUtf8String(name);
        }

        private void GetDebugInfoForFunctionType(object data, CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO* pInfo)
        {
            var (descriptor, argumentTypes) = ((MemberFunctionTypeDescriptor, uint[]))data;

            if (_debugTypesShapes[(int)descriptor.TypeIndexOfThisPointer] is
                {
                    Kind: CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_PRIMITIVE,
                    Data: TypeDesc { Category: TypeFlags.Void }
                })
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

        private void GetDebugInfoForCurrentMethod(CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo)
        {
            *pInfo = default;
            MethodDesc method = _methodCodeNode.Method;

            string documentPath = null;
            ArrayBuilder<CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO> lineNumbersBuilder = default;
            foreach (ILSequencePoint sequencePoint in _debugInfo.GetSequencePoints())
            {
                lineNumbersBuilder.Add(new() { ILOffset = (uint)sequencePoint.Offset, LineNumber = (uint)sequencePoint.LineNumber });
                documentPath ??= sequencePoint.Document;
            }

            if (documentPath == null)
            {
                _debugInfo = null; // Remember there is no debug info for this method.
                return;
            }

            uint declIndex = DebugTypes.GetMethodFunctionIdTypeIndex(method);
            MemberFunctionIdTypeDescriptor decl = _debugFunctions[(int)declIndex];
            Utf8String linkageName = _compilation.NameMangler.GetMangledMethodName(decl.Method);
            pInfo->Decl.Name = ToPinnedUtf8String(decl.Name);
            pInfo->Decl.LinkageName.Data = (byte*)GetPin(linkageName.Value);
            pInfo->Decl.LinkageName.Length = (nuint)linkageName.Length;
            pInfo->Decl.Type = IndexToDebugTypeHandle(decl.MemberFunction);
            pInfo->Decl.OwnerType = IndexToDebugTypeHandle(decl.ParentClass);

            pInfo->Directory = ToPinnedUtf8String(Path.GetDirectoryName(documentPath));
            pInfo->FileName = ToPinnedUtf8String(Path.GetFileName(documentPath));

            CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO[] lineNumbers = lineNumbersBuilder.ToArray();
            Array.Sort(lineNumbers, static (x, y) => (int)(x.ILOffset - y.ILOffset));

            pInfo->LineNumberCount = (uint)lineNumbers.Length;
            pInfo->SortedLineNumbers = (CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO*)GetPin(lineNumbers);

            MethodIL methodIL = (MethodIL)HandleToObject(_methodScope);
            IEnumerable<DebugVarInfoMetadata> debugVars =
                GetDebugVarsForMethod(method, methodIL.GetLocals(), static l => l.Type, _debugInfo, out uint variableCount);

            int i = 0;
            CORINFO_LLVM_VARIABLE_DEBUG_INFO[] variables = new CORINFO_LLVM_VARIABLE_DEBUG_INFO[variableCount];
            foreach (DebugVarInfoMetadata debugVar in debugVars)
            {
                ref CORINFO_LLVM_VARIABLE_DEBUG_INFO info = ref variables[i++];
                info.Name = ToPinnedUtf8String(debugVar.Name);
                info.VarNumber = debugVar.DebugVarInfo.VarNumber;

                uint variableTypeIndex;
                if (info.VarNumber == 0 && !method.Signature.IsStatic)
                {
                    // We emit special types for async state machines (see also "ObjectWriter.EmitDebugVar").
                    variableTypeIndex = _debugInfo.IsStateMachineMoveNextMethod
                        ? DebugTypes.GetStateMachineThisVariableTypeIndex(method.OwningType)
                        : DebugTypes.GetThisTypeIndex(method.OwningType);
                }
                else
                {
                    variableTypeIndex = DebugTypes.GetVariableTypeIndex(debugVar.Type);
                }
                info.Type = IndexToDebugTypeHandle(variableTypeIndex);
            }
            Debug.Assert(i == variableCount);

            pInfo->VariableCount = (uint)variableCount;
            pInfo->Variables = (CORINFO_LLVM_VARIABLE_DEBUG_INFO*)GetPin(variables);
        }

        private static CORINFO_LLVM_DEBUG_TYPE_HANDLE IndexToDebugTypeHandle(uint index) => (CORINFO_LLVM_DEBUG_TYPE_HANDLE)index;
        private static uint DebugTypeHandleToIndex(CORINFO_LLVM_DEBUG_TYPE_HANDLE handle) => (uint)handle;

        private byte* ToPinnedUtf8String(string name) => (byte*)GetPin(StringToUTF8(name));

        public static IEnumerable<DebugVarInfoMetadata> GetDebugVarsForMethod<TLocal>(
            MethodDesc method, TLocal[] locals, Func<TLocal, TypeDesc> getLocalType, MethodDebugInformation debugInfo, out uint count)
        {
            bool isStateMachineMoveNextMethod = debugInfo.IsStateMachineMoveNextMethod;
            MethodSignature sig = method.Signature;
            int offset = sig.IsStatic ? 0 : 1;
            int parameterCount = offset + sig.Length;
            uint variableCount = 0;

            uint i = 0;
            string[] parameterNames = new string[parameterCount];
            foreach (string paramName in debugInfo.GetParameterNames())
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

            string[] localNames = new string[locals.Length];
            foreach (ILLocalVariable local in debugInfo.GetLocalVariables())
            {
                if (!local.CompilerGenerated && local.Slot < localNames.Length && localNames[local.Slot] != local.Name)
                {
                    localNames[local.Slot] = local.Name;
                    variableCount++;
                }
            }
            count = variableCount;

            IEnumerable<DebugVarInfoMetadata> GetResults()
            {
                for (int num = 0; num < parameterNames.Length + locals.Length; num++)
                {
                    string name;
                    TypeDesc type;
                    bool isParameter = num < parameterCount;
                    if (isParameter)
                    {
                        if (!sig.IsStatic && num == 0)
                        {
                            type = method.OwningType.IsValueType
                                ? method.OwningType.MakeByRefType()
                                : method.OwningType;
                        }
                        else
                        {
                            type = method.Signature[num - offset];
                        }

                        name = parameterNames[num];
                    }
                    else
                    {
                        // This is a local
                        int localNumber = num - parameterCount;
                        type = getLocalType(locals[localNumber]);
                        name = localNames[localNumber];
                    }

                    if (name != null)
                    {
                        yield return new DebugVarInfoMetadata(name, type, isParameter, new DebugVarInfo((uint)num, null));
                    }
                }
            }

            return GetResults();
        }

        public string GetDebugTypeName(TypeDesc type)
        {
            // The default mangler produces simple names for these classes which collide with the C++ runtime.
            if (type.IsObject)
            {
                return "System_Object";
            }
            if (type.IsString)
            {
                return "System_String";
            }
            return _compilation.NodeFactory.NameMangler.GetMangledTypeName(type);
        }
    }

    internal unsafe class LlvmTypesDebugInfoEmit : ITypesDebugInfoWriter
    {
        private readonly Utf8StringBuilder _utf8 = new();
        private readonly RyuJitCompilation _compilation;
        private readonly CorInfoImpl _module;
        private readonly UserDefinedTypeDescriptor _debugTypes;
        private uint _voidTypeIndex;

        public LlvmTypesDebugInfoEmit(RyuJitCompilation compilation, CorInfoImpl module)
        {
            _compilation = compilation;
            _module = module;
            _debugTypes = new UserDefinedTypeDescriptor(this, compilation.NodeFactory);
        }

        public void EmitTypeInfo(TypeDesc type)
        {
            // Should not be used for pointers/byrefs.
            Debug.Assert(type.IsDefType || type.IsArray);
            _debugTypes.GetTypeIndex(type, needsCompleteType: true);
        }

        public void EmitMethodInfo(MethodDesc method, INodeWithDebugInfo node)
        {
            foreach (DebugVarInfoMetadata debugVar in node.GetDebugVars())
            {
                try
                {
                    if (debugVar.DebugVarInfo.VarNumber == 0 && node.IsStateMachineMoveNextMethod)
                    {
                        // TODO-LLVM-DI: we're emitting a pointer here, which is wasteful...
                        // These types are per-method, we probably should emit them into each CU instead.
                        _debugTypes.GetStateMachineThisVariableTypeIndex(debugVar.Type);
                    }
                    else
                    {
                        EmitVariableTypeInfo(debugVar.Type);
                    }
                }
                catch (TypeSystemException) { }
            }

            // Emit the declaration itself.
            _debugTypes.GetMethodFunctionIdTypeIndex(method);
        }

        private void EmitVariableTypeInfo(TypeDesc type)
        {
            while (type.IsPointer || type.IsByRef)
            {
                type = ((ParameterizedType)type).ParameterType;
            }
            if (type.IsFunctionPointer)
            {
                // Currenly function pointers are not faithfully represented (they're emitted as void*).
                // Once/if that changes, we'll need to enumerate and emit all of the component types here.
                return;
            }

            EmitTypeInfo(type);
        }

        private byte* GetOffsetAndAppendName(string name)
        {
            int offset = _utf8.Length;
            AppendName(name);
            return (byte*)offset;
        }

        private ReadOnlySpan<byte> AppendName(string name) => _utf8.Append(name).Append('\0').AsSpan();

        private static CORINFO_LLVM_DEBUG_TYPE_HANDLE IndexToDebugTypeHandle(uint index) => (CORINFO_LLVM_DEBUG_TYPE_HANDLE)index;
        private static uint DebugTypeHandleToIndex(CORINFO_LLVM_DEBUG_TYPE_HANDLE handle) => (uint)handle;

        uint ITypesDebugInfoWriter.GetClassTypeIndex(ClassTypeDescriptor classType)
        {
            _utf8.Clear();
            CORINFO_LLVM_TYPE_DEBUG_INFO info;
            info.Kind = CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD;
            fixed (byte* pName = AppendName(classType.Name))
            {
                info.Union.ForwardInfo.Name = pName;
                info.Union.ForwardInfo.Kind = CorInfoLlvmDebugTypeForwardKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD_STRUCT;
                return DebugTypeHandleToIndex(_module.JitEmitDebugTypeInfo(&info));
            }
        }

        uint ITypesDebugInfoWriter.GetCompleteClassTypeIndex(ClassTypeDescriptor classType, ClassFieldsTypeDescriptor classFieldTypes, DataFieldDescriptor[] fields, StaticDataFieldDescriptor[] statics)
        {
            _utf8.Clear();
            CORINFO_LLVM_TYPE_DEBUG_INFO info;
            info.Kind = CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_COMPOSITE;

            info.Union.CompositeInfo.Name = GetOffsetAndAppendName(classType.Name);
            info.Union.CompositeInfo.BaseClass = IndexToDebugTypeHandle(classType.BaseClassId);
            info.Union.CompositeInfo.Size = checked((uint)classType.InstanceSize);

            int instanceFieldCount = fields.Length - statics.Length;
            var instanceFieldsInfo = instanceFieldCount != 0 ? new CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO[instanceFieldCount] : null;
            int staticFieldCount = statics.Length;
            var staticFieldsInfo = staticFieldCount != 0 ? new CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO[staticFieldCount] : null;

            string lastBaseSymbolName = null;
            byte* lastBaseSymbolNameOffset = null;
            for (int i = 0, s = 0, j = 0; j < fields.Length; j++)
            {
                DataFieldDescriptor field = fields[j];
                if (field.Offset == 0xFFFFFFFF) // Static field.
                {
                    StaticDataFieldDescriptor staticField = statics[s];
                    ref CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO fieldInfo = ref staticFieldsInfo[s++];
                    fieldInfo.Name = GetOffsetAndAppendName(field.Name);
                    fieldInfo.Type = IndexToDebugTypeHandle(field.FieldTypeIndex);
                    if (lastBaseSymbolName == staticField.StaticDataName)
                    {
                        fieldInfo.BaseSymbolName = lastBaseSymbolNameOffset;
                    }
                    else
                    {
                        fieldInfo.BaseSymbolName = GetOffsetAndAppendName(staticField.StaticDataName);
                        lastBaseSymbolName = staticField.StaticDataName;
                        lastBaseSymbolNameOffset = fieldInfo.BaseSymbolName;
                    }
                    fieldInfo.StaticOffset = checked((uint)staticField.StaticOffset);
                    fieldInfo.IsStaticDataInObject = staticField.IsStaticDataInObject;
                }
                else
                {
                    ref CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO fieldInfo = ref instanceFieldsInfo[i++];
                    fieldInfo.Name = GetOffsetAndAppendName(field.Name);
                    fieldInfo.Type = IndexToDebugTypeHandle(field.FieldTypeIndex);
                    fieldInfo.Offset = checked((uint)field.Offset);
                }
            }

            fixed (byte* pNameBase = _utf8.UnderlyingArray)
            fixed (CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO* pInstanceFieldsInfo = instanceFieldsInfo)
            fixed (CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO* pStaticFieldsInfo = staticFieldsInfo)
            {
                info.Union.CompositeInfo.Name += (nint)pNameBase;
                foreach (ref CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO fieldInfo in instanceFieldsInfo.AsSpan())
                {
                    fieldInfo.Name += (nint)pNameBase;
                }
                foreach (ref CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO fieldInfo in staticFieldsInfo.AsSpan())
                {
                    fieldInfo.Name += (nint)pNameBase;
                    fieldInfo.BaseSymbolName += (nint)pNameBase;
                }

                info.Union.CompositeInfo.InstanceFieldCount = (uint)instanceFieldCount;
                info.Union.CompositeInfo.InstanceFields = pInstanceFieldsInfo;
                info.Union.CompositeInfo.StaticFieldCount = (uint)staticFieldCount;
                info.Union.CompositeInfo.StaticFields = pStaticFieldsInfo;
                return DebugTypeHandleToIndex(_module.JitEmitDebugTypeInfo(&info));
            }
        }

        uint ITypesDebugInfoWriter.GetArrayTypeIndex(ClassTypeDescriptor classType, ArrayTypeDescriptor arrayType)
        {
            _utf8.Clear();
            CORINFO_LLVM_TYPE_DEBUG_INFO info;
            info.Kind = CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_ARRAY;
            fixed (byte* pName = AppendName(classType.Name))
            {
                info.Union.ArrayInfo.Name = pName;
                info.Union.ArrayInfo.Rank = arrayType.Rank;
                info.Union.ArrayInfo.ElementType = IndexToDebugTypeHandle(arrayType.ElementType);
                info.Union.ArrayInfo.IsMultiDimensional = arrayType.IsMultiDimensional;
                return DebugTypeHandleToIndex(_module.JitEmitDebugTypeInfo(&info));
            }
        }

        uint ITypesDebugInfoWriter.GetEnumTypeIndex(EnumTypeDescriptor enumType, EnumRecordTypeDescriptor[] elements)
        {
            _utf8.Clear();
            int pNameOffset = (int)GetOffsetAndAppendName(enumType.Name);
            CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO[] elementsInfo = new CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO[elements.Length];
            for (int i = 0; i < elementsInfo.Length; i++)
            {
                EnumRecordTypeDescriptor element = elements[i];
                ref CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO elementInfo = ref elementsInfo[i];
                elementInfo.Name = GetOffsetAndAppendName(element.Name);
                elementInfo.Value = element.Value;
            }

            fixed (byte* pNameBase = _utf8.UnderlyingArray)
            fixed (CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO* pElements = elementsInfo)
            {
                foreach (ref CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO elementInfo in elementsInfo.AsSpan())
                {
                    elementInfo.Name += (nint)pNameBase;
                }

                CORINFO_LLVM_TYPE_DEBUG_INFO info;
                info.Kind = CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_ENUM;
                info.Union.EnumInfo.Name = pNameBase + pNameOffset;
                info.Union.EnumInfo.ElementType = IndexToDebugTypeHandle(enumType.ElementType);
                info.Union.EnumInfo.ElementCount = enumType.ElementCount;
                info.Union.EnumInfo.Elements = pElements;
                return DebugTypeHandleToIndex(_module.JitEmitDebugTypeInfo(&info));
            }
        }

        uint ITypesDebugInfoWriter.GetMemberFunctionTypeIndex(MemberFunctionTypeDescriptor methodType, uint[] argumentTypes)
        {
            CORINFO_LLVM_TYPE_DEBUG_INFO info;
            info.Kind = CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_FUNCTION;
            if (methodType.TypeIndexOfThisPointer == _voidTypeIndex)
            {
                info.Union.FunctionInfo.TypeOfThisPointer = 0;
            }
            else
            {
                info.Union.FunctionInfo.TypeOfThisPointer = IndexToDebugTypeHandle(methodType.TypeIndexOfThisPointer);
            }

            fixed (uint* pArgumentTypes = argumentTypes)
            {
                info.Union.FunctionInfo.ReturnType = IndexToDebugTypeHandle(methodType.ReturnType);
                info.Union.FunctionInfo.NumberOfArguments = methodType.NumberOfArguments;
                info.Union.FunctionInfo.ArgumentTypes = (CORINFO_LLVM_DEBUG_TYPE_HANDLE*)pArgumentTypes;
                return DebugTypeHandleToIndex(_module.JitEmitDebugTypeInfo(&info));
            }
        }

        uint ITypesDebugInfoWriter.GetPointerTypeIndex(PointerTypeDescriptor pointerType)
        {
            CORINFO_LLVM_TYPE_DEBUG_INFO info;
            info.Kind = CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_POINTER;
            info.Union.PointerInfo.ElementType = IndexToDebugTypeHandle(pointerType.ElementType);
            info.Union.PointerInfo.IsReference = pointerType.IsReference;
            return DebugTypeHandleToIndex(_module.JitEmitDebugTypeInfo(&info));
        }

        uint ITypesDebugInfoWriter.GetPrimitiveTypeIndex(TypeDesc type)
        {
            CORINFO_LLVM_TYPE_DEBUG_INFO info;
            info.Kind = CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_PRIMITIVE;
            info.Union.PrimitiveType = (CorInfoType)type.Category;
            uint index = DebugTypeHandleToIndex(_module.JitEmitDebugTypeInfo(&info));
            if (type.IsVoid)
            {
                _voidTypeIndex = index;
            }
            return index;
        }

        uint ITypesDebugInfoWriter.GetMemberFunctionId(MemberFunctionIdTypeDescriptor method)
        {
            _utf8.Clear();
            CORINFO_LLVM_METHOD_DECL_DEBUG_INFO info;
            ReadOnlySpan<byte> linkageName = _compilation.NameMangler.GetMangledMethodName(method.Method).AsSpan();
            fixed (byte* pName = AppendName(method.Name))
            fixed (byte* pLinkageName = linkageName)
            {
                info.Name = pName;
                info.LinkageName.Data = pLinkageName;
                info.LinkageName.Length = (nuint)linkageName.Length;
                info.Type = IndexToDebugTypeHandle(method.MemberFunction);
                info.OwnerType = IndexToDebugTypeHandle(method.ParentClass);
                return (uint)_module.JitEmitDebugMethodDecl(&info);
            }
        }

        string ITypesDebugInfoWriter.GetMangledName(TypeDesc type) => _module.GetDebugTypeName(type);
    }

    internal enum CORINFO_LLVM_DEBUG_TYPE_HANDLE { }
    internal enum CORINFO_LLVM_DEBUG_METHOD_DECL_HANDLE { }

    internal unsafe struct CORINFO_LLVM_STRING
    {
        public byte* Data;
        public nuint Length;
    }

    internal unsafe struct CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO
    {
        public byte* Name;
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
        public uint Offset;
    }

    internal unsafe struct CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO
    {
        public byte* Name;
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
        public byte* BaseSymbolName;
        public uint StaticOffset;
        public int IsStaticDataInObject;
    }

    internal unsafe struct CORINFO_LLVM_COMPOSITE_TYPE_DEBUG_INFO
    {
        public byte* Name;
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE BaseClass;
        public uint Size;

        public uint InstanceFieldCount;
        public CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO* InstanceFields;

        public uint StaticFieldCount;
        public CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO* StaticFields;
    }

    internal unsafe struct CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO
    {
        public byte* Name;
        public ulong Value;
    }

    internal unsafe struct CORINFO_LLVM_ENUM_TYPE_DEBUG_INFO
    {
        public byte* Name;
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE ElementType;
        public ulong ElementCount;
        public CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO* Elements;
    }

    internal unsafe struct CORINFO_LLVM_ARRAY_TYPE_DEBUG_INFO
    {
        public byte* Name;
        public uint Rank;
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE ElementType;
        public int IsMultiDimensional;
    }

    internal struct CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO
    {
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE ElementType;
        public int IsReference;
    }

    internal unsafe struct CORINFO_LLVM_FORWARD_TYPE_DEBUG_INFO
    {
        public byte* Name;
        public CorInfoLlvmDebugTypeForwardKind Kind;
    }

    internal unsafe struct CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO
    {
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE TypeOfThisPointer;
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE ReturnType;
        public uint NumberOfArguments;
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE* ArgumentTypes;
    }

    internal struct CORINFO_LLVM_TYPE_DEBUG_INFO
    {
        public CorInfoLlvmDebugTypeKind Kind;
#pragma warning disable CS0649
        public CORINFO_LLVM_TYPE_DEBUG_INFO_UNION Union;
#pragma warning restore CS0649
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct CORINFO_LLVM_TYPE_DEBUG_INFO_UNION
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
        public CORINFO_LLVM_FORWARD_TYPE_DEBUG_INFO ForwardInfo;
        [FieldOffset(0)]
        public CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO FunctionInfo;
    }

    internal unsafe struct CORINFO_LLVM_VARIABLE_DEBUG_INFO
    {
        public byte* Name;
        public uint VarNumber;
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
    }

    internal struct CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO
    {
        public uint ILOffset;
        public uint LineNumber;
    }

    internal unsafe struct CORINFO_LLVM_METHOD_DECL_DEBUG_INFO
    {
        public byte* Name;
        public CORINFO_LLVM_STRING LinkageName;
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
        public CORINFO_LLVM_DEBUG_TYPE_HANDLE OwnerType;
    }

    internal unsafe struct CORINFO_LLVM_METHOD_DEBUG_INFO
    {
        public CORINFO_LLVM_METHOD_DECL_DEBUG_INFO Decl;
        public byte* Directory;
        public byte* FileName;
        public uint LineNumberCount;
        public CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO* SortedLineNumbers;
        public uint VariableCount;
        public CORINFO_LLVM_VARIABLE_DEBUG_INFO* Variables;
    }
}
