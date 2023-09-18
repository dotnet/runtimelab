// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace HelloMint
{
    internal class Program
    {
        enum Sample
        {
            VoidVoid,
            IntReturn,
            IntParamIntReturn,
            IntDoubleParamsIntReturn,
        }
        private static void Main(string[] args)
        {
            if (AppContext.TryGetSwitch("System.Private.Mint.Enable", out var enabled))
            {
                Console.WriteLine ("Hello, Mint is {0}", enabled ? "enabled": "disabled");
                try
                {
                    if (args.Length > 0)
                        sample = Enum.Parse<Sample>(args[0]);
                    else
                        sample = Sample.VoidVoid;
                    Console.WriteLine($"Running sample: {sample}");
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

        private static Sample sample;

        private static void GenerateSample(ILGenerator ilgen)
        {
            switch (sample)
            {
                case Sample.VoidVoid:
                    GenerateVoidVoidSample(ilgen);
                    break;
                case Sample.IntReturn:
                    GenerateIntReturnSample(ilgen);
                    break;
                case Sample.IntParamIntReturn:
                    GenerateIntParamIntReturnSample(ilgen);
                    break;
                case Sample.IntDoubleParamsIntReturn:
                    GenerateIntDoubleParamsIntReturnSample(ilgen);
                    break;
                default:
                    throw new Exception($"Unknown sample: {sample}");
            }
        }

        private static void GenerateVoidVoidSample(ILGenerator ilgen)
        {
            ilgen.Emit(OpCodes.Ldc_I4_S, (byte)23);
            ilgen.Emit(OpCodes.Pop);
            ilgen.Emit(OpCodes.Ret);
        }

        private static void GenerateIntReturnSample(ILGenerator ilgen)
        {
            ilgen.Emit(OpCodes.Ldc_I4_S, (byte)40);
            ilgen.Emit(OpCodes.Ldc_I4_S, (byte)2);
            ilgen.Emit(OpCodes.Add);
            ilgen.Emit(OpCodes.Ret);
        }

        private static void GenerateIntParamIntReturnSample(ILGenerator ilgen)
        {
            ilgen.Emit(OpCodes.Ldarg_0);
            ilgen.Emit(OpCodes.Ldc_I4_S, (byte)2);
            ilgen.Emit(OpCodes.Add);
            ilgen.Emit(OpCodes.Ret);
        }

        private static void GenerateIntDoubleParamsIntReturnSample(ILGenerator ilgen)
        {
            ilgen.Emit(OpCodes.Ldarg_0);
            ilgen.Emit(OpCodes.Ldarg_1);
            ilgen.Emit(OpCodes.Conv_I4);
            ilgen.Emit(OpCodes.Add);
            // this is redundant, but it will exercise the code path;
            // and the Mint optimizer should eliminate all this code
            ilgen.Emit(OpCodes.Starg_S, (byte)0);
            ilgen.Emit(OpCodes.Ldarg_0);
            ilgen.Emit(OpCodes.Ret);
        }

        static void CreateDynamicMethod()
        {
            var returnType = sample switch
            {
                Sample.VoidVoid => typeof(void),
                Sample.IntReturn
                or Sample.IntParamIntReturn
                or Sample.IntDoubleParamsIntReturn => typeof(int),
                _ => throw new Exception($"Unknown sample: {sample}")
            };
            var paramTypes = sample switch
            {
                Sample.VoidVoid or Sample.IntReturn => Type.EmptyTypes,
                Sample.IntParamIntReturn => new Type[] { typeof(int) },
                Sample.IntDoubleParamsIntReturn => new Type[] { typeof(int), typeof(double) },
                _ => throw new Exception($"Unknown sample: {sample}")
            };
            DynamicMethod dMethod = new DynamicMethod("MeaningOfLife", returnType, paramTypes, typeof(object).Module);
            if (dMethod is not null)
            {
                var mName = dMethod.Name;
                var mReturnType = dMethod.ReturnType;
                var mParams = dMethod.GetParameters();

                Console.WriteLine($"DynamicMethod: '{dMethod.Name}'");
                Console.WriteLine($"Return type: '{dMethod.ReturnType}'");
                Console.WriteLine($"Has {mParams.Length} params:");
                int paramCnt = 0;
                foreach (var param in mParams)
                    Console.WriteLine($"\tparam[{paramCnt++}] type: {param.ParameterType}");

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

        private delegate int MeaningOfLife();

        private static void RunSample(DynamicMethod dMethod)
        {
            switch (sample)
            {
                case Sample.VoidVoid:
                    RunVoidVoidSample(dMethod);
                    break;
                case Sample.IntReturn:
                    RunIntReturnSample(dMethod);
                    break;
                case Sample.IntParamIntReturn:
                    RunIntParamIntReturnSample(dMethod);
                    break;
                case Sample.IntDoubleParamsIntReturn:
                    RunIntDoubleParamsIntReturnSample(dMethod);
                    break;
                default:
                    throw new Exception($"Unknown sample: {sample}");
            }
        }

        private static void RunVoidVoidSample(DynamicMethod dMethod)
        {
            Action answer = (Action)dMethod.CreateDelegate(typeof(Action));
            if (answer is null)
                throw new Exception("Delegate for the dynamic method is null");
            answer();
            Console.WriteLine("delegate returned");
        }

        private static void RunIntReturnSample(DynamicMethod dMethod)
        {
            MeaningOfLife answer = (MeaningOfLife)dMethod.CreateDelegate(typeof(MeaningOfLife));
            if (answer is null)
                throw new Exception("Delegate for the dynamic method is null");
            var retVal = answer();
            Console.WriteLine($"The answer is: {retVal}");
        }

        private static void RunIntParamIntReturnSample(DynamicMethod dMethod)
        {
            Func<int, int> answer = (Func<int, int>)dMethod.CreateDelegate(typeof(Func<int, int>));
            if (answer is null)
                throw new Exception("Delegate for the dynamic method is null");
            int retVal = answer(40);
            Console.WriteLine($"The answer is: {retVal}");
        }

        private static void RunIntDoubleParamsIntReturnSample(DynamicMethod dMethod)
        {
            Func<int, double, int> answer = (Func<int, double, int>)dMethod.CreateDelegate(typeof(Func<int, double, int>));
            if (answer is null)
                throw new Exception("Delegate for the dynamic method is null");
            int retVal = answer(40, 2.0);
            Console.WriteLine($"The answer is: {retVal}");
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
