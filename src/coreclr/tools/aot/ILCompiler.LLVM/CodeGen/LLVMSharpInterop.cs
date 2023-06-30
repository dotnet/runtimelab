// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LLVMSharp.Interop;

namespace Internal.IL
{
    internal static unsafe class LLVMSharpInterop
    {
        internal static LLVMAttributeRef CreateAttribute(LLVMContextRef context, string name, string value)
        {
            using var marshaledName = new MarshaledString(name);
            using var marshaledValue = new MarshaledString(value);

            return LLVM.CreateStringAttribute(context, marshaledName.Value, (uint)marshaledName.Length,
                marshaledValue.Value, (uint)marshaledValue.Length);
        }

        internal static void AddFunctionAttribute(this LLVMValueRef function, string name, string value)
        {
            LLVMAttributeRef attribute = CreateAttribute(function.TypeOf.Context, name, value);
            LLVM.AddAttributeAtIndex(function, LLVMAttributeIndex.LLVMAttributeFunctionIndex, attribute);
        }

        internal static LLVMValueRef GetNamedAlias(this LLVMModuleRef module, ReadOnlySpan<byte> name)
        {
            fixed (byte* pName = name)
            {
                Debug.Assert(pName[name.Length] == '\0');
                return LLVM.GetNamedGlobalAlias(module, (sbyte*)pName, (nuint)name.Length);
            }
        }

        internal static LLVMValueRef GetNamedFunction(this LLVMModuleRef module, ReadOnlySpan<byte> name)
        {
            fixed (byte* pName = name)
            {
                Debug.Assert(pName[name.Length] == '\0');
                return LLVM.GetNamedFunction(module, (sbyte*)pName);
            }
        }

        internal static LLVMValueRef GetNamedGlobal(this LLVMModuleRef module, ReadOnlySpan<byte> name)
        {
            fixed (byte* pName = name)
            {
                Debug.Assert(pName[name.Length] == '\0');
                return LLVM.GetNamedGlobal(module, (sbyte*)pName);
            }
        }

        internal static LLVMValueRef AddAlias(this LLVMModuleRef module, ReadOnlySpan<byte> name, LLVMTypeRef valueType, LLVMValueRef aliasee)
        {
            fixed (byte* pName = name)
            {
                Debug.Assert(pName[name.Length] == '\0');
                return LLVM.AddAlias2(module, valueType, 0, aliasee, (sbyte*)pName);
            }
        }

        internal static LLVMValueRef AddFunction(this LLVMModuleRef module, ReadOnlySpan<byte> name, LLVMTypeRef type)
        {
            fixed (byte* pName = name)
            {
                Debug.Assert(pName[name.Length] == '\0');
                return LLVM.AddFunction(module, (sbyte*)pName, type);
            }
        }

        internal static LLVMValueRef AddGlobal(this LLVMModuleRef module, ReadOnlySpan<byte> name, LLVMTypeRef type)
        {
            fixed (byte* pName = name)
            {
                Debug.Assert(pName[name.Length] == '\0');
                return LLVM.AddGlobal(module, type, (sbyte*)pName);
            }
        }

        internal static LLVMTypeRef GetValueType(this LLVMValueRef value)
        {
            Debug.Assert(value.IsAGlobalValue.Handle != IntPtr.Zero);
            return LLVM.GlobalGetValueType(value);
        }

        internal struct MarshaledString : IDisposable
        {
            public MarshaledString(ReadOnlySpan<char> input)
            {
                if (input.IsEmpty)
                {
                    var value = Marshal.AllocHGlobal(1);
                    Marshal.WriteByte(value, 0, 0);

                    Length = 0;
                    Value = (sbyte*)value;
                }
                else
                {
                    var valueBytes = Encoding.UTF8.GetBytes(input.ToString());
                    var length = valueBytes.Length;
                    var value = Marshal.AllocHGlobal(length + 1);
                    Marshal.Copy(valueBytes, 0, value, length);
                    Marshal.WriteByte(value, length, 0);

                    Length = length;
                    Value = (sbyte*)value;
                }
            }

            public int Length { get; private set; }

            public sbyte* Value { get; private set; }

            public void Dispose()
            {
                if (Value != null)
                {
                    Marshal.FreeHGlobal((IntPtr)Value);
                    Value = null;
                    Length = 0;
                }
            }

            public static implicit operator sbyte*(in MarshaledString value)
            {
                return value.Value;
            }

            public override string ToString()
            {
                var span = new ReadOnlySpan<byte>(Value, Length);
                return AsString(span);
            }

            public static string AsString(ReadOnlySpan<byte> self)
            {
                if (self.IsEmpty)
                {
                    return string.Empty;
                }

                fixed (byte* pSelf = self)
                {
                    return Encoding.UTF8.GetString(pSelf, self.Length);
                }
            }
        }
    }
}
