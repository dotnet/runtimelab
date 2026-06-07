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
    // in the compilation phase. Fixing this implies changing upstream code, however.
    // TODO-LLVM-Upstream: augment UDTD to allow only requesting what we need.
    //
    internal sealed unsafe partial class CorInfoImpl
    {
        private LlvmCodeDebugInfoEmit _debugTypes;
        private LlvmCodeDebugInfoEmit DebugTypes => _debugTypes ??= new(_compilation, this);

        private void GetDebugInfoForCurrentMethod(CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo)
        {
            *pInfo = default;
            if (_debugInfo == null)
            {
                // The Jit may call us more than once (e. g. due to internal fallbacks).
                return;
            }

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

            uint declIndex = DebugTypes.GetMethodDeclIndex(method);
            pInfo->Decl = LlvmDebugInfoEmit.IndexToDebugMethodDeclHandle(declIndex);
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
                info.Type = LlvmDebugInfoEmit.IndexToDebugTypeHandle(variableTypeIndex);
            }
            Debug.Assert(i == variableCount);

            pInfo->VariableCount = (uint)variableCount;
            pInfo->Variables = (CORINFO_LLVM_VARIABLE_DEBUG_INFO*)GetPin(variables);
        }

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
    }

    internal abstract unsafe class LlvmDebugInfoEmit : ITypesDebugInfoWriter
    {
        protected readonly UserDefinedTypeDescriptor _debugTypes;
        protected readonly Utf8StringBuilder _utf8 = new();
        protected readonly CorInfoImpl _module;
        private readonly RyuJitCompilation _compilation;
        private uint _voidTypeIndex;

        public LlvmDebugInfoEmit(RyuJitCompilation compilation, CorInfoImpl module)
        {
            _compilation = compilation;
            _module = module;
            _debugTypes = new UserDefinedTypeDescriptor(this, compilation.NodeFactory);
        }

        public uint GetClassTypeIndex(ClassTypeDescriptor classType)
        {
            return GetTypeForwardIndex(classType.Name, CorInfoLlvmDebugTypeForwardKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD_STRUCT);
        }

        public abstract uint GetCompleteClassTypeIndex(ClassTypeDescriptor classType, ClassFieldsTypeDescriptor classFieldTypes, DataFieldDescriptor[] fields, StaticDataFieldDescriptor[] statics);

        public abstract uint GetArrayTypeIndex(ClassTypeDescriptor classType, ArrayTypeDescriptor arrayType);

        public abstract uint GetEnumTypeIndex(EnumTypeDescriptor enumType, EnumRecordTypeDescriptor[] elements);

        public uint GetMemberFunctionTypeIndex(MemberFunctionTypeDescriptor methodType, uint[] argumentTypes)
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

        public uint GetPointerTypeIndex(PointerTypeDescriptor pointerType)
        {
            CORINFO_LLVM_TYPE_DEBUG_INFO info;
            info.Kind = CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_POINTER;
            info.Union.PointerInfo.ElementType = IndexToDebugTypeHandle(pointerType.ElementType);
            info.Union.PointerInfo.IsReference = pointerType.IsReference;
            return DebugTypeHandleToIndex(_module.JitEmitDebugTypeInfo(&info));
        }

        public uint GetPrimitiveTypeIndex(TypeDesc type)
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

        public uint GetMemberFunctionId(MemberFunctionIdTypeDescriptor method)
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
                return DebugMethodDeclHandleToIndex(_module.JitEmitDebugMethodDecl(&info));
            }
        }

        public Utf8String GetMangledName(TypeDesc type)
        {
            // The default mangler produces simple names for these classes which collide with the C++ runtime.
            if (type.IsObject)
            {
                return new Utf8String("System_Object");
            }
            if (type.IsString)
            {
                return new Utf8String("System_String");
            }
            return _compilation.NodeFactory.NameMangler.GetMangledTypeName(type);
        }

        public static CORINFO_LLVM_DEBUG_TYPE_HANDLE IndexToDebugTypeHandle(uint index) => (CORINFO_LLVM_DEBUG_TYPE_HANDLE)index;
        public static CORINFO_LLVM_DEBUG_METHOD_DECL_HANDLE IndexToDebugMethodDeclHandle(uint index) => (CORINFO_LLVM_DEBUG_METHOD_DECL_HANDLE)index;

        public static uint DebugTypeHandleToIndex(CORINFO_LLVM_DEBUG_TYPE_HANDLE handle) => (uint)handle;
        public static uint DebugMethodDeclHandleToIndex(CORINFO_LLVM_DEBUG_METHOD_DECL_HANDLE handle) => (uint)handle;

        protected uint GetTypeForwardIndex(Utf8String name, CorInfoLlvmDebugTypeForwardKind kind)
        {
            _utf8.Clear();
            CORINFO_LLVM_TYPE_DEBUG_INFO info;
            info.Kind = CorInfoLlvmDebugTypeKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD;
            fixed (byte* pName = AppendName(name))
            {
                info.Union.ForwardInfo.Name = pName;
                info.Union.ForwardInfo.Kind = kind;
                return DebugTypeHandleToIndex(_module.JitEmitDebugTypeInfo(&info));
            }
        }

        protected byte* GetOffsetAndAppendName(Utf8String name)
        {
            int offset = _utf8.Length;
            AppendName(name);
            return (byte*)offset;
        }

        protected ReadOnlySpan<byte> AppendName(Utf8String name) => _utf8.Append(name).Append('\0').AsSpan();
    }

    internal sealed class LlvmCodeDebugInfoEmit(RyuJitCompilation compilation, CorInfoImpl module) : LlvmDebugInfoEmit(compilation, module)
    {
        public uint GetVariableTypeIndex(TypeDesc type) => _debugTypes.GetVariableTypeIndex(type);

        public uint GetStateMachineThisVariableTypeIndex(TypeDesc owningType) => _debugTypes.GetStateMachineThisVariableTypeIndex(owningType);

        public uint GetThisTypeIndex(TypeDesc owningType) => _debugTypes.GetThisTypeIndex(owningType);

        public uint GetMethodDeclIndex(MethodDesc method) => _debugTypes.GetMethodFunctionIdTypeIndex(method);

        public override uint GetArrayTypeIndex(ClassTypeDescriptor classType, ArrayTypeDescriptor arrayType)
        {
            return GetTypeForwardIndex(classType.Name, CorInfoLlvmDebugTypeForwardKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD_STRUCT);
        }

        public override uint GetCompleteClassTypeIndex(ClassTypeDescriptor classType, ClassFieldsTypeDescriptor classFieldTypes, DataFieldDescriptor[] fields, StaticDataFieldDescriptor[] statics)
        {
            return GetTypeForwardIndex(classType.Name, CorInfoLlvmDebugTypeForwardKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD_STRUCT);
        }

        public override uint GetEnumTypeIndex(EnumTypeDescriptor enumType, EnumRecordTypeDescriptor[] elements)
        {
            return GetTypeForwardIndex(enumType.Name, CorInfoLlvmDebugTypeForwardKind.CORINFO_LLVM_DEBUG_TYPE_FORWARD_ENUM);
        }
    }

    internal sealed unsafe class LlvmTypesDebugInfoEmit(RyuJitCompilation compilation, CorInfoImpl module) : LlvmDebugInfoEmit(compilation, module)
    {
        public void EmitTypeInfo(TypeDesc type)
        {
            // Should not be used for pointers/byrefs.
            Debug.Assert(type.IsDefType || type.IsArray);
            _debugTypes.GetTypeIndex(type, needsCompleteType: true);
        }

        public void EmitMethodInfo(MethodDesc method, INodeWithDebugInfo node)
        {
            void EmitVariableTypeInfo(TypeDesc type)
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

        public override uint GetCompleteClassTypeIndex(ClassTypeDescriptor classType, ClassFieldsTypeDescriptor classFieldTypes, DataFieldDescriptor[] fields, StaticDataFieldDescriptor[] statics)
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

            Utf8String lastBaseSymbolName = new Utf8String((byte [])null);
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
                    if (!lastBaseSymbolName.IsNull && lastBaseSymbolName.Equals(staticField.StaticDataName))
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

        public override uint GetArrayTypeIndex(ClassTypeDescriptor classType, ArrayTypeDescriptor arrayType)
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

        public override uint GetEnumTypeIndex(EnumTypeDescriptor enumType, EnumRecordTypeDescriptor[] elements)
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
        public CORINFO_LLVM_DEBUG_METHOD_DECL_HANDLE Decl;
        public byte* Directory;
        public byte* FileName;
        public uint LineNumberCount;
        public CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO* SortedLineNumbers;
        public uint VariableCount;
        public CORINFO_LLVM_VARIABLE_DEBUG_INFO* Variables;
    }
}
