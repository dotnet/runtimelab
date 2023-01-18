using System;
using System.Runtime.InteropServices;
using System.Text;
using LLVMSharp.Interop;

namespace Internal.IL
{
    //TODO-LLVM: delete this file when IL->LLVM module has gone
    internal static unsafe class LLVMSharpInterop
    {
        ///
        /// Wrapper while waiting for https://github.com/microsoft/LLVMSharp/pull/144
        /// 
        internal static void DISetSubProgram(LLVMValueRef function, LLVMMetadataRef diFunction)
        {
            LLVM.SetSubprogram(function, diFunction);
        }

        internal static LLVMAttributeRef CreateAttribute(LLVMContextRef context, string name, string value)
        {
            ReadOnlySpan<char> nameSpan = name.AsSpan();
            ReadOnlySpan<char> valueSpan = value.AsSpan();

            using var marshaledName = new MarshaledString(name);
            using var marshaledValue = new MarshaledString(value);

            return LLVM.CreateStringAttribute(context, marshaledName.Value, (uint)marshaledName.Length,
                marshaledValue.Value, (uint)marshaledValue.Length);
        }

        public static LLVMValueRef GetNamedAlias(this LLVMModuleRef module, ReadOnlySpan<char> name)
        {
            using var marshaledName = new MarshaledString(name);
            return LLVM.GetNamedGlobalAlias(module, marshaledName, (nuint)marshaledName.Length);
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
