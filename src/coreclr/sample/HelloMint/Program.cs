// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

        private delegate int MeaningOfLife();
        static void CreateDynamicMethod()
        {
            DynamicMethod dMethod = new DynamicMethod("MeaningOfLife", typeof(int), Type.EmptyTypes, typeof(object).Module);
            if (dMethod is not null)
            {
                Console.WriteLine ($"DynamicMethod: '{dMethod.Name}' with return type '{dMethod.ReturnType}' has been created");

                ILGenerator il = dMethod.GetILGenerator(256);
                if (il is null)
                    throw new Exception("ILGenerator is null");

                il.Emit(OpCodes.Ldc_I4, 42);
                il.Emit(OpCodes.Ret);

                MeaningOfLife answer = (MeaningOfLife) dMethod.CreateDelegate(typeof(MeaningOfLife));
                if (answer is null)
                    throw new Exception("Delegate for the dynamic method is null");

                var retVal = answer();
                Console.WriteLine($"The answer is: {retVal}");
            }
            else
            {
                Console.WriteLine ($"Failed to create a DynamicMethod");
            }
        }
    }
}
