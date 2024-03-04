// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

namespace System.Runtime.JitTesting;

public unsafe class LSSATests
{
    public static void Run()
    {
        int value = 10;
        object obj = NewObject();

#if DEBUG
        NotOptimized_ParameterUnused(NewObject(), NewObject());
#else
        Optimized_ParameterUnused(NewObject());
        Optimized_ParameterNotExposed(NewObject());
        Optimized_ParameterExposed(NewObject());
        Optimized_AddressExposedValuesAndEH();
        Optimized_LastUses_DisjointValuesNotExposed();
        Optimized_LastUses_ForkedFlowNotExposed(flag: true);
        Optimized_LastUses_ForkedFlowExposed(flag: true, NewObject());
        Optimized_LastUses_UseLocation(NewObject());
        Optimized_LastUses_OutOfOrder(NewObject());
        Optimized_LastUses_OutOfOrderMultiple(flag: true, NewObject());
        Optimized_ExplicitInit_NotExposed(ref value, &obj);
        Optimized_ExplicitInit_Exposed();
        Optimized_NonGcValues_SingleDef(flag: true);
        Optimized_AlreadySpilled(NewObject());
        Optimized_AlreadySpilled_AddressExposed(NewObject());
        Optimized_AlreadySpilled_DerivedAddress(flag: true, new ClassWithField(), new int[1]);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("""
     BB01:
       STORE SS00 ARG00 ARG00
       STORE SS01 ARG01 ARG01
     """)]
    private static void NotOptimized_ParameterUnused(object x, object y)
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("")]
    private static void Optimized_ParameterUnused(object x)
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("")]
    private static void Optimized_ParameterNotExposed(object x)
    {
        SafePoint(x);
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("BB01: STORE SS00 ARG00 ARG00/1")]
    private static void Optimized_ParameterExposed(object x)
    {
        SafePoint(x);
        SafePoint(x);
    }

    private static object* s_exposedLocalAddress;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Optimized_AddressExposedValuesAndEH()
    {
        // It may be tempting to elide homing GC values on the shadow stack without
        // explicit safe points... This test shows why that would not be correct.
        [MethodImpl(MethodImplOptions.NoInlining), LSSATest("BB01: STORE SS00 ARG01 ARG01")]
        void Optimized_AddressExposedValuesAndEH_Impl(int* p, object x)
        {
            s_exposedLocalAddress = &x;
            _ = *p; // This will call "PeskyFilter", and "x" must still be live at that point.
        }

        bool PeskyFilter()
        {
            SafePoint(null);
            SafePoint(*s_exposedLocalAddress);
            return true;
        }

        try
        {
            Optimized_AddressExposedValuesAndEH_Impl(null, NewObject());
        }
        catch when (PeskyFilter()) { }
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("")]
    private static void Optimized_LastUses_DisjointValuesNotExposed()
    {
        object x = NewObject();
        ForceILLocal(&x);
        SafePoint(x);

        x = NewObject();
        SafePoint(x);
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("")]
    private static void Optimized_LastUses_ForkedFlowNotExposed(bool flag)
    {
        object x = NewObject();
        ForceILLocal(&x);

        if (flag)
        {
            SafePoint(x);
        }
        else
        {
            AnotherSafePoint(x);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("BB01: STORE SS00 ARG01 ARG01/1")]
    private static void Optimized_LastUses_ForkedFlowExposed(bool flag, object x)
    {
        // Test that we handle the following case properly:
        //
        // [x = ...]---------------->
        //      |                   |
        // [SafePoint(x)] <-- [SafePoint(x)]
        //
        // "x" will be live across a safepoint in this scenario.
        //
        if (flag)
        {
            goto ANOTHER_SAFE_POINT;
        }

    SAFE_POINT:
        SafePoint(x);
        return;

    ANOTHER_SAFE_POINT:
        AnotherSafePoint(x);
        goto SAFE_POINT;
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("BB01: STORE SS00 ARG00 ARG00/1")]
    private static void Optimized_LastUses_UseLocation(object x)
    {
        // Test that we're not using RyuJit's last use flags "blindly".
        // Case 1: locals are used at their parent:
        //
        // t1 = LCL_VAR V00 ; last use
        //      CALL(...)
        //      CALL(t1)
        // V00 is still live across CALL(...)
        //
        SafePoint(x, SafePointWithReturn(null));
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("BB01: STORE SS00 ARG00 ARG00/1")]
    private static void Optimized_LastUses_OutOfOrder(object x)
    {
        // Test that we're not using RyuJit's last use flags "blindly".
        // Case 2: locals with "out of order" last uses:
        //
        // t1 = LCL_VAR V00 ; actual last use
        // t2 = LCL_VAR V00 ; tagged last use
        //      CALL(t2)
        //      CALL(t1)
        // V00 is still live across CALL(t2)
        //
        SafePoint(x, SafePointWithReturn(x));
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("""
     BB01:
       STORE SS00 ARG01 ARG01/1
     BB02:
       STORE SS00 ARG01 ARG01/2
     """)]
    private static void Optimized_LastUses_OutOfOrderMultiple(bool flag, object x)
    {
        // Same as above, but checks a case with multiple definitions in different blocks.
        SafePoint(x, SafePointWithReturn(x));

        if (flag)
        {
            x = NewObject();
            SafePoint(x, SafePointWithReturn(x));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("BB01: STORE SS00 USR02 USR02/2")]
    private static void Optimized_ExplicitInit_NotExposed(ref int valueRef, object* pObj)
    {
        valueRef++;

        // Test that this object does not get zero-init-ed. There are no safe points between it and the prolog.
        object x = *pObj;
        ForceILLocal(&x);
        SafePoint(null);
        SafePoint(x, x);
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("""
     BB01:
       ZEROINIT SS00
       STORE SS00 USR00 USR00/2
     """)]
    private static void Optimized_ExplicitInit_Exposed()
    {
        SafePoint(null);

        // Test that this object does get zero-init-ed. There are safe points between it and the prolog.
        // Note this is a policy choice: we don't want too many "random" pointers on the shadow stack.
        object x = NewObject();
        ForceILLocal(&x);
        SafePoint(null);
        SafePoint(x, x);
    }

    // TODO-LLVM: add this test, but in such a way that we don't need to encode the virtual unwind
    // frame implementation detail...
    //
    // static int* s_pValue;
    //
    // [MethodImpl(MethodImplOptions.NoInlining)]
    // private static void Problem(int* pValue, object* pObj)
    // {
    // TRY_AGAIN:
    //     try
    //     {
    //         (*s_pValue)++;
    // 
    //         // In this test, the line above will throw and control will pass through
    //         // the EH system (itself a safepoint) and return back to this method.
    //         // Make sure the shadow stack slot for the local below will be zero-initialized.
    //         object x = *pObj;
    //         ForceILLocal(&x);
    //         SafePoint(null);
    //         SafePoint(x, x);
    //     }
    //     catch
    //     {
    //         s_pValue = (int*)NativeMemory.Alloc(4);
    //         SafePoint(null);
    //         goto TRY_AGAIN;
    //     }
    // }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("")]
    private static void Optimized_NonGcValues_SingleDef(bool flag)
    {
        int value = 0;
        ref int valueRef = ref value;
        if (!flag)
        {
            // Make propagation optimizations less likely.
            return;
        }

        SafePoint(ref valueRef);
        SafePoint(ref valueRef);

        object x = "xyz"; // Frozen string object.
        for (int i = 0; i < 10; i++)
        {
            // Make constant propagation look unprofitable with this loop.
            SafePoint(x, x);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("BB01: STORE SS00 ARG00 ARG00/1")]
    private static void Optimized_AlreadySpilled(object x)
    {
        SafePoint(null);

        // Test that we only spill "x", thus also rooting "y".
        object y = x;
        ForceILLocal(&y);
        SafePoint(y, y);
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("""
     BB01:
       STORE SS00 ARG00 ARG00
       LOAD  SS00 ARG00
     """)]
    private static void Optimized_AlreadySpilled_AddressExposed(object x)
    {
        SafePoint(ref x);

        // Same test as above, only now the source is address-exposed.
        object y = x;
        ForceILLocal(&y);
        SafePoint(y, y);
    }

    [MethodImpl(MethodImplOptions.NoInlining), LSSATest("""
     BB01:
       STORE SS00 ARG01 ARG01/1
       STORE SS01 ARG02 ARG02/1
     """)]
    private static void Optimized_AlreadySpilled_DerivedAddress(bool flag, ClassWithField x, int[] y)
    {
        SafePoint(x);
        SafePoint(x);

        // Test that we do not spill "xRef" - it should be rooted by the spill of "x".
        ref int xRef = ref x.Field;
        if (!flag)
        {
            return;
        }

        SafePoint(ref xRef);
        SafePoint(ref xRef);

        // Same idea, but now with array access.
        ref int yRef = ref y[x.Field];
        if (!flag)
        {
            return;
        }

        SafePoint(ref yRef);
        SafePoint(ref yRef);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SafePoint(object x) { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SafePoint(object x, object y) { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object SafePointWithReturn(object x) => x;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SafePoint(ref object x) { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SafePoint(ref int x) { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AnotherSafePoint(object x) { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object NewObject() => new object();

    // Roslyn likes to eliminate locals we write. Force it to abstain with this function.
    private static void ForceILLocal(void* x) { }

    class ClassWithField
    {
        public int Field;
    }
}

public sealed class LSSATestAttribute(string allocation) : System.Attribute
{
    public string Allocation { get; } = allocation;
}
