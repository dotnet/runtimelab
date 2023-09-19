// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;

using Internal.Mint.Abstraction;

namespace Internal.Mint;

internal sealed class CompiledMethod : IMintCompiledMethod, IDisposable
{
    public MethodInfo MethodInfo { get; }
    public KnownInvokeShape InvokeShape { get; }
    public InterpMethodPtr InterpMethod { get; }
    public MemoryManager ExecMemoryManager { get; }
    public CompiledMethod(KnownInvokeShape invokeShape, InterpMethodPtr interpMethod, MemoryManager execMemoryManager)
    {
        InvokeShape = invokeShape;
        InterpMethod = interpMethod;
        ExecMemoryManager = execMemoryManager;
    }

    ~CompiledMethod() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private void Dispose(bool disposing)
    {
        ExecMemoryManager?.Dispose();
    }
#pragma warning restore IDE0060 // Remove unused parameter

    public object Invoke(object[] args)
    {
        InvokeWithKnownShape(args, out var result);
        switch (InvokeShape)
        {
            case KnownInvokeShape.VoidVoid:
                return null;
            case KnownInvokeShape.IntReturn:
            case KnownInvokeShape.IntParamIntReturn:
            case KnownInvokeShape.IntDoubleParamsIntReturn:
                int resultVal;
                unsafe
                {
                    resultVal = *(int*)result;
                }
                return resultVal;
            case KnownInvokeShape.Unrecognized:
            default:
                throw new InvalidOperationException("Unrecognized invoke shape");
        }
    }

    internal enum KnownInvokeShape
    {
        Unrecognized = 0,
        VoidVoid,
        IntReturn,
        IntParamIntReturn,
        IntDoubleParamsIntReturn,
    }

    internal static KnownInvokeShape GetKnownInvokeShape(MethodInfo dm)
    {
        KnownInvokeShape invokeShape = KnownInvokeShape.Unrecognized;
        if (dm.ReturnType == typeof(void))
        {
            if (dm.GetParameters().Length == 0)
            {
                invokeShape = KnownInvokeShape.VoidVoid;
            }
        }
        else if (dm.ReturnType == typeof(int))
        {
            var parameters = dm.GetParameters();
            if (parameters.Length == 0)
            {
                invokeShape = KnownInvokeShape.IntReturn;
            }
            else if (parameters.Length == 1)
            {
                if (parameters[0].ParameterType == typeof(int))
                {
                    invokeShape = KnownInvokeShape.IntParamIntReturn;
                }
            }
            else if (parameters.Length == 2)
            {
                if (parameters[0].ParameterType == typeof(int) &&
                    parameters[1].ParameterType == typeof(double))
                {
                    invokeShape = KnownInvokeShape.IntDoubleParamsIntReturn;
                }
            }
        }
        return invokeShape;
    }

    internal void InvokeWithKnownShape(object[] args, out IntPtr result)
    {
        switch (InvokeShape)
        {
            case KnownInvokeShape.VoidVoid:
                {
                    result = IntPtr.Zero;
                    Mint.mint_testing_ee_interp_entry_static_0(InterpMethod.Value);
                    break;
                }
            case KnownInvokeShape.IntReturn:
                {
                    result = ExecMemoryManager.Allocate(8);
                    Mint.mint_testing_ee_interp_entry_static_ret_0(result, InterpMethod.Value);
                    break;
                }
            case KnownInvokeShape.IntParamIntReturn:
                {
                    result = ExecMemoryManager.Allocate(8);
                    int arg1 = (int)args[0];
                    unsafe
                    {
                        IntPtr arg1Ptr = (IntPtr)(void*)&arg1;
                        Mint.mint_testing_ee_interp_entry_static_ret_1(result, arg1Ptr, InterpMethod.Value);
                    }
                    break;
                }
            case KnownInvokeShape.IntDoubleParamsIntReturn:
                {
                    result = ExecMemoryManager.Allocate(8);
                    int arg1 = (int)args[0];
                    double arg2 = (double)args[1];
                    unsafe
                    {
                        IntPtr arg1Ptr = (IntPtr)(void*)&arg1;
                        IntPtr arg2Ptr = (IntPtr)(void*)&arg2;
                        Mint.mint_testing_ee_interp_entry_static_ret_2(result, arg1Ptr, arg2Ptr, InterpMethod.Value);
                    }
                    break;
                }
            case KnownInvokeShape.Unrecognized:
            default:
                throw new InvalidOperationException("Unrecognized invoke shape");
        }
    }


}
