// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace HelloMint
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (AppContext.TryGetSwitch("System.Private.Mint.Enable", out var enabled))
            {
                Console.WriteLine ("Hello, Mint is {0}", enabled ? "enabled": "disabled");
                try
                {
                    CreateDynamicMethod();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed with: {ex}");
                }
            }
            else
            {
                Console.WriteLine ($"Hello, System.Private.Mint.Enable is unset");
            }
        }

        private static bool voidVoidSample = true;

        private static void GenerateSample(ILGenerator ilgen)
        {
            if (voidVoidSample)
            {
                ilgen.Emit(OpCodes.Ldc_I4_S, (byte)42);
                ilgen.Emit(OpCodes.Pop);
                ilgen.Emit(OpCodes.Ret);

            }
            else
            {
                ilgen.Emit(OpCodes.Ldc_I4_S, (byte)42);
                ilgen.Emit(OpCodes.Ret);
            }
        }


        private delegate int MeaningOfLife();
        static void CreateDynamicMethod()
        {
            var returnType = voidVoidSample ? typeof(void) : typeof(int);
            DynamicMethod dMethod = new DynamicMethod("MeaningOfLife", returnType, Type.EmptyTypes, typeof(object).Module);
            if (dMethod is not null)
            {
                Console.WriteLine ($"DynamicMethod: '{dMethod.Name}' with return type '{dMethod.ReturnType}' has been created");

                ILGenerator ilgen = dMethod.GetILGenerator();
                if (ilgen is null)
                    throw new Exception("ILGenerator is null");

                GenerateSample(ilgen);
                DumpILBytes(ilgen);

                RunSample(dMethod);
            }
            else
            {
                Console.WriteLine($"Failed to create a DynamicMethod");
            }
        }

        private static void RunSample(DynamicMethod dMethod)
        {
            if (voidVoidSample)
            {
                Action answer = (Action)dMethod.CreateDelegate(typeof(Action));
                if (answer is null)
                    throw new Exception("Delegate for the dynamic method is null");

                answer();
                Console.WriteLine("delegate returned");
            }
            else
            {
                MeaningOfLife answer = (MeaningOfLife)dMethod.CreateDelegate(typeof(MeaningOfLife));
                if (answer is null)
                    throw new Exception("Delegate for the dynamic method is null");

                var retVal = answer();
                Console.WriteLine($"The answer is: {retVal}");
            }
        }

        // Requires rooting DynamicILGenerator
        static void DumpILBytes(ILGenerator ilgen)
        {
            var ilBufferAccessor = ilgen.GetType().GetField("m_ILStream", BindingFlags.Instance | BindingFlags.NonPublic);
            var ilBufferLengthAccessor = ilgen.GetType().GetField("m_length", BindingFlags.Instance | BindingFlags.NonPublic);
            byte[] ilBuffer = ilBufferAccessor.GetValue(ilgen) as byte[];
            int ilBufferLength = (int)ilBufferLengthAccessor.GetValue(ilgen);

            Console.WriteLine("--------------------------");
            Console.WriteLine("ILBuffer contents: ");
            int i = 0;
            while (i < ilBufferLength)
            {
                if (i > 0 && i % 4 == 0)
                    Console.WriteLine();
                Console.Write(String.Format(" 0x{0:X}", ilBuffer[i]));
                i ++;
            }
            if (i % 4 != 1)
                Console.WriteLine();
            Console.WriteLine("--------------------------");
        }
    }
}
