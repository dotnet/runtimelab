// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Buffers.Text;
using System.Runtime.InteropServices;

namespace NativeLibrary
{
    public unsafe class NativeLibrary
    {
        [UnmanagedCallersOnly(EntryPoint = "NativeLibrary_Free")]
        public static void Free(void* p)
        {
            NativeMemory.Free(p);
        }

        [UnmanagedCallersOnly(EntryPoint = "NativeLibrary_ComputeArithmeticExpression")]
        public static double ComputeArithmeticExpression(byte* inputAsUtf8)
        {
            // The marshalling code is typically auto-generated by a custom tool in larger projects.
            double output;
            try
            {
                // UnmanagedCallersOnly methods only accept primitive arguments. The primitive arguments
                // have to be marshalled manually if necessary.
                ReadOnlySpan<byte> input = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(inputAsUtf8);
                output = ComputeArithmeticExpressionImpl(input);
            }
            catch
            {
                // Exceptions escaping out of UnmanagedCallersOnly methods are treated as unhandled exceptions.
                // The errors have to be marshalled manually if necessary.
                output = double.NaN;
            }

            return output;
        }

        private static double ComputeArithmeticExpressionImpl(ReadOnlySpan<byte> input)
        {
            double result = 0;
            byte lastOp = (byte)'+';
            while (true)
            {
                if (!Utf8Parser.TryParse(input, out double currentValue, out int valueLength))
                {
                    throw new Exception("Invalid number in the input!");
                }

                if (lastOp == '+')
                {
                    result += currentValue;
                }
                else
                {
                    result -= currentValue;
                }

                if (valueLength == input.Length)
                {
                    break;
                }

                lastOp = input[valueLength];
                if (lastOp != '+' && lastOp != '-')
                {
                    throw new Exception("Invalid operator in the input!");
                }

                input = input[(valueLength + 1)..];
            }

            return result;
        }
    }
}
