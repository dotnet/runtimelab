// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.JitTesting;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Specialized;
using System.Globalization;

using CpObj;
using CkFinite;

internal unsafe partial class Program
{
    private static int staticInt;
    [ThreadStatic]
    private static int threadStaticInt;

    private static unsafe int Main(string[] args)
    {
        var x = new StructWithObjRefs
        {
            C1 = null,
            C2 = null,
        };
        Success = true;
        PrintLine("Starting " + 1);

        TestBox();

        TestSByteExtend();
        TestMetaData();

        TestGC();
        TestFinalization();

        Add(1, 2);
        PrintLine("Hello from C#!");
        int tempInt = 0;
        int tempInt2 = 0;
        StartTest("Address/derefernce test");
        (*(&tempInt)) = 9;
        EndTest(tempInt == 9);

        int* targetAddr = (tempInt > 0) ? (&tempInt2) : (&tempInt);

        StartTest("basic block stack entry Test");
        (*targetAddr) = 1;
        EndTest(tempInt2 == 1 && tempInt == 9);

        StartTest("Inline assign byte Test");
        EndTest(ILHelpers.ILHelpersTest.InlineAssignByte() == 100);

        StartTest("dup test");
        int dupTestInt = 9;
        EndTest(ILHelpers.ILHelpersTest.DupTest(ref dupTestInt) == 209 && dupTestInt == 209);

        TestClass tempObj = new TestDerivedClass(1337);
        tempObj.TestMethod("Hello");
        tempObj.TestVirtualMethod("Hello");
        tempObj.TestVirtualMethod2("Hello");

        TwoByteStr str = new TwoByteStr() { first = 1, second = 2 };
        TwoByteStr str2 = new TwoByteStr() { first = 3, second = 4 };
        *(&str) = str2;
        str2 = *(&str);

        StartTest("value type int field test");
        EndTest(str2.second == 4);

        StartTest("static int field test");
        staticInt = 5;
        EndTest(staticInt == 5);

        StartTest("thread static int initial value field test");
        EndTest(threadStaticInt == 0);

        StartTest("thread static int field test");
        threadStaticInt = 9;
        EndTest(threadStaticInt == 9);

        StaticCtorTest();

        StartTest("box test");
        var boxedInt = (object)tempInt;
        if (((int)boxedInt) == 9)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Value:");
            PrintLine(boxedInt.ToString());
        }

        TestBoxUnboxDifferentSizes();

        var boxedStruct = (object)new BoxStubTest { Value = "Boxed Stub Test: Ok." };
        PrintLine(boxedStruct.ToString());

        StartTest("Subtraction Test");
        int subResult = tempInt - 1;
        EndTest(subResult == 8);

        StartTest("Division Test");
        int divResult = tempInt / 3;
        EndTest(divResult == 3);

        StartTest("Addition of byte and short test");
        byte aByte = 2;
        short aShort = 0x100;
        short byteAndShortResult = (short)(aByte + aShort);
        EndTest(byteAndShortResult == 0x102);

        StartTest("not test");
        var not = Not(0xFFFFFFFF) == 0x00000000;
        EndTest(not);

        StartTest("negInt test");
        var negInt = Neg(42) == -42;
        EndTest(negInt);

        StartTest("shiftLeft test");
        var shiftLeft = ShiftLeft(1, 2) == 4;
        EndTest(shiftLeft);

        StartTest("shiftRight test");
        var shiftRight = ShiftRight(4, 2) == 1;
        EndTest(shiftRight);

        StartTest("unsignedShift test");
        var unsignedShift = UnsignedShift(0xFFFFFFFFu, 4) == 0x0FFFFFFFu;
        EndTest(unsignedShift);

        StartTest("shiftLeft byte to short test");
        byte byteConstant = (byte)0x80;
        ushort shiftedToShort = (ushort)(byteConstant << 1);
        EndTest((int)shiftedToShort == 0x0100);

        StartTest("SwitchOp0 test");
        var switchTest0 = SwitchOp(5, 5, 0);
        EndTest(switchTest0 == 10);

        StartTest("SwitchOp1 test");
        var switchTest1 = SwitchOp(5, 5, 1);
        EndTest(switchTest1 == 25);

        StartTest("SwitchOpDefault test");
        var switchTestDefault = SwitchOp(5, 5, 20);
        EndTest(switchTestDefault == 0);

        StartTest("CpObj test");
        var cpObjTestA = new TestValue { Field = 1234 };
        var cpObjTestB = new TestValue { Field = 5678 };
        CpObjTest.CpObj(ref cpObjTestB, ref cpObjTestA);
        EndTest(cpObjTestB.Field == 1234);

        StartTest("Static delegate test");
        Func<int> staticDelegate = StaticDelegateTarget;
        EndTest(staticDelegate() == 7);

        StartTest("Instance delegate test");
        tempObj.TestInt = 8;
        Func<int> instanceDelegate = tempObj.InstanceDelegateTarget;
        EndTest(instanceDelegate() == 8);

        StartTest("Virtual Delegate Test");
        Action virtualDelegate = tempObj.VirtualDelegateTarget;
        virtualDelegate();

        var arrayTest = new BoxStubTest[] { new BoxStubTest { Value = "Hello" }, new BoxStubTest { Value = "Array" }, new BoxStubTest { Value = "Test" } };
        foreach (var element in arrayTest)
            PrintLine(element.Value);

        arrayTest[1].Value = "Array load/store test: Ok.";
        PrintLine(arrayTest[1].Value);

        int ii = 0;
        arrayTest[ii++].Value = "dup ref test: Ok.";
        PrintLine(arrayTest[0].Value);

        StartTest("Large array load/store test");
        var largeArrayTest = new long[] { Int64.MaxValue, 0, Int64.MinValue, 0 };
        EndTest(largeArrayTest[0] == Int64.MaxValue &&
                largeArrayTest[1] == 0 &&
                largeArrayTest[2] == Int64.MinValue &&
                largeArrayTest[3] == 0);

        StartTest("Small array load/store test");
        var smallArrayTest = new long[] { Int16.MaxValue, 0, Int16.MinValue, 0 };
        EndTest(smallArrayTest[0] == Int16.MaxValue &&
                smallArrayTest[1] == 0 &&
                smallArrayTest[2] == Int16.MinValue &&
                smallArrayTest[3] == 0);

        StartTest("Newobj value type test");
        IntPtr returnedIntPtr = NewobjValueType();
        EndTest(returnedIntPtr.ToInt32() == 3);

        TestShadowStackAlignment();

        StackallocTest();

        IntToStringTest();

        CastingTestClass castingTest = new DerivedCastingTestClass1();

        PrintLine("interface call test: Ok " + (castingTest as ICastingTest1).GetValue().ToString());

        StartTest("Type casting with isinst & castclass to class test");
        EndTest(((DerivedCastingTestClass1)castingTest).GetValue() == 1 && !(castingTest is DerivedCastingTestClass2));

        StartTest("Type casting with isinst & castclass to interface test");
        // Instead of checking the result of `GetValue`, we use null check by now until interface dispatch is implemented.
        EndTest((ICastingTest1)castingTest != null && !(castingTest is ICastingTest2));

        StartTest("Type casting with isinst & castclass to array test");
        object arrayCastingTest = new BoxStubTest[] { new BoxStubTest { Value = "Array" }, new BoxStubTest { Value = "Cast" }, new BoxStubTest { Value = "Test" } };
        PrintLine(((BoxStubTest[])arrayCastingTest)[0].Value);
        PrintLine(((BoxStubTest[])arrayCastingTest)[1].Value);
        PrintLine(((BoxStubTest[])arrayCastingTest)[2].Value);
        EndTest(!(arrayCastingTest is CastingTestClass[]));

        ConvUTest();

        CastByteForIndex();

        ldindTest();

        InterfaceDispatchTest();

        StartTest("Runtime.Helpers array initialization test");
        var testRuntimeHelpersInitArray = new long[] { 1, 2, 3 };
        EndTest(testRuntimeHelpersInitArray[0] == 1 &&
                testRuntimeHelpersInitArray[1] == 2 &&
                testRuntimeHelpersInitArray[2] == 3);

        StartTest("Multi-dimension array instantiation test");
        var testMdArrayInstantiation = new int[2, 2];
        EndTest(testMdArrayInstantiation != null && testMdArrayInstantiation.GetLength(0) == 2 && testMdArrayInstantiation.GetLength(1) == 2);

        StartTest("Multi-dimension array get/set test");
        testMdArrayInstantiation[0, 0] = 1;
        testMdArrayInstantiation[0, 1] = 2;
        testMdArrayInstantiation[1, 0] = 3;
        testMdArrayInstantiation[1, 1] = 4;
        EndTest(testMdArrayInstantiation[0, 0] == 1
                && testMdArrayInstantiation[0, 1] == 2
                && testMdArrayInstantiation[1, 0] == 3
                && testMdArrayInstantiation[1, 1] == 4);


        FloatDoubleTest();

        StartTest("long comparison");
        long l = 0x1;
        EndTest(l < 0x7FF0000000000000);

        // Create a ByReference<char> through the ReadOnlySpan ctor and call the ByReference.Value via the indexer.
        StartTest("ByReference intrinsics exercise via ReadOnlySpan");
        var span = "123".AsSpan();
        if (span[0] != '1'
            || span[1] != '2'
            || span[2] != '3')
        {
            FailTest();
            PrintLine(span[0].ToString());
            PrintLine(span[1].ToString());
            PrintLine(span[2].ToString());
        }
        else
        {
            PassTest();
        }

        TestConstrainedClassCalls();

        TestConstrainedStructCalls();

        TestLdvirtftn();

        TestValueTypeElementIndexing();

        TestArrayItfDispatch();

        StartTest("RVA static field test");
        int rvaFieldValue = ILHelpers.ILHelpersTest.StaticInitedInt;
        if (rvaFieldValue == 0x78563412)
        {
            PassTest();
        }
        else
        {
            FailTest(rvaFieldValue.ToString());
        }

        TestNativeCallback();

        LazyDllImportThrows();

        TestDirectPInvoke();

#if false // TODO-LLVM: Should throw an EntryPointNotFoundException, but fails in the Wasm runtime (RuntimeError: null function or function signature mismatch)
        TestEntryPointNotFoundForWasmImport();
#endif

        TestStaticAbiCompatibleSignatures();

#if !CODEGEN_WASI // Easier to test with Javascript/Emscripten.

        TestNamedModuleCall();

        TestNamedModuleCallWithoutEntryPoint();

        TestSameFunctionNameInDifferentModules();

        TestStaticPInvokeOverloadedInDifferentModules();

        TestWasmImportAbiCompatibleSignatures();
#endif

        TestNativeCallsWithMismatchedSignatures();

        TestArgsWithMixedTypesAndExceptionRegions();

        TestThreadStaticsForSingleThread();

        TestDispose();

        TestCallToGenericInterfaceMethod();

        TestInitObjDouble();

        TestTryCatch();

        StartTest("Non/GCStatics field access test");
        if (new FieldStatics().TestGetSet())
        {
            PassTest();
        }
        else
        {
            FailTest();
        }

        TestSByteExtend();

        TestSharedDelegate();

        TestUlongUintMultiply();

        TestBoxSingle();

        TestGvmCallInIf(new GenDerived<string>(), "hello");

        TestStoreFromGenericMethod();

        TestConstrainedValueTypeCallVirt();

        TestBoxToGenericTypeFromDirectMethod();

        TestGenericStructHandling();

        TestGenericCallInFinally();

        TestInitializeArray();

        TestImplicitUShortToUInt();

        TestReverseDelegateInvoke();

        TestInterlocked();

        TestThrowIfNull();

        TestCkFinite();

        TestFloatToIntConversions();

        TestIntOverflows();

#if !CODEGEN_WASI // TODO-LLVM: stack traces on WASI.
        TestStackTrace();
#endif

        if (OperatingSystem.IsBrowser())
        {
            TestJavascriptCall();
        }

        TestPalRandom();

        TestGlobalization();

        TestDefaultConstructorOf();

        TestStructUnboxOverload();

        TestGetSystemArrayEEType();

        TestBoolCompare();

        TestDifferentSizeIntOperator();

        TestStructStoreWithSignificantPadding();

        TestLclVarAddr(new LlvmStruct { i1 = 1, i2 = 2 });

        TestJitUseStruct();

        TestMismatchedStructLocalFieldStore();

        TestUnsafe();

        TestReadByteArray();

        TestDoublePrint();

        TestGenStructContains();

        LSSATests.Run();

        TestThreadStaticAlignment();

        if (OperatingSystem.IsBrowser())
        {
            EventLoopTestClass.TestEventLoopIntegration();
        }

        PrintLine("Done");
        return Success ? 100 : -1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private unsafe static StructWithIndex JitUseStructProblem(StructWithStructWithIndex* p, StructWithIndex b)
    {
        b = p->StructWithIndex;
        JitUse(&b);

        return b;
    }

    struct StructWithIndex
    {
        public int Index;
        public int Value;
    }

    struct StructWithStructWithIndex
    {
        public StructWithIndex StructWithIndex;
        public int AnotherIndex;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public unsafe static void JitUse<T>(T* arg) where T : unmanaged { }

    private unsafe static void TestJitUseStruct()
    {
        StartTest("TestJitUseStruct (Jit compilation struct test)");
        StructWithIndex structWithIndex = new StructWithIndex() { Index = 1, Value = 2 };
        StructWithStructWithIndex structWithStruct =
            new StructWithStructWithIndex() { StructWithIndex = structWithIndex, AnotherIndex = 3 };

        var res = JitUseStructProblem(&structWithStruct, structWithIndex);

        EndTest(res.Index == structWithIndex.Index && res.Value == structWithIndex.Value);
    }

    public struct StructWrapper
    {
        public readonly Guid WrappedStruct;
        public StructWrapper(Guid value) => WrappedStruct = value;
    }

    private static void TestMismatchedStructLocalFieldStore()
    {
        const string GuidValue = "0c733a1e-2a1c-11ce-ade5-00aa0044773d";

        [MethodImpl(MethodImplOptions.NoInlining)]
        bool ExposeAndVerify(ref StructWrapper? x)
        {
            return x.HasValue && x.Value.WrappedStruct.ToString() == GuidValue;
        }

        StartTest("TestMismatchedStructLocalFieldStore");
        StructWrapper? x = new StructWrapper(Guid.Parse(GuidValue));
        EndTest(ExposeAndVerify(ref x));
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct LandPatchData
    {
        public uint Index;
        public uint Pointer;
        public LandPatchData* Next;

        public LandPatchData(uint index, uint ptr)
        {
            Index = index;
            Pointer = ptr;
            Next = null;
        }
    }

    private static Dictionary<uint, LandPatchData> _landPatchPtrs;

    private unsafe static void TestUnsafe()
    {
        StartTest("TestUnsafe");

        uint key = 1;
        _landPatchPtrs = new Dictionary<uint, LandPatchData>();
        ref var data = ref CollectionsMarshal.GetValueRefOrNullRef(_landPatchPtrs, key);

        if (Unsafe.IsNullRef(ref data))
        {

        }

        something();
        // just testing if the compilation succeeds
        EndTest(true);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Test { public uint A, B; }

    unsafe static void something()
    {
        Span<byte> s = stackalloc byte[System.Runtime.CompilerServices.Unsafe.SizeOf<Test>()];
        var xxx = System.Runtime.CompilerServices.Unsafe.AsPointer(ref MemoryMarshal.GetReference(s));
    }

    class ShortAndByte { internal short aShort; internal byte aByte; }
    private static void TestDifferentSizeIntOperator()
    {
        StartTest("Logical and short and int");

        var o = new ShortAndByte
        {
            aShort = 3,
            aByte = 2,
        };

        EndTest((o.aShort & o.aByte) == 2);
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    struct ExplicitStructNoGCPtr
    {
        [FieldOffset(0)]
        public int A;
    }

    [StructLayout(LayoutKind.Sequential, Size = 8)]
    struct SizedStructNoGCPtr
    {
        public int A;
    }

    private static unsafe void TestStructStoreWithSignificantPadding()
    {
        StartTest("Significant padding struct store");

        ExplicitStructNoGCPtr aStruct;
        int* ptrStruct = (int*)&(aStruct);
        ptrStruct = ptrStruct + 1;
        // store something in space not used by any field
        *ptrStruct = 2;

        var copy1 = aStruct;
        int* ptrStruct2 = (int*)&copy1;
        ptrStruct2 = ptrStruct2 + 1;

        if (*ptrStruct2 != 2)
        {
            FailTest("Explicit store failed");
        }

        SizedStructNoGCPtr sizedStruct;
        int* ptrSizedStruct = (int*)&(sizedStruct);
        ptrSizedStruct = ptrSizedStruct + 1;
        // store something in space not used by any field
        *ptrSizedStruct = 2;

        var copySized = sizedStruct;
        int* ptrCopySized = (int*)&copySized;
        ptrCopySized = ptrCopySized + 1;

        EndTest(*ptrCopySized == 2);
    }

    struct LlvmStruct
    {
        internal int i1, i2;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int FuncWithStructArg(LlvmStruct s)
    {
        return s.i2;
    }

    private static void TestLclVarAddr(LlvmStruct s)
    {
        StartTest("Test passing struct arg");

        EndTest(FuncWithStructArg(s) == 2);
    }

    private static void TestGC()
    {
        StartTest("GC");

        var genOfNewObject = GC.GetGeneration(new object());
        PrintLine("Generation of new object " + genOfNewObject.ToString());
        if (genOfNewObject != 0)
        {
            FailTest("Gen of new object was " + genOfNewObject);
            return;
        }
        var weakReference = MethodWithObjectInShadowStack();
        GC.Collect();
        GC.Collect();
        if (weakReference.IsAlive)
        {
            FailTest("object alive when has no references");
            return;
        }
        for (var i = 0; i < 3; i++)
        {
            PrintString("GC Collection Count " + i.ToString() + " ");
            PrintLine(GC.CollectionCount(i).ToString());
        }
        if (!TestCreateDifferentObjects())
        {
            FailTest("Failed test for creating/collecting different objects");
        }
        GC.Collect();
        GC.Collect();
        for (var i = 0; i < 3; i++)
        {
            PrintString("GC Collection Count " + i.ToString() + " ");
            PrintLine(GC.CollectionCount(i).ToString());
        }

        if (!TestObjectRefInUncoveredShadowStackSlot())
        {
            FailTest("struct Child1 alive unexpectedly");
        }

        if (!TestRhpAssignRefWithClassInStructGC())
        {
            FailTest();
            return;
        }

        if (!StackEntriesLiveAcrossSafePointsGetScanned())
        {
            FailTest("Stack entry live across a safe point was not reported");
            return;
        }

        EndTest(TestGeneration2Rooting());
    }

    struct MiniRandom
    {
        private uint _val;

        public MiniRandom(uint seed)
        {
            _val = seed;
        }

        public uint Next()
        {
            _val ^= (_val << 13);
            _val ^= (_val >> 7);
            _val ^= (_val << 17);
            return _val;
        }
    }

    class F4 { internal int i; }
    class F8 { internal long l; }
    class F2Plus8 { internal short s; internal long l; }
    class CDisp : IDisposable { public void Dispose() { } }
    struct StructF48 { internal int i1; internal long l2; }
    private static bool TestCreateDifferentObjects()
    {
        var mr = new MiniRandom(257);
        var keptObjects = new object[100];
        for (var i = 0; i < 1000000; i++)
        {
            var r = mr.Next();
            object o;
            switch (r % 8)
            {
                case 0:
                    o = new F4 { i = 1, };
                    break;
                case 1:
                    o = new F8 { l = 4 };
                    break;
                case 2:
                    o = new F2Plus8 { l = 5, s = 6 };
                    break;
                case 3:
                    o = i.ToString();
                    break;
                case 4:
                    o = new long[10000];
                    break;
                case 5:
                    o = new int[10000];
                    break;
                case 6:
                    o = new StructF48 { i1 = 7, l2 = 8 };
                    break;
                case 7:
                    o = new CDisp();
                    break;
                default:
                    o = null;
                    break;
            }
            keptObjects[r % 100] = o;
        }
        return true;
    }

    private static Parent aParent;
    private static ParentOfStructWithObjRefs aParentOfStructWithObjRefs;
    private static WeakReference childRef;

    private static unsafe bool TestRhpAssignRefWithClassInStructGC()
    {
        bool result = true;

        var parentRef = CreateParentWithStruct();
        result &= BumpToGen(parentRef, 1);
        result &= BumpToGen(parentRef, 2);

        StoreChildInC1();
        GC.Collect(1);
        PrintLine("GC finished");

        if (!childRef.IsAlive)
        {
            PrintLine("Child died unexpectedly");
            result = false;
        }

        KillParentWithStruct();
        GC.Collect();
        if (childRef.IsAlive)
        {
            PrintLine("Child alive unexpectedly");
            result = false;
        }
        if (parentRef.IsAlive)
        {
            PrintLine("Parent of struct Child1 alive unexpectedly");
            result = false;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool BumpToGen(WeakReference reference, int expectedGeneration)
    {
        GC.Collect();
        var target = reference.Target;
        if (target == null)
        {
            PrintLine("WeakReference died unexpectedly");
            return false;
        }
        if (GC.GetGeneration(target) is { } actualGeneration && actualGeneration != expectedGeneration)
        {
            PrintLine("WeakReference is in gen " + actualGeneration + " instead of " + expectedGeneration);
            return false;
        }
        return true;
    }

    private static bool TestGeneration2Rooting()
    {
        var parent = CreateParent();
        GC.Collect(); // parent moves to gen1
        GC.Collect(); // parent moves to gen2
        if (!CheckParentGeneration()) return false;

        // store our children in the gen2 object
        var child1 = StoreProperty();
        var child2 = StoreField();

        KillParent(); // even though we kill the parent, it should survive as we do not collect gen2
        GC.Collect(1);

        // the parent should have kept the children alive
        bool parentAlive = parent.IsAlive;
        bool child1Alive = child1.IsAlive;
        bool child2Alive = child2.IsAlive;
        if (!parentAlive)
        {
            PrintLine("Parent died unexpectedly");
            return false;
        }

        if (!child1Alive)
        {
            PrintLine("Child1 died unexpectedly");
            return false;
        }

        if (!child2Alive)
        {
            PrintLine("Child2 died unexpectedly");
            return false;
        }

        // Test struct assignment keeps fields alive
        var parentRef = CreateParentWithStruct();
        GC.Collect(); // move parent to gen1
        GC.Collect(); // move parent to gen2
        StoreChildInC1(); // store ephemeral object in gen 2 object via struct assignment
        KillParentWithStruct();
        GC.Collect(1);

        if (childRef.IsAlive)
        {
            PrintLine("Child1 gen:" + GC.GetGeneration(childRef.Target));
        }

        if (!childRef.IsAlive)
        {
            PrintLine("struct Child1 died unexpectedly");
            return false;
        }
        if (!parentRef.IsAlive)
        {
            PrintLine("parent of struct Child1 died unexpectedly");
            return false;
        }

        return true;
    }

    class ParentOfStructWithObjRefs
    {
        internal StructWithObjRefs StructWithObjRefs;
    }

    struct StructWithObjRefs
    {
        internal Child C1;
        internal Child C2;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference CreateParent()
    {
        var parent = new Parent();
        aParent = parent;
        return new WeakReference(parent);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference CreateStruct()
    {
        var parent = new Parent();
        aParent = parent;
        return new WeakReference(parent);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void KillParent()
    {
        aParent = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool CheckParentGeneration()
    {
        int actualGen = GC.GetGeneration(aParent);
        if (actualGen != 2)
        {
            PrintLine("Parent Object is not in expected generation 2 but in " + actualGen);
            return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference StoreProperty()
    {
        var child = new Child();
        aParent.Child1 = child;
        return new WeakReference(child);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference StoreField()
    {
        var child = new Child();
        aParent.Child2 = child;
        return new WeakReference(child);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    unsafe static WeakReference CreateParentWithStruct()
    {
        var parent = new ParentOfStructWithObjRefs();
        aParentOfStructWithObjRefs = parent;
        return new WeakReference(parent);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void KillParentWithStruct()
    {
        aParentOfStructWithObjRefs = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe void StoreChildInC1()
    {
        var child = new Child();
        aParentOfStructWithObjRefs.StructWithObjRefs = new StructWithObjRefs
        {
            C1 = child,
        };
        childRef = new WeakReference(child);
    }

    public class Parent
    {
        public Child Child1 { get; set; }
        public Child Child2;
    }

    public class Child
    {
    }

    // This test is to catch where slots are allocated on the shadow stack uncovering object references that were there previously.
    // If this happens in the call to GC.Collect, which at the time of writing allocate 12 bytes in the call, 3 slots, then any objects that were in those 
    // 3 slots will not be collected as they will now be (back) in the range of bottom of stack -> top of stack.
    private static unsafe bool TestObjectRefInUncoveredShadowStackSlot()
    {
        CreateObjectRefsInShadowStack();
        GC.Collect();
        return !childRef.IsAlive;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe void CreateObjectRefsInShadowStack()
    {
        var child = new Child();
        Child c1, c2, c3;  // 3 more locals to cover give a bit more resiliency to the test, in case of slots being added or removed in the RhCollect calls
        c1 = c2 = c3 = child;
        childRef = new WeakReference(child);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool StackEntriesLiveAcrossSafePointsGetScanned()
    {
        ClassWithFields obj = GetClass();
        ClearShadowStack(null, null, null, null, null, null);

        return StackEntriesLiveAcrossSafePointsGetScannedInner<object>(obj);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateLotsOfGarbage()
    {
        for (int i = 0; i < 10000; i++)
        {
            GC.KeepAlive(new object());
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TriggerObjRelocation()
    {
        CreateLotsOfGarbage();
        GC.Collect();
        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ObjRefsAreEqual(object a, int b, object c)
    {
        return a == c;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ClearShadowStack(object a, object b, object c, object d, object e, object f)
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ClassWithFields GetClass()
    {
        var outerObj = new ClassWithFields();
        CreateLotsOfGarbage();
        outerObj.Obj = new object();
        return outerObj;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool StackEntriesLiveAcrossSafePointsGetScannedInner<T>(ClassWithFields obj)
    {
        return ObjRefsAreEqual(obj.Obj, TriggerObjRelocation(), obj.Obj);
    }

    private class ClassWithFields
    {
        public object Obj;
    }

    private static unsafe void TestBoxUnboxDifferentSizes()
    {
        StartTest("Box/Unbox different sizes");
        var pass = true;
        long longValue = Convert.ToInt64((object)11111111L);
        if (longValue != 11111111L)
        {
            FailTest("Int64");
            pass = false;
        }

        int intValue = Convert.ToInt32((object)11111111);
        if (intValue != 11111111)
        {
            FailTest("Int32");
            pass = false;
        }

        float singleValue = Convert.ToSingle((object)1f);
        if (singleValue != 1f)
        {
            FailTest("Single");
            pass = false;
        }

        double doubleValue = Convert.ToDouble((object)1D);
        if (doubleValue != 1D)
        {
            FailTest("Double");
            pass = false;
        }

        short s1 = 1;
        short shortValue = Convert.ToInt16((object)s1);
        if (shortValue != 1)
        {
            FailTest("Int16");
            pass = false;
        }

        byte b1 = 1;
        byte byteValue = Convert.ToByte((object)b1);
        if (byteValue != 1)
        {
            FailTest("Byte");
            pass = false;
        }

        var s = new StructWintIntf();
        s.IntField = 11111111;
        s.LongField = 222222222L;
        IHasTwoFields hasTwoFields = (IHasTwoFields)s;

        if (hasTwoFields.GetIntField() != 11111111)
        {
            FailTest("GetIntField");
            pass = false;
        }
        if (hasTwoFields.GetLongField() != 222222222L)
        {
            FailTest("GetLongField");
            pass = false;
        }

        EndTest(pass);
    }

    private static WeakReference MethodWithObjectInShadowStack()
    {
        var o = new object();
        var wr = new WeakReference(o);
        if (!wr.IsAlive)
        {
            FailTest("object not alive when still referenced and not collected");
            return wr;
        }
        GC.Collect();
        GC.Collect();
        if (!wr.IsAlive)
        {
            FailTest("object not alive when still referenced");
            return wr;
        }
        o = null;
        if (!wr.IsAlive)
        {
            FailTest("object not alive when not collected");
            return wr;
        }
        return wr;
    }

    private static void TestFinalization()
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CreateFinalizableObject(int keepAliveCount, bool gcOnFinalization = false, bool waitOnFinalization = false)
        {
            new ClassWithFinalizer()
            {
                KeepAliveCount = keepAliveCount,
                TriggerCollectionOnFinalization = gcOnFinalization,
                WaitOnFinalization = waitOnFinalization
            };
        }

        StartTest("Finalization");
        int expectedTotalFinalizationCount = 0;

        CreateFinalizableObject(0);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        expectedTotalFinalizationCount++;
        if (ClassWithFinalizer.TotalFinalizationCount != expectedTotalFinalizationCount)
        {
            FailTest("Object was not finalized");
            return;
        }

        int keepAliveCount = 3;
        CreateFinalizableObject(keepAliveCount);
        expectedTotalFinalizationCount += keepAliveCount + 1;
        for (int i = 0; i < keepAliveCount + 1; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        if (ClassWithFinalizer.TotalFinalizationCount != expectedTotalFinalizationCount)
        {
            FailTest($"Object was not resurrected enough times (e: {expectedTotalFinalizationCount} / a: {ClassWithFinalizer.TotalFinalizationCount})");
            return;
        }

        CreateFinalizableObject(0, gcOnFinalization: true);
        CreateFinalizableObject(0, gcOnFinalization: true);
        CreateFinalizableObject(0, gcOnFinalization: true);
        expectedTotalFinalizationCount += 3;
        GC.Collect();
        GC.WaitForPendingFinalizers();

        CreateFinalizableObject(0, waitOnFinalization: true);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        expectedTotalFinalizationCount += 1;

        if (ClassWithFinalizer.TotalFinalizationCount != expectedTotalFinalizationCount)
        {
            FailTest("Objects triggering recursive GCs did not finalize properly");
            return;
        }

        PassTest();
    }

    class ClassWithFinalizer
    {
        public static int TotalFinalizationCount;

        public int KeepAliveCount;
        public bool TriggerCollectionOnFinalization;
        public bool WaitOnFinalization;

        ~ClassWithFinalizer()
        {
            TotalFinalizationCount++;

            if (KeepAliveCount > 0)
            {
                GC.ReRegisterForFinalize(this);
                KeepAliveCount--;
            }
            if (TriggerCollectionOnFinalization)
            {
                GC.Collect();
            }
            if (WaitOnFinalization)
            {
                GC.WaitForPendingFinalizers();
            }
        }
    }

    private static void TestBox()
    {
        StartTest("Box int test");
        object o = (Int32)1;
        string virtCallRes = o.ToString();
        PrintLine(virtCallRes);
        var i = (int)o;
        PrintLine("i");
        PrintLine(i.ToString());
        EndTest(virtCallRes == "1");
    }

    private static int StaticDelegateTarget()
    {
        return 7;
    }

    private static int Add(int a, int b)
    {
        return a + b;
    }

    private static uint Not(uint a)
    {
        return ~a;
    }

    private static int Neg(int a)
    {
        return -a;
    }

    private static int ShiftLeft(int a, int b)
    {
        return a << b;
    }

    private static int ShiftRight(int a, int b)
    {
        return a >> b;
    }

    private static uint UnsignedShift(uint a, int b)
    {
        return a >> b;
    }

    private static int SwitchOp(int a, int b, int mode)
    {
        switch (mode)
        {
            case 0:
                return a + b;
            case 1:
                return a * b;
            case 2:
                return a / b;
            case 3:
                return a - b;
            default:
                return 0;
        }
    }

    private static IntPtr NewobjValueType()
    {
        return new IntPtr(3);
    }

    private static void TestShadowStackAlignment()
    {
        StartTest("Shadow stack alignment test");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool TestNormalAlignment()
        {
            object alignedObject = default;
            Volatile.Write(ref alignedObject, new object());
            return ((nuint)(void*)&alignedObject & (nuint)(sizeof(nint) - 1)) == 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool TestNormalAlignmentTwo()
        {
            object paddingObject = default;
            Volatile.Write(ref paddingObject, new object());
            return TestNormalAlignment();
        }

        if (!TestNormalAlignment() || !TestNormalAlignmentTwo())
        {
            FailTest("Shadow stack wasn't aligned to pointer size");
            return;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool TestDoubleAlignment()
        {
            StructWithObjectAndDouble doubleAlignedStruct = default;
            Volatile.Write(ref doubleAlignedStruct.DoubleField, 1);
            return ((nuint)(void*)&doubleAlignedStruct & (nuint)(sizeof(double) - 1)) == 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool TestDoubleAlignmentTwo()
        {
            object paddingObject = default;
            Volatile.Write(ref paddingObject, new object());
            return TestDoubleAlignment();
        }

        if (!TestDoubleAlignment() || !TestDoubleAlignmentTwo())
        {
            FailTest("Shadow stack wasn't aligned to 8");
            return;
        }

        PassTest();
    }

    private unsafe static void StackallocTest()
    {
        StartTest("Stackalloc test");
        int* intSpan = stackalloc int[2];
        intSpan[0] = 3;
        intSpan[1] = 7;

        EndTest(intSpan[0] == 3 && intSpan[1] == 7);
    }

    private static void IntToStringTest()
    {
        StartTest("Int to String Test: Ok if says 42");
        string intString = 42.ToString();
        PrintLine(intString);
        EndTest(intString == "42");
    }

    private static void ConvUTest()
    {
        StartTest("Implicit casting using ConvU");
        byte alpha = 0xFF;
        float f = alpha / 255f;
        if (f != 1f)
        {
            FailTest("Expected 1f but didn't get it"); // TODO: float.ToString() is failing in DiyFP
        }

        byte msbByte = 0xff;
        nuint nativeUnsignedFromByte = msbByte;
        if (nativeUnsignedFromByte != 0xff)
        {
            FailTest($"Expected 0xff but got {nativeUnsignedFromByte}");
            return;
        }

        ushort msbUshort = 0x8000;
        nuint nativeUnsignedFromUshort = msbUshort;
        EndTest(nativeUnsignedFromUshort == 0x8000, $"Expected 0x8000 but got {nativeUnsignedFromUshort}");
    }

    private static void CastByteForIndex()
    {
        StartTest("Implicit casting of byte for an index");
        int[] someInts = new int[0xff + 1];
        byte byteIndex = 0xFF;
        someInts[byteIndex] = 123;
        EndTest(someInts[0xff] == 123, "Expected 123 at index 0xff but didn't get it");
    }

    private unsafe static void ldindTest()
    {
        StartTest("ldind test");
        var ldindTarget = new TwoByteStr { first = byte.MaxValue, second = byte.MinValue };
        var ldindField = &ldindTarget.first;
        if ((*ldindField) == byte.MaxValue)
        {
            ldindTarget.second = byte.MaxValue;
            *ldindField = byte.MinValue;
            //ensure there isnt any overwrite of nearby fields
            if (ldindTarget.first == byte.MinValue && ldindTarget.second == byte.MaxValue)
            {
                PassTest();
            }
            else if (ldindTarget.first != byte.MinValue)
            {
                FailTest("didnt update target.");
            }
            else
            {
                FailTest("overwrote data");
            }
        }
        else
        {
            uint ldindFieldValue = *ldindField;
            FailTest(ldindFieldValue.ToString());
        }
    }

    private static void InterfaceDispatchTest()
    {
        StartTest("Struct interface test");
        ItfStruct itfStruct = new ItfStruct();
        EndTest(ItfCaller(itfStruct) == 4);

        ClassWithSealedVTable classWithSealedVTable = new ClassWithSealedVTable();
        StartTest("Interface dispatch with sealed vtable test");
        EndTest(CallItf(classWithSealedVTable) == 37);
    }

    // Calls the ITestItf interface via a generic to ensure the concrete type is known and
    // an interface call is generated instead of a virtual or direct call
    private static int ItfCaller<T>(T obj) where T : ITestItf
    {
        return obj.GetValue();
    }

    private static int CallItf(ISomeItf asItf)
    {
        return asItf.GetValue();
    }

    private static void StaticCtorTest()
    {
        BeforeFieldInitTest.Nop();
        if (StaticsInited.BeforeFieldInitInited)
        {
            PrintLine("BeforeFieldInitType inited too early");
        }
        else
        {
            StartTest("BeforeFieldInit test");
            int x = BeforeFieldInitTest.TestField;
            EndTest(StaticsInited.BeforeFieldInitInited, "cctor not run");
        }

        StartTest("NonBeforeFieldInit test");
        NonBeforeFieldInitTest.Nop();
        EndTest(StaticsInited.NonBeforeFieldInitInited, "cctor not run");
    }

    private static void TestConstrainedClassCalls()
    {
        string s = "utf-8";

        StartTest("Direct ToString test");
        string stringDirectToString = s.ToString();
        if (s.Equals(stringDirectToString))
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintString("Returned string:\"");
            PrintString(stringDirectToString);
            PrintLine("\"");
        }

        // Generic calls on methods not defined on object
        uint dataFromBase = GenericGetData<MyBase>(new MyBase(11));
        StartTest("Generic call to base class test");
        EndTest(dataFromBase == 11);

        uint dataFromUnsealed = GenericGetData<UnsealedDerived>(new UnsealedDerived(13));
        StartTest("Generic call to unsealed derived class test");
        EndTest(dataFromUnsealed == 26);

        uint dataFromSealed = GenericGetData<SealedDerived>(new SealedDerived(15));
        StartTest("Generic call to sealed derived class test");
        EndTest(dataFromSealed == 45);

        uint dataFromUnsealedAsBase = GenericGetData<MyBase>(new UnsealedDerived(17));
        StartTest("Generic call to unsealed derived class as base test");
        EndTest(dataFromUnsealedAsBase == 34);

        uint dataFromSealedAsBase = GenericGetData<MyBase>(new SealedDerived(19));
        StartTest("Generic call to sealed derived class as base test");
        EndTest(dataFromSealedAsBase == 57);

        // Generic calls to methods defined on object
        uint hashCodeOfSealedViaGeneric = (uint)GenericGetHashCode<MySealedClass>(new MySealedClass(37));
        StartTest("Generic GetHashCode for sealed class test");
        EndTest(hashCodeOfSealedViaGeneric == 74);

        uint hashCodeOfUnsealedViaGeneric = (uint)GenericGetHashCode<MyUnsealedClass>(new MyUnsealedClass(41));
        StartTest("Generic GetHashCode for unsealed class test");
        EndTest(hashCodeOfUnsealedViaGeneric == 82);
    }

    static void TestConstrainedStructCalls()
    {
        StartTest("Constrained struct callvirt test");
        EndTest("Program+ConstrainedStructTest" == new ConstrainedStructTest().ThisToString());
    }

    struct ConstrainedStructTest
    {
        internal string ThisToString()
        {
            return this.ToString();
        }
    }


    static uint GenericGetData<T>(T obj) where T : MyBase
    {
        return obj.GetData();
    }

    static int GenericGetHashCode<T>(T obj)
    {
        return obj.GetHashCode();
    }

    private static void TestArrayItfDispatch()
    {
        ICollection<int> arrayItfDispatchTest = new int[37];
        StartTest("Array interface dispatch test");
        EndTest(arrayItfDispatchTest.Count == 37,
            "Failed.  asm.js (WASM=1) known to fail due to alignment problem, although this problem sometimes means we don't even get this far and fails with an invalid function pointer.");
    }

    class ClassForLdvirtftnTest : ICloneable
    {
        public override string ToString() => "ClassForLdvirtftnTest";

        public object Clone() => "ClassForLdvirtftnTestClone";
    }

    private static void TestLdvirtftn()
    {
        ClassForLdvirtftnTest obj = new();

        StartTest("Testing ldvirtftn (vtable)");
        EndTest(ILHelpers.ILHelpersTest.TestLdvirtftnVTable(obj));

        StartTest("Testing ldvirtftn (interface)");
        EndTest(ILHelpers.ILHelpersTest.TestLdvirtftnInterface(obj));
    }

    private static void TestValueTypeElementIndexing()
    {
        var chars = new[] { 'i', 'p', 's', 'u', 'm' };
        StartTest("Value type element indexing: ");
        EndTest(chars[0] == 'i' && chars[1] == 'p' && chars[2] == 's' && chars[3] == 'u' && chars[4] == 'm');
    }

    private static void FloatDoubleTest()
    {
        StartTest("(double) cast test");
        int intToCast = 1;
        double castedDouble = (double)intToCast;
        if (castedDouble == 1d)
        {
            PassTest();
        }
        else
        {
            var toInt = (int)castedDouble;
            //            PrintLine("expected 1m, but was " + castedDouble.ToString());  // double.ToString is not compiling at the time of writing, but this would be better output
            FailTest("Back to int on next line");
            PrintLine(toInt.ToString());
        }

        StartTest("different width float comparisons");
        EndTest(1f < 2d && 1d < 2f && 1f == 1d);

        StartTest("double precision comparison");
        // floats are 7 digits precision, so check some double more precise to make sure there is no loss occurring through some inadvertent cast to float
        EndTest(10.23456789d != 10.234567891d);

        StartTest("float comparison");
        EndTest(12.34567f == 12.34567f && 12.34567f != 12.34568f);

        StartTest("Test comparison of float constant");
        var maxFloat = Single.MaxValue;
        EndTest(maxFloat == Single.MaxValue);

        StartTest("Test comparison of double constant");
        var maxDouble = Double.MaxValue;
        EndTest(maxDouble == Double.MaxValue);
    }

    private static bool callbackResult;
    private static void TestNativeCallback()
    {
        StartTest("Native callback test");
        CallMe(123);
        EndTest(callbackResult);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly(EntryPoint = "CallMe")]
    private static void _CallMe(int x)
    {
        if (x == 123)
        {
            callbackResult = true;
        }
    }

    // All "*" imports are implicitly DirectPInvoke so name a module.
    [DllImport("NonExistentModule", EntryPoint = "NonExistentMethod")]
    private static extern void LazyMethod([MarshalAs(UnmanagedType.LPStr)] string str);

    private static void LazyDllImportThrows()
    {
        StartTest("Lazy DllImport fails");
        try
        {
            LazyMethod("some string");
            FailTest("Lazy linked DllImport did not throw");
        }
        catch (PlatformNotSupportedException)
        {
            PassTest();
        }
    }

    [DllImport("xyz", EntryPoint = "SimpleDirectPInvokeTestFunc")]
    private static extern int SimpleDirectPInvokeTest(int x);

    private static void TestDirectPInvoke()
    {
        StartTest("DirectPInvoke test");
        EndTest(SimpleDirectPInvokeTest(234) == 234);
    }

    [DllImport("StaticModule1", EntryPoint = "CommonStaticFunctionName")]
    private static extern int CallAbiCompatFunctionWithInt(int arg);

    [DllImport("StaticModule2", EntryPoint = "CommonStaticFunctionName")]
    private static extern uint CallAbiCompatFunctionWitUint(uint arg);

    private static void TestStaticAbiCompatibleSignatures()
    {
        StartTest("Static imports with ABI compatible signatures");
        EndTest(CallAbiCompatFunctionWithInt(456) == 456 && CallAbiCompatFunctionWitUint(789) == 789);
    }

#if !CODEGEN_WASI // Easier to test with Javascript/Emscripten
    private static void TestNamedModuleCall()
    {
        StartTest("Wasm import from named module test");
        EndTest(CallFunctionInModule(456) == 456);
    }

    private static void TestNamedModuleCallWithoutEntryPoint()
    {
        StartTest("Wasm import from named module test");
        EndTest(ModuleFunc(77) == 77);
    }

    [DllImport("ModuleName", EntryPoint = "ModuleFunc"), WasmImportLinkage]
    private static extern int CallFunctionInModule(int x);

    [DllImport("ModuleName"), WasmImportLinkage]
    private static extern int ModuleFunc(int x);

    [DllImport("ModuleName", EntryPoint = "DupImportTest"), WasmImportLinkage]
    private static extern int WasmImportFuncDup1(int arg);

    [DllImport("ModuleName", EntryPoint = "DupImportTest"), WasmImportLinkage]
    private static extern int WasmImportFuncDup2();

    private static void TestEntryPointNotFoundForWasmImport()
    {
        try
        {
            WasmImportFuncDup1(0);
        }
        catch (EntryPointNotFoundException)
        {
            try
            {
                WasmImportFuncDup2();
            }
            catch (EntryPointNotFoundException)
            {
                PassTest();
                return;
            }
        }

        FailTest("EntryPointNotFoundException not thrown");
    }

    [DllImport("ModuleName1", EntryPoint = "CommonFunctionName"), WasmImportLinkage]
    private static extern int CallFunctionInModule1(int arg);

    [DllImport("ModuleName2", EntryPoint = "CommonFunctionName"), WasmImportLinkage]
    private static extern int CallFunctionInModule2(int arg);

    private static void TestSameFunctionNameInDifferentModules()
    {
        StartTest("Wasm import same function name from different modules test");
        EndTest(CallFunctionInModule1(456) == 456 && CallFunctionInModule2(789) == 790);
    }

    [DllImport("ModuleName1", EntryPoint = "CommonWasmImportFunctionName"), WasmImportLinkage]
    private static extern int CallWasmImportAbiCompatFunctionWithInt(int arg);

    [DllImport("ModuleName1", EntryPoint = "CommonWasmImportFunctionName"), WasmImportLinkage]
    private static extern uint CallWasmImportAbiCompatFunctionWitUint(uint arg);

    private static void TestWasmImportAbiCompatibleSignatures()
    {
        StartTest("Wasm imports with ABI compatible signatures");
        EndTest(CallWasmImportAbiCompatFunctionWithInt(456) == 456 && CallWasmImportAbiCompatFunctionWitUint(789) == 789);
    }

    [DllImport("StaticModule1", EntryPoint = "CommonStaticFunctionName")]
    private static extern int CommonFunctionNameInModule1(int arg);

    [DllImport("StaticModule2", EntryPoint = "CommonStaticFunctionName")]
    private static extern int CommonFunctionNameInModule2(int arg);

    private static void TestStaticPInvokeOverloadedInDifferentModules()
    {
        StartTest("Static PInvoke of overloaded function in different modules test");
        EndTest(CommonFunctionNameInModule1(12) == 12 && CommonFunctionNameInModule2(34) == 34);
    }
#endif

    [System.Runtime.InteropServices.DllImport("*")]
    private static extern void CallMe(int x);

    [System.Runtime.InteropServices.DllImport("*")]
    private static extern int GetNativeFunctionToCall();

    [System.Runtime.InteropServices.DllImport("*", EntryPoint = "NativeIntToDouble")]
    private static extern double NativeIntToDoubleRight(int a);

    [System.Runtime.InteropServices.DllImport("*", EntryPoint = "NativeIntToDouble")]
    private static extern int NativeIntToDoubleWrong(double a);

    [System.Runtime.InteropServices.DllImport("*", EntryPoint = "NativeIntToDouble")]
    private static extern void NativeIntToDoubleWrongAnother();

    [System.Runtime.InteropServices.DllImport("*")]
    private static extern int GetMemCpyLength();

    [System.Runtime.InteropServices.DllImport("*", EntryPoint = "memcpy")]
    private static extern void* MemCpyRight(void* dst, void* src, nuint length);

    [System.Runtime.InteropServices.DllImport("*", EntryPoint = "memcpy")]
    private static extern void MemCpyWrong(void* dst, void* src);

    [System.Runtime.InteropServices.DllImport("*", EntryPoint = "memcpy")]
    private static extern nuint MemCpyVariant(nuint dst, nuint src, nuint length);

    [System.Runtime.InteropServices.DllImport("*", EntryPoint = "memset")]
    private static extern void* MemSetRight(void* dst, int value, nuint length);

    [System.Runtime.InteropServices.DllImport("*", EntryPoint = "memset")]
    private static extern void MemSetWrong(void* dst, int value);

    [System.Runtime.InteropServices.DllImport("*", EntryPoint = "memset")]
    private static extern void MemSetWrongAnother();

    [System.Runtime.InteropServices.DllImport("*", EntryPoint = "memset")]
    private static extern nuint MemSetVariant(nuint dst, int value, nuint length);

    private static void TestNativeCallsWithMismatchedSignatures()
    {
        StartTest("Native calls with mismatched signatures test");

        int whichFunc = GetNativeFunctionToCall();
        double intToDoubleResult = 0;
        switch (whichFunc)
        {
            case 0:
                intToDoubleResult = NativeIntToDoubleWrong(10);
                break;
            case 1:
                intToDoubleResult = NativeIntToDoubleRight(20);
                break;
            default:
                NativeIntToDoubleWrongAnother();
                break;
        }

        if (intToDoubleResult != 20)
        {
            FailTest("NativeToIntDouble did not return the expected value");
            return;
        }

        // "memcpy/memset" are among the so-called "LLVM libcalls". These require special treatment.
        //
        const int MemSetValue = 10;
        int length = GetMemCpyLength();
        byte* src = stackalloc byte[length];
        byte* dst = stackalloc byte[length];

        void FillSrc()
        {
            for (int i = 0; i < length; i++)
            {
                src[i] = (byte)i;
            }
        }
        bool CheckDst(int? value = null)
        {
            for (int i = 0; i < length; i++)
            {
                if (value != null)
                {
                    if (dst[i] != value)
                    {
                        return false;
                    }
                }
                else
                {
                    if (dst[i] != (byte)i)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        if (length != 255)
        {
            MemCpyWrong(dst, src);
            MemSetWrong(dst, MemSetValue);
            MemSetWrongAnother();
        }

        FillSrc();
        MemCpyRight(dst, src, (nuint)length);
        if (!CheckDst())
        {
            FailTest("memcpy did not copy the bytes");
            return;
        }
        MemSetRight(dst, MemSetValue, (nuint)length);
        if (!CheckDst(MemSetValue))
        {
            FailTest("memset did not set the bytes");
            return;
        }

        MemCpyVariant((nuint)dst, (nuint)src, (nuint)length);
        if (!CheckDst())
        {
            FailTest("memcpy variant did not copy the bytes");
            return;
        }
        MemSetVariant((nuint)dst, MemSetValue, (nuint)length);
        if (!CheckDst(MemSetValue))
        {
            FailTest("memset variant did not set the bytes");
            return;
        }

        PassTest();
    }

    private static void TestMetaData()
    {
        StartTest("type == null.  Simple class metadata test");
        var typeGetType = Type.GetType("System.Char, System.Private.CoreLib");
        if (typeGetType == null)
        {
            FailTest("type == null.  Simple class metadata test");
        }
        else
        {
            if (typeGetType.FullName != "System.Char")
            {
                FailTest("type != System.Char.  Simple class metadata test");
            }
            else PassTest();
        }

        StartTest("Simple struct metadata test (typeof(Char))");
        var typeofChar = typeof(Char);
        if (typeofChar == null)
        {
            FailTest("type == null.  Simple struct metadata test");
        }
        else
        {
            if (typeofChar.FullName != "System.Char")
            {
                FailTest("type != System.Char.  Simple struct metadata test");
            }
            else PassTest();
        }

        var gentT = new Gen<int>();
        var genParamType = gentT.TestTypeOf();
        StartTest("type of generic parameter");
        if (genParamType.FullName != "System.Int32")
        {
            FailTest("expected System.Int32 but was " + genParamType.FullName);
        }
        else
        {
            PassTest();
        }

        var arrayType = typeof(object[]);
        StartTest("type of array");
        if (arrayType.FullName != "System.Object[]")
        {
            FailTest("expected System.Object[] but was " + arrayType.FullName);
        }
        else
        {
            PassTest();
        }

        var genericType = typeof(List<object>);
        StartTest("type of generic");
        if (genericType.FullName.Substring(0, genericType.FullName.LastIndexOf(",")) != "System.Collections.Generic.List`1[[System.Object, System.Private.CoreLib, Version=9.0.0.0, Culture=neutral")
        {
            FailTest("expected System.Collections.Generic.List`1[[System.Object, System.Private.CoreLib, Version=9.0.0.0, Culture=neutral  but was " + genericType.FullName);
        }
        else
        {
            PassTest();
        }

        StartTest("Type GetFields length");
        var x = new ClassForMetaTests();
        var s = x.StringField;
        var i = x.IntField;
        var classForMetaTestsType = typeof(ClassForMetaTests);
        FieldInfo[] fields = classForMetaTestsType.GetFields();
        PrintLine("Fields Length");
        PrintLine(fields.Length.ToString());
        EndTest(fields.Length == 4);

        StartTest("Type get string field via reflection");
        var stringFieldInfo = classForMetaTestsType.GetField("StringField");
        EndTest((string)stringFieldInfo.GetValue(x) == s);

        StartTest("Type get int field via reflection");
        var intFieldInfo = classForMetaTestsType.GetField("IntField");
        EndTest((int)intFieldInfo.GetValue(x) == i);

        StartTest("Type get static int field via reflection");
        var staticIntFieldInfo = classForMetaTestsType.GetField("StaticIntField");
        EndTest((int)staticIntFieldInfo.GetValue(x) == 23);

        StartTest("Type set string field via reflection");
        stringFieldInfo.SetValue(x, "bcd");
        EndTest(x.StringField == "bcd");

        StartTest("Type set int field via reflection");
        intFieldInfo.SetValue(x, 456);
        EndTest(x.IntField == 456);

        StartTest("Type set static int field via reflection");
        staticIntFieldInfo.SetValue(x, 987);
        EndTest(ClassForMetaTests.StaticIntField == 987);

        StartTest("Type set static long field via reflection");
        var staticLongFieldInfo = classForMetaTestsType.GetField("StaticLongField");
        staticLongFieldInfo.SetValue(x, 0x11111111);
        EndTest(ClassForMetaTests.StaticLongField == 0x11111111L);

        var st = new StructForMetaTests();
        st.StringField = "xyz";
        var fieldStructType = typeof(StructForMetaTests);
        var structStringFieldInfo = fieldStructType.GetField("StringField");
        StartTest("Struct get string field via reflection");
        EndTest((string)structStringFieldInfo.GetValue(st) == "xyz");

        StartTest("Class get+invoke ctor via reflection");
        var ctor = classForMetaTestsType.GetConstructor(new Type[0]);
        ClassForMetaTests instance = (ClassForMetaTests)ctor.Invoke(null);
        EndTest(instance.IntField == 12);

        instance.ReturnTrueIf1(0); // force method output
        instance.ReturnTrueIf1AndThis(0, null); // force method output
        ClassForMetaTests.ReturnsParam(null); // force method output

        NewMethod(classForMetaTestsType, instance);

        StartTest("Class get+invoke static method with ref param via reflection");
        var staticMtd = classForMetaTestsType.GetMethod("ReturnsParam");
        var retVal = (ClassForMetaTests)staticMtd.Invoke(null, new object[] { instance });
        EndTest(Object.ReferenceEquals(retVal, instance));
    }

    private static void NewMethod([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type classForMetaTestsType, ClassForMetaTests instance)
    {
        StartTest("Class get+invoke simple method via reflection");
        var mtd = classForMetaTestsType.GetMethod("ReturnTrueIf1");
        bool shouldBeTrue = (bool)mtd.Invoke(instance, new object[] { 1 });
        bool shouldBeFalse = (bool)mtd.Invoke(instance, new object[] { 2 });
        EndTest(shouldBeTrue && !shouldBeFalse);

        StartTest("Class get+invoke method with ref param via reflection");
        var mtdWith2Params = classForMetaTestsType.GetMethod("ReturnTrueIf1AndThis");
        shouldBeTrue = (bool)mtdWith2Params.Invoke(instance, new object[] { 1, instance });
        shouldBeFalse = (bool)mtdWith2Params.Invoke(instance, new object[] { 1, new ClassForMetaTests() });
        EndTest(shouldBeTrue && !shouldBeFalse);

    }

    public class ClassForMetaTests
    {
        // used via reflection
#pragma warning disable 0169
        public int IntField;
        public string StringField;
#pragma warning restore 0169
        public static int StaticIntField;
        public static long StaticLongField;

        public ClassForMetaTests()
        {
            StringField = "ab";
            IntField = 12;
            StaticIntField = 23;
            StaticLongField = 0x22222222;
        }

        public bool ReturnTrueIf1(int i)
        {
            return i == 1;
        }

        public bool ReturnTrueIf1AndThis(int i, object anInstance)
        {
            return i == 1 && object.ReferenceEquals(this, anInstance);
        }

        public static object ReturnsParam(object p1)
        {
            return p1;
        }
    }

    public struct StructForMetaTests
    {
        public string StringField;
    }

    public interface IHasTwoFields
    {
        int GetIntField();
        long GetLongField();
    }

    public struct StructWintIntf : IHasTwoFields
    {
        public int IntField;
        public long LongField;

        public int GetIntField()
        {
            return IntField;
        }

        public long GetLongField()
        {
            return LongField;
        }
    }

    private static void TestGvmCallInIf<T>(GenBase<T> g, T p)
    {
        var i = 1;
        if (i == 1)
        {
            g.GMethod1(p, p);
        }
    }

    class GenDerived<A> : GenBase<A>
    {
        public override string GMethod1<T>(T t1, T t2) { return "GenDerived<" + typeof(A) + ">.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
    }

    private static void TestStoreFromGenericMethod()
    {
        StartTest("TestStoreFromGenericMethod");
        var values = new string[1];
        // testing that the generic return value type from the function can be stored in a concrete type
        values = values.AsSpan(0, 1).ToArray();
        EndTest(values.Length == 1);
    }

    private static void TestCallToGenericInterfaceMethod()
    {
        StartTest("Call generic method on interface test");

        TestGenItf implInt = new TestGenItf();
        implInt.Log<object>(new object());
        EndTest(true);
    }

    private static void TestConstrainedValueTypeCallVirt()
    {
        StartTest("Call constrained callvirt");
        //TODO: create simpler test that doesn't need Dictionary<>/KVP<>/Span
        var dict = new Dictionary<KeyValuePair<string, string>, string>();
        var notContainsKey = dict.ContainsKey(new KeyValuePair<string, string>());

        EndTest(!notContainsKey);
    }

    private static void TestBoxToGenericTypeFromDirectMethod()
    {
        StartTest("Callvirt on generic struct boxing to looked up generic type");

        new GetHashCodeCaller<GenStruct<string>, string>().CallValueTypeGetHashCodeFromGeneric(new GenStruct<string>(""));

        PassTest();
    }

    public struct GenStruct<TKey>
    {
        private TKey key;

        public GenStruct(TKey key)
        {
            this.key = key;
        }
    }

    public struct GenStruct2<TKey, T2>
    {
        private TKey key;
        T2 field2;

        public GenStruct2(TKey key, T2 v)
        {
            this.key = key;
            this.field2 = v;
        }
    }

    private static void TestGenericStructHandling()
    {
        StartTest("Casting of generic structs on return and in call params");

        // test return  type is cast
        ActualStructCallParam(new string[0]);

        // test call param is cast
        GenStructCallParam(new GenStructWithImplicitOp<string>());

        // replicate compilation error with https://github.com/dotnet/corert/blob/66fbcd492fbc08db4f472e7e8fa368cb523b38d4/src/System.Private.CoreLib/shared/System/Array.cs#L1482
        GenStructCallParam(CreateGenStructWithImplicitOp<string>(new[] { "" }));

        // replicate compilation error with https://github.com/dotnet/corefx/blob/e99ec129cfd594d53f4390bf97d1d736cff6f860/src/System.Collections.Immutable/src/System/Collections/Immutable/SortedInt32KeyNode.cs#L561
        new GenClassUsingFieldOfInnerStruct<GenClassWithInnerStruct<string>.GenInterfaceOverGenStructStruct>(
            new GenClassWithInnerStruct<string>.GenInterfaceOverGenStructStruct(), null).Create();

        // replicate compilation error with https://github.com/dotnet/runtime/blob/b57a099c1773eeb52d3c663211e275131b4b7938/src/libraries/System.Net.Primitives/src/System/Net/CredentialCache.cs#L328
        new GenClassWithInnerStruct<string>().SetField("");

        PassTest();
    }

    private static GenStructWithImplicitOp<T> CreateGenStructWithImplicitOp<T>(T[] v)
    {
        return new GenStructWithImplicitOp<T>(v);
    }

    private static GenStruct2<T, T2> CreateGenStruct2<T, T2>(T k, T2 v)
    {
        return new GenStruct2<T, T2>(k, v);
    }

    public class GenClassWithInnerStruct<TKey>
    {
        private GenStruct2<TKey, string> structField;

        public void SetField(TKey v)
        {
            structField = Program.CreateGenStruct2(v, "");
        }

        internal readonly struct GenInterfaceOverGenStructStruct
        {
            // 2 fields to avoid struct collapsing to an i32
            private readonly TKey _firstValue;
            private readonly TKey _otherValue;

            private GenInterfaceOverGenStructStruct(TKey firstElement)
            {
                _firstValue = firstElement;
                _otherValue = firstElement;
            }
        }
    }

    public class GenClassUsingFieldOfInnerStruct<T>
    {
        private readonly T _value;
        private GenClassUsingFieldOfInnerStruct<T> _left;

        public GenClassUsingFieldOfInnerStruct(T v, GenClassUsingFieldOfInnerStruct<T> left)
        {
            _value = v;
            _left = left;
        }

        public GenClassUsingFieldOfInnerStruct<T> Create(GenClassUsingFieldOfInnerStruct<T> left = null)
        {
            // some logic to get _value in a temp 
            return new GenClassUsingFieldOfInnerStruct<T>(_value, left ?? _left);
        }
    }

    private static void TestGenericCallInFinally()
    {
        StartTest("calling generic method requiring context from finally block");
        if (GenRequiresContext<string>.Called)
        {
            FailTest("static bool defaulted to true");
        }
        EndTest(CallGenericInFinally<string>());
    }

    private static bool CallGenericInFinally<T>()
    {
        try
        {
            // do nothing
        }
        finally
        {
            GenRequiresContext<T>.Dispose();
        }
        return GenRequiresContext<T>.Called;
    }

    public class GenRequiresContext<T>
    {
        internal static bool Called;

        public static void Dispose()
        {
            Called = true;
        }
    }

    private static void ActualStructCallParam(GenStructWithImplicitOp<string> gs)
    {
    }

    private static void GenStructCallParam<T>(GenStructWithImplicitOp<T> gs)
    {
    }

    public ref struct GenStructWithImplicitOp<TKey>
    {
        private int length;
        private int length2; // just one int field will not create an LLVM struct type, so put another field

        public GenStructWithImplicitOp(TKey[] key)
        {
            length = key.Length;
            length2 = length;
        }

        public static implicit operator GenStructWithImplicitOp<TKey>(TKey[] array) => new GenStructWithImplicitOp<TKey>(array);
    }

    public class GetHashCodeCaller<TKey, TValue>
    {
        public void CallValueTypeGetHashCodeFromGeneric(TKey k)
        {
            k.GetHashCode();
        }
    }

    public interface ITestGenItf
    {
        bool Log<TState>(TState state);
    }

    public class TestGenItf : ITestGenItf
    {
        public bool Log<TState>(TState state)
        {
            return true;
        }
    }

    private static void TestArgsWithMixedTypesAndExceptionRegions()
    {
        new MixedArgFuncClass().MixedArgFunc(1, null, 2, null);
    }

    class MixedArgFuncClass
    {
        public void MixedArgFunc(int firstInt, object shadowStackArg, int secondInt, object secondShadowStackArg)
        {
            Program.StartTest("MixedParamFuncWithExceptionRegions does not overwrite args");
            bool ok = true;
            int p1 = firstInt;
            try // add a try/catch to get _exceptionRegions.Length > 0 and copy stack args to shadow stack
            {
                if (shadowStackArg != null)
                {
                    FailTest("shadowStackArg != null");
                    ok = false;
                }
            }
            catch (Exception)
            {
                throw;
            }
            if (p1 != 1)
            {
                FailTest("p1 not 1, was ");
                PrintLine(p1.ToString());
                ok = false;
            }

            if (secondInt != 2)
            {
                FailTest("secondInt not 2, was ");
                PrintLine(secondInt.ToString());
                ok = false;
            }
            if (secondShadowStackArg != null)
            {
                FailTest("secondShadowStackArg != null");
                ok = false;
            }
            if (ok)
            {
                PassTest();
            }
        }
    }

    private static void TestThreadStaticsForSingleThread()
    {
        var firstClass = new ClassWithFourThreadStatics();
        int firstClassStatic = firstClass.GetStatic();
        StartTest("Static should be initialised");
        if (firstClassStatic == 2)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Was: " + firstClassStatic.ToString());
        }
        StartTest("Second class with same statics should be initialised");
        int secondClassStatic = new AnotherClassWithFourThreadStatics().GetStatic();
        if (secondClassStatic == 13)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Was: " + secondClassStatic.ToString());
        }

        StartTest("First class increment statics");
        firstClass.IncrementStatics();
        firstClassStatic = firstClass.GetStatic();
        if (firstClassStatic == 3)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Was: " + firstClassStatic.ToString());
        }

        StartTest("Second class should not be overwritten"); // catches a type of bug where beacuse the 2 types share the same number and types of ThreadStatics, the first class can end up overwriting the second
        secondClassStatic = new AnotherClassWithFourThreadStatics().GetStatic();
        if (secondClassStatic == 13)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Was: " + secondClassStatic.ToString());
        }

        StartTest("First class 2nd instance should share static");
        int secondInstanceOfFirstClassStatic = new ClassWithFourThreadStatics().GetStatic();
        if (secondInstanceOfFirstClassStatic == 3)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Was: " + secondInstanceOfFirstClassStatic.ToString());
        }
        Thread.Sleep(10);
    }

    private static void TestDispose()
    {
        StartTest("using calls Dispose");
        var disposable = new DisposableTest();
        using (disposable)
        {
        }
        EndTest(disposable.Count == 1);
    }

    private static void TestInitObjDouble()
    {
        StartTest("Init struct with double field test");
        StructWithDouble strt = new StructWithDouble();
        EndTest(strt.DoubleField == 0d);
    }

    private static unsafe void TestSByteExtend()
    {
        StartTest("SByte extend");
        sbyte s = -1;
        int x = (int)s;
        sbyte s2 = 1;
        int x2 = (int)s2;
        if (x == -1 && x2 == 1)
        {
            PassTest();
        }
        else
        {
            FailTest("Expected -1 and 1 but got " + x.ToString() + " and " + x2.ToString());
        }

        StartTest("SByte left shift");
        x = (int)(s << 1);
        if (x == -2)
        {
            PassTest();
        }
        else
        {
            FailTest("Expected -2 but got " + x.ToString());
        }

        sbyte minus1 = -1;
        StartTest("Negative SByte op");
        if ((s & minus1) == -1)
        {
            PassTest();
        }
        else
        {
            FailTest();
        }

        StartTest("Negative SByte br");
        if (s == -1) // this only creates the bne opcode, which it is testing, in Release mode.
        {
            PassTest();
        }
        else
        {
            FailTest();
        }
    }

    public static void TestSharedDelegate()
    {
        StartTest("Shared Delegate");
        var shouldBeFalse = SampleClassWithGenericDelegate.CallDelegate(new object[0]);
        var shouldBeTrue = SampleClassWithGenericDelegate.CallDelegate(new object[1]);
        EndTest(!shouldBeFalse && shouldBeTrue);
    }

    internal static void TestUlongUintMultiply()
    {
        StartTest("Test ulong/int multiplication");
        uint a = 0x80000000;
        uint b = 2;
        ulong f = ((ulong)a * b);
        EndTest(f == 0x100000000);
    }

    internal static void TestBoxSingle()
    {
        StartTest("Test box single");
        var fi = typeof(ClassWithFloat).GetField("F");
        fi.SetValue(null, 1.1f);
        EndTest(1.1f == ClassWithFloat.F);
    }

    static void TestInitializeArray()
    {
        StartTest("Test InitializeArray");

        bool[,] bools = new bool[2, 2] {
            {  true,                        true},
            {  false,                       true},
        };

        if (!(bools[0, 0] && bools[0, 1]
            && !bools[1, 0] && bools[0, 1]))
        {
            FailTest("bool initialisation failed");
        }

        double[,] doubles = new double[2, 3]
        {
            {1.0, 1.1, 1.2 },
            {2.0, 2.1, 2.2 },
        };

        if (!(doubles[0, 0] == 1.0 && doubles[0, 1] == 1.1 && doubles[0, 2] == 1.2
            && doubles[1, 0] == 2.0 && doubles[1, 1] == 2.1 && doubles[1, 2] == 2.2
            ))
        {
            FailTest("double initialisation failed");
        }

        PassTest();
    }

    static void TestImplicitUShortToUInt()
    {
        StartTest("test extend of shorts with MSB set");
        uint start;
        start = ReadUInt16();
        EndTest(start == 0x0000828f);
    }

    unsafe static void TestReverseDelegateInvoke()
    {
        // tests the try catch LLVM for reverse delegate invokes
        DelegateToCallFromUnmanaged del = (char* charPtr) =>
        {
            return true;
        };
        int i = 1;
        if (i == 0) // dont actually call it as it doesnt exist, just want the reverse delegate created & compiled
        {
            SomeExternalUmanagedFunction(del);
        }
    }

    static void TestInterlocked()
    {
        static int InterlockedAnd(ref int location, int value) => Interlocked.And(ref location, value);
        static int InterlockedOr(ref int location, int value) => Interlocked.Or(ref location, value);
        static int InterlockedAdd(ref int location, int value) => Interlocked.Add(ref location, value);
        static byte InterlockedExchangeByte(ref byte location, byte value) => Interlocked.Exchange(ref location, value);
        static short InterlockedExchangeInt16(ref short location, short value) => Interlocked.Exchange(ref location, value);
        static int InterlockedExchangeInt32(ref int location, int value) => Interlocked.Exchange(ref location, value);
        static object InterlockedExchangeObj(ref object location, object value) => Interlocked.Exchange(ref location, value);
        static byte InterlockedCompareExchangeByte(ref byte location, byte value, byte comparand) => Interlocked.CompareExchange(ref location, value, comparand);
        static short InterlockedCompareExchangeInt16(ref short location, short value, short comparand) => Interlocked.CompareExchange(ref location, value, comparand);
        static int InterlockedCompareExchangeInt32(ref int location, int value, int comparand) => Interlocked.CompareExchange(ref location, value, comparand);
        static object InterlockedCompareExchangeObj(ref object location, object value, object comparand) => Interlocked.CompareExchange(ref location, value, comparand);

        // Test statically direct (in the wrapper) calls to the atomics.
        TestInterlockedImpl(
            &InterlockedAnd,
            &InterlockedOr,
            &InterlockedAdd,
            &InterlockedExchangeByte,
            &InterlockedExchangeInt16,
            &InterlockedExchangeInt32,
            &InterlockedExchangeObj,
            &InterlockedCompareExchangeByte,
            &InterlockedCompareExchangeInt16,
            &InterlockedCompareExchangeInt32,
            &InterlockedCompareExchangeObj,
            "");

        // Test indirect calls to the atomics.
        TestInterlockedImpl(
            (delegate*<ref int, int, int>)&Interlocked.And,
            (delegate*<ref int, int, int>)&Interlocked.Or,
            (delegate*<ref int, int, int>)&Interlocked.Add,
            (delegate*<ref byte, byte, byte>)&Interlocked.Exchange,
            (delegate*<ref short, short, short>)&Interlocked.Exchange,
            (delegate*<ref int, int, int>)&Interlocked.Exchange,
            (delegate*<ref object, object, object>)&Interlocked.Exchange,
            (delegate*<ref byte, byte, byte, byte>)&Interlocked.CompareExchange,
            (delegate*<ref short, short, short, short>)&Interlocked.CompareExchange,
            (delegate*<ref int, int, int, int>)&Interlocked.CompareExchange,
            (delegate*<ref object, object, object, object>)&Interlocked.CompareExchange,
            " (indirect)");
    }

    static void TestInterlockedImpl(
        delegate*<ref int, int, int> interlockedAnd,
        delegate*<ref int, int, int> interlockedOr,
        delegate*<ref int, int, int> interlockedAdd,
        delegate*<ref byte, byte, byte> interlockedExchangeByte,
        delegate*<ref short, short, short> interlockedExchangeInt16,
        delegate*<ref int, int, int> interlockedExchangeInt32,
        delegate*<ref object, object, object> interlockedExchangeObj,
        delegate*<ref byte, byte, byte, byte> interlockedCompareExchangeByte,
        delegate*<ref short, short, short, short> interlockedCompareExchangeInt16,
        delegate*<ref int, int, int, int> interlockedCompareExchangeInt32,
        delegate*<ref object, object, object, object> interlockedCompareExchangeObj,
        string postfix)
    {
        const long LongLocationValue = 0x1010101010101010;
        long longLocation = LongLocationValue;
        byte* alignedLongAddress = (byte*)&longLocation;

        StartTest($"Test Interlocked.And" + postfix);
        {
            int initValue = 1;
            if (interlockedAnd(ref initValue, 0) != 1)
            {
                FailTest("Interlocked.And - old value");
                return;
            }
            if (initValue != 0)
            {
                FailTest("Interlocked.And - new value");
                return;
            }
            try
            {
                interlockedAnd(ref *(int*)null, 0);
                FailTest("Interlocked.And - null location");
                return;
            }
            catch (NullReferenceException) { }
            try
            {
                interlockedAnd(ref *(int*)(alignedLongAddress + 1), 0);
                FailTest("Interlocked.And - unaligned location");
                return;
            }
            catch (DataMisalignedException) { }
            if (longLocation != LongLocationValue)
            {
                FailTest("Interlocked.And - unaligned store observed");
                return;
            }
        }
        PassTest();

        StartTest("Test Interlocked.Or" + postfix);
        {
            int initValue = 0;
            if (interlockedOr(ref initValue, 1) != 0)
            {
                FailTest("Interlocked.Or - old value");
                return;
            }
            if (initValue != 1)
            {
                FailTest("Interlocked.Or - new value");
                return;
            }
            try
            {
                interlockedOr(ref *(int*)null, 0);
                FailTest("Interlocked.Or - null location");
                return;
            }
            catch (NullReferenceException) { }
            try
            {
                interlockedOr(ref *(int*)(alignedLongAddress + 1), 1);
                FailTest("Interlocked.Or - unaligned location");
                return;
            }
            catch (DataMisalignedException) { }
            if (longLocation != LongLocationValue)
            {
                FailTest("Interlocked.Or - unaligned store observed");
                return;
            }
        }
        PassTest();

        StartTest("Test Interlocked.Add" + postfix);
        {
            int initValue = 0;
            if (interlockedAdd(ref initValue, 1) != 1)
            {
                FailTest("Interlocked.Add - old value");
                return;
            }
            if (initValue != 1)
            {
                FailTest("Interlocked.Add - new value");
                return;
            }
            try
            {
                interlockedAdd(ref *(int*)null, 0);
                FailTest("Interlocked.Add - null location");
                return;
            }
            catch (NullReferenceException) { }
            try
            {
                interlockedAdd(ref *(int*)(alignedLongAddress + 1), 1);
                FailTest("Interlocked.Add - unaligned location");
                return;
            }
            catch (DataMisalignedException) { }
            if (longLocation != LongLocationValue)
            {
                FailTest("Interlocked.Add - unaligned store observed");
                return;
            }
        }
        PassTest();

        StartTest("Test Interlocked.Exchange<byte>" + postfix);
        {
            byte initValue = 0;
            if (interlockedExchangeByte(ref initValue, 1) != 0)
            {
                FailTest("Interlocked.Exchange<byte> - old value");
                return;
            }
            if (initValue != 1)
            {
                FailTest("Interlocked.Exchange<byte> - new value");
                return;
            }
            try
            {
                interlockedExchangeByte(ref *(byte*)null, 0);
                FailTest("Interlocked.Exchange<byte> - null location");
                return;
            }
            catch (NullReferenceException) { }
        }
        PassTest();

        StartTest("Test Interlocked.Exchange<int16>" + postfix);
        {
            short initValue = 0;
            if (interlockedExchangeInt16(ref initValue, 1) != 0)
            {
                FailTest("Interlocked.Exchange<int16> - old value");
                return;
            }
            if (initValue != 1)
            {
                FailTest("Interlocked.Exchange<int16> - new value");
                return;
            }
            try
            {
                interlockedExchangeInt16(ref *(short*)null, 0);
                FailTest("Interlocked.Exchange<int16> - null location");
                return;
            }
            catch (NullReferenceException) { }
            try
            {
                interlockedExchangeInt16(ref *(short*)(alignedLongAddress + 1), 0);
                FailTest("Interlocked.Exchange<int16> - unaligned location");
                return;
            }
            catch (DataMisalignedException) { }
            if (longLocation != LongLocationValue)
            {
                FailTest("Interlocked.Exchange<int16> - unaligned store observed");
                return;
            }
        }
        PassTest();

        StartTest("Test Interlocked.Exchange<int32>" + postfix);
        {
            int initValue = 0;
            if (interlockedExchangeInt32(ref initValue, 1) != 0)
            {
                FailTest("Interlocked.Exchange<int32> - old value");
                return;
            }
            if (initValue != 1)
            {
                FailTest("Interlocked.Exchange<int32> - new value");
                return;
            }
            try
            {
                interlockedExchangeInt32(ref *(int*)null, 0);
                FailTest("Interlocked.Exchange<int32> - null location");
                return;
            }
            catch (NullReferenceException) { }
            try
            {
                interlockedExchangeInt32(ref *(int*)(alignedLongAddress + 1), 0);
                FailTest("Interlocked.Exchange<int32> - unaligned location");
                return;
            }
            catch (DataMisalignedException) { }
            if (longLocation != LongLocationValue)
            {
                FailTest("Interlocked.Exchange<int32> - unaligned store observed");
                return;
            }
        }
        PassTest();

        StartTest("Test Interlocked.Exchange<object>" + postfix);
        {
            object initValue = null;
            object newValue = new object();
            if (interlockedExchangeObj(ref initValue, newValue) != null)
            {
                FailTest("Interlocked.Exchange<object> - old value");
                return;
            }
            if (initValue != newValue)
            {
                FailTest("Interlocked.Exchange<object> - new value");
                return;
            }
            try
            {
                interlockedExchangeObj(ref *(object*)null, null);
                FailTest("Interlocked.Exchange<object> - null location");
                return;
            }
            catch (NullReferenceException) { }
        }
        PassTest();

        StartTest("Test Interlocked.CompareExchange<byte>" + postfix);
        {
            byte initValue = 0;
            if (interlockedCompareExchangeByte(ref initValue, 1, 0) != 0)
            {
                FailTest("Interlocked.CompareExchange<byte> - old value");
                return;
            }
            if (initValue != 1)
            {
                FailTest("Interlocked.CompareExchange<byte> - new value");
                return;
            }
            try
            {
                interlockedCompareExchangeByte(ref *(byte*)null, 0, 0);
                FailTest("Interlocked.CompareExchange<byte> - null location");
                return;
            }
            catch (NullReferenceException) { }
        }
        PassTest();

        StartTest("Test Interlocked.CompareExchange<int16>" + postfix);
        {
            short initValue = 0;
            if (interlockedCompareExchangeInt16(ref initValue, 1, 0) != 0)
            {
                FailTest("Interlocked.CompareExchange<int16> - old value");
                return;
            }
            if (initValue != 1)
            {
                FailTest("Interlocked.CompareExchange<int16> - new value");
                return;
            }
            try
            {
                interlockedCompareExchangeInt16(ref *(short*)null, 0, 0);
                FailTest("Interlocked.CompareExchange<int16> - null location");
                return;
            }
            catch (NullReferenceException) { }
            try
            {
                interlockedCompareExchangeInt16(ref *(short*)(alignedLongAddress + 1), 0, 0);
                FailTest("Interlocked.CompareExchange<int16> - unaligned location");
                return;
            }
            catch (DataMisalignedException) { }
            if (longLocation != LongLocationValue)
            {
                FailTest("Interlocked.CompareExchange<int16> - unaligned store observed");
                return;
            }
        }
        PassTest();

        StartTest("Test Interlocked.CompareExchange<int32>" + postfix);
        {
            int initValue = 0;
            if (interlockedCompareExchangeInt32(ref initValue, 1, 0) != 0)
            {
                FailTest("Interlocked.CompareExchange<int32> - old value");
                return;
            }
            if (initValue != 1)
            {
                FailTest("Interlocked.CompareExchange<int32> - new value");
                return;
            }
            try
            {
                interlockedCompareExchangeInt32(ref *(int*)null, 0, 0);
                FailTest("Interlocked.CompareExchange<int32> - null location");
                return;
            }
            catch (NullReferenceException) { }
            try
            {
                interlockedCompareExchangeInt32(ref *(int*)(alignedLongAddress + 1), 0, 0);
                FailTest("Interlocked.CompareExchange<int32> - unaligned location");
                return;
            }
            catch (DataMisalignedException) { }
            if (longLocation != LongLocationValue)
            {
                FailTest("Interlocked.CompareExchange<int32> - unaligned store observed");
                return;
            }
        }
        PassTest();

        StartTest("Test Interlocked.CompareExchange<object>" + postfix);
        {
            object initValue = null;
            object newValue = new object();
            if (interlockedCompareExchangeObj(ref initValue, newValue, null) != null)
            {
                FailTest("Interlocked.CompareExchange<object> - old value");
                return;
            }
            if (initValue != newValue)
            {
                FailTest("Interlocked.CompareExchange<object> - new value");
                return;
            }
            try
            {
                interlockedCompareExchangeObj(ref *(object*)null, null, null);
                FailTest("Interlocked.CompareExchange<object> - null location");
                return;
            }
            catch (NullReferenceException) { }
        }
        PassTest();
    }

    static void TestThrowIfNull()
    {
        StartTest("TestThrowIfNull");
        ClassForNre c = null;
        var success = true;
        try
        {
            var f = c.F; //field access
            PrintLine("NRE Field load access failed");
            success = false;
        }
        catch (NullReferenceException)
        {
        }
        catch (Exception)
        {
            success = false;
        }
        try
        {
            c.F = 1;
            PrintLine("NRE Field store access failed");
            success = false;
        }
        catch (NullReferenceException)
        {
        }
        catch (Exception)
        {
            success = false;
        }
        try
        {
            var f = c.ToString(); //virtual method access
            PrintLine("NRE virtual method access failed");
            success = false;
        }
        catch (NullReferenceException)
        {
        }
        catch (Exception)
        {
            success = false;
        }

        try
        {
            c.NonVirtual(); //method access
            PrintLine("NRE non virtual method access failed");
            success = false;
        }
        catch (NullReferenceException)
        {
        }
        catch (Exception)
        {
            success = false;
        }

        EndTest(success);
    }

    private static void TestCkFinite()
    {
        // includes tests from https://github.com/dotnet/coreclr/blob/9b0a9fd623/tests/src/JIT/IL_Conformance/Old/Base/ckfinite.il4
        StartTest("CkFiniteTests");
        if (!CkFiniteTest.CkFinite32(0) || !CkFiniteTest.CkFinite32(1) ||
            !CkFiniteTest.CkFinite32(100) || !CkFiniteTest.CkFinite32(-100) ||
            !CkFinite32(0x7F7FFFC0) || CkFinite32(0xFF800000) ||  // use converter function to get the float equivalent of this bits
            CkFinite32(0x7FC00000) && !CkFinite32(0xFF7FFFFF) ||
            CkFinite32(0x7F800000))
        {
            FailTest("one or more 32 bit tests failed");
            return;
        }

        if (!CkFiniteTest.CkFinite64(0) || !CkFiniteTest.CkFinite64(1) ||
            !CkFiniteTest.CkFinite64(100) || !CkFiniteTest.CkFinite64(-100) ||
            CkFinite64(0x7FF0000000000000) || CkFinite64(0xFFF0000000000000) ||
            CkFinite64(0x7FF8000000000000) || !CkFinite64(0xFFEFFFFFFFFFFFFF))
        {
            FailTest("one or more 64 bit tests failed.");
            return;
        }
        PassTest();
    }

    private static unsafe bool CkFinite32(uint value)
    {
        return CkFiniteTest.CkFinite32(*(float*)(&value));
    }

    private static unsafe bool CkFinite64(ulong value)
    {
        return CkFiniteTest.CkFinite64(*(double*)(&value));
    }

    private static void TestFloatToIntConversions()
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        static T HideFromOptimizations<T>(T value) => value;

        StartTest("Test float to int conversions");
        if ((int)HideFromOptimizations(1245.6789d) != 1245)
        {
            FailTest("(int)1245.6789d not equal to 1245");
            return;
        }
        if ((int)HideFromOptimizations(double.NaN) != 0)
        {
            FailTest("(int)double.NaN not equal to 0");
            return;
        }
        if ((int)HideFromOptimizations(double.PositiveInfinity) != int.MaxValue)
        {
            FailTest("(int)double.PositiveInfinity not equal to int.MaxValue");
            return;
        }
        if ((int)HideFromOptimizations(double.NegativeInfinity) != int.MinValue)
        {
            FailTest("(int)double.NegativeInfinity not equal to int.MinValue");
            return;
        }
        PassTest();
    }

    static void TestIntOverflows()
    {
        TestCharInOvf();

        TestSignedIntAddOvf();

        TestSignedLongAddOvf();

        TestUnsignedIntAddOvf();

        TestUnsignedLongAddOvf();

        TestSignedIntSubOvf();

        TestSignedLongSubOvf();

        TestUnsignedIntSubOvf();

        TestUnsignedLongSubOvf();

        TestUnsignedIntMulOvf();

        TestUnsignedLongMulOvf();

        TestSignedIntMulOvf();

        TestSignedLongMulOvf();

        TestSignedToSignedNativeIntConvOvf();

        TestUnsignedToSignedNativeIntConvOvf();

        TestSignedToUnsignedNativeIntConvOvf();

        TestI1ConvOvf();

        TestUnsignedI1ConvOvf();

        TestI2ConvOvf();

        TestUnsignedI2ConvOvf();

        TestI4ConvOvf();

        TestUnsignedI4ConvOvf();

        TestI8ConvOvf();

        TestUnsignedI8ConvOvf();
    }

    private static void TestSignedToSignedNativeIntConvOvf()
    {
        // TODO: when use of nint is available
    }

    private static void TestUnsignedToSignedNativeIntConvOvf()
    {
        // TODO: when use of nuint is available
    }

    private static unsafe void TestSignedToUnsignedNativeIntConvOvf()
    {
        StartTest("Test unsigned native int Conv_Ovf"); // TODO : wasm64
        int thrown = 0;
        long i = 1;
        void* converted;
        checked { converted = (void*)i; }
        if (converted != new IntPtr(1).ToPointer()) FailTest("Test unsigned native int Conv_Ovf conversion failed");
        try
        {
            i = uint.MaxValue + 1L;
            checked { converted = (void*)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            i = -1;
            checked { converted = (void*)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        if (thrown != 2) FailTest("Test unsigned native int Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
        EndTest(true);

    }

    private static void TestI1ConvOvf()
    {
        StartTest("Test I1 Conv_Ovf");
        int thrown = 0;
        int i = 1;
        float f = 127.9F;
        sbyte converted;
        checked { converted = (sbyte)i; }
        if (converted != 1) FailTest("Test I1 Conv_Ovf conversion failed" + converted.ToString());
        checked { converted = (sbyte)(-1); }
        checked { converted = (sbyte)f; }
        checked { converted = (sbyte)((float)-128.5F); }
        try
        {
            i = sbyte.MaxValue + 1;
            checked { converted = (sbyte)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            i = sbyte.MinValue - 1;
            checked { converted = (sbyte)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (float)(sbyte.MaxValue + 1);
            checked { converted = (sbyte)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (float)(sbyte.MinValue - 1);
            checked { converted = (sbyte)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        if (thrown != 4) FailTest("Test I1 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
        EndTest(true);
    }

    private static void TestUnsignedI1ConvOvf()
    {
        StartTest("Test unsigned I1 Conv_Ovf");
        int thrown = 0;
        int i = 1;
        float f = 255.9F;
        byte converted;
        checked { converted = (byte)i; }
        if (converted != 1) FailTest("Test unsigned I1 Conv_Ovf conversion failed" + converted.ToString());
        checked { converted = (byte)f; }
        try
        {
            i = byte.MaxValue + 1;
            checked { converted = (byte)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            i = -1;
            checked { converted = (byte)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (float)(byte.MaxValue + 1);
            checked { converted = (byte)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = -1f;
            checked { converted = (byte)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        if (thrown != 4) FailTest("Test unsigned I1 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
        EndTest(true);
    }

    private static void TestI2ConvOvf()
    {
        StartTest("Test I2 Conv_Ovf");
        int thrown = 0;
        int i = 1;
        float f = 32767.9F;
        Int16 converted;
        checked { converted = (Int16)i; }
        if (converted != 1) FailTest("Test I2 Conv_Ovf conversion failed" + converted.ToString());
        checked { converted = (Int16)(-1); }
        checked { converted = (Int16)f; }
        checked { converted = (Int16)((float)-32768.5F); }
        try
        {
            i = Int16.MaxValue + 1;
            checked { converted = (Int16)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            i = Int16.MinValue - 1;
            checked { converted = (Int16)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (float)(Int16.MaxValue + 1);
            checked { converted = (Int16)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (float)(Int16.MinValue - 1);
            checked { converted = (Int16)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        if (thrown != 4) FailTest("Test I2 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
        EndTest(true);
    }

    private static void TestUnsignedI2ConvOvf()
    {
        StartTest("Test unsigned I2 Conv_Ovf");
        int thrown = 0;
        int i = 1;
        float f = 65535.9F;
        UInt16 converted;
        checked { converted = (UInt16)i; }
        if (converted != 1) FailTest("Test unsigned I2 Conv_Ovf conversion failed" + converted.ToString());
        checked { converted = (UInt16)f; }
        try
        {
            i = UInt16.MaxValue + 1;
            checked { converted = (UInt16)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            i = -1;
            checked { converted = (UInt16)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            short s = -1; // test overflow check is not reliant on different widths
            checked { converted = (UInt16)s; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (float)(UInt16.MaxValue + 1);
            checked { converted = (UInt16)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = -1f;
            checked { converted = (UInt16)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        if (thrown != 5) FailTest("Test unsigned I2 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
        EndTest(true);
    }

    private static void TestI4ConvOvf()
    {
        StartTest("Test I4 Conv_Ovf");
        int thrown = 0;
        long i = 1;
        double f = 2147483647.9d;
        int converted;
        checked { converted = (int)i; }
        if (converted != 1) FailTest("Test I4 Conv_Ovf conversion failed" + converted.ToString());
        checked { converted = (int)(-1); }
        checked { converted = (int)f; }
        checked { converted = (int)((double)-2147483648.9d); }
        try
        {
            i = int.MaxValue + 1L;
            checked { converted = (int)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            i = int.MinValue - 1L;
            checked { converted = (int)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (double)(int.MaxValue + 1L);
            checked { converted = (int)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (double)(int.MinValue - 1L);
            checked { converted = (int)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        if (thrown != 4) FailTest("Test I4 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
        EndTest(true);
    }

    private static void TestUnsignedI4ConvOvf()
    {
        StartTest("Test unsigned I4 Conv_Ovf");
        int thrown = 0;
        long i = 1;
        double f = 4294967294.9d;
        uint converted;
        checked { converted = (uint)i; }
        if (converted != 1) FailTest("Test unsigned I4 Conv_Ovf conversion failed" + converted.ToString());
        checked { converted = (uint)f; }
        try
        {
            i = uint.MaxValue + 1L;
            checked { converted = (uint)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            i = -1;
            checked { converted = (uint)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (double)(uint.MaxValue + 1L);
            checked { converted = (uint)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = -1d;
            checked { converted = (uint)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        if (thrown != 4) FailTest("Test unsigned I4 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
        EndTest(true);
    }

    private static void TestI8ConvOvf()
    {
        StartTest("Test I8 Conv_Ovf");
        int thrown = 0;
        long i = 1;
        double f = 9223372036854774507.9d; /// not a precise check
        long converted;
        checked { converted = (long)i; }
        if (converted != 1) FailTest("Test I8 Conv_Ovf conversion failed" + converted.ToString());
        checked { converted = (long)(-1); }
        checked { converted = (long)f; }
        checked { converted = (long)((double)-9223372036854776508d); } // not a precise check
        try
        {
            ulong ul = long.MaxValue + (ulong)1;
            checked { converted = (int)ul; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (double)(long.MaxValue) + 1000d; // need to get into the next representable double
            checked { converted = (int)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (double)(long.MinValue) - 2000d; // need to get into the next representable double
            checked { converted = (long)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            byte[] bytes = new byte[2];
            var b = bytes[i];
        }
        catch (OverflowException)
        {
            EndTest(false, "implicit convertion of long to int should not throw when long has value == 1");
        }
        if (thrown != 3) FailTest("Test I8 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
        EndTest(true);
    }

    private static void TestUnsignedI8ConvOvf()
    {
        StartTest("Test unsigned I8 Conv_Ovf");
        int thrown = 0;
        long i = 1;
        double f = 18446744073709540015.9d; // not a precise check
        ulong converted;
        checked { converted = (ulong)i; }
        if (converted != 1) FailTest("Test unsigned I8 Conv_Ovf conversion failed" + converted.ToString());
        checked { converted = (ulong)f; }
        try
        {
            i = -1;
            checked { converted = (ulong)i; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = (double)(ulong.MaxValue) + 2000d; // need to get into the next representable double
            checked { converted = (ulong)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        try
        {
            f = -1d;
            checked { converted = (ulong)f; }
        }
        catch (OverflowException)
        {
            thrown++;
        }
        if (thrown != 3) FailTest("Test unsigned I8 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
        EndTest(true);
    }

    private static void TestSignedLongAddOvf()
    {
        StartTest("Test long add overflows");
        bool thrown;
        long op64l = 1;
        long op64r = long.MaxValue;
        thrown = false;
        try
        {
            long res = checked(op64l + op64r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i64 addition of positive number");
            return;
        }
        thrown = false;
        op64l = long.MinValue; // add negative to overflow below the MinValue
        op64r = -1;
        try
        {
            long res = checked(op64l + op64r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i64 addition of negative number");
            return;
        }
        EndTest(true);
    }

    private static void TestCharInOvf()
    {
        // Just checks the compiler can handle the char type
        // This was failing for https://github.com/dotnet/corert/blob/f542d97f26e87f633310e67497fb01dad29987a5/src/System.Private.CoreLib/shared/System/Environment.Unix.cs#L111
        StartTest("Test char add overflows");
        char opChar = '1';
        int op32r = 2;
        if (checked(opChar + op32r) != 51)
        {
            FailTest("No overflow for char failed"); // check not always throwing an exception
            return;
        }
        PassTest();
    }

    private static void TestSignedIntAddOvf()
    {
        StartTest("Test int add overflows");
        bool thrown;
        int op32l = 1;
        int op32r = 2;
        if (checked(op32l + op32r) != 3)
        {
            FailTest("No overflow failed"); // check not always throwing an exception
            return;
        }
        op32l = 1;
        op32r = int.MaxValue;
        thrown = false;
        try
        {
            int res = checked(op32l + op32r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i32 addition of positive number");
            return;
        }

        thrown = false;
        op32l = int.MinValue; // add negative to overflow below the MinValue
        op32r = -1;
        try
        {
            int res = checked(op32l + op32r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i32 addition of negative number");
            return;
        }
        PassTest();
    }

    private static void TestUnsignedIntAddOvf()
    {
        StartTest("Test uint add overflows");
        bool thrown;
        uint op32l = 1;
        uint op32r = 2;
        if (checked(op32l + op32r) != 3)
        {
            FailTest("No overflow failed"); // check not always throwing an exception
            return;
        }
        op32l = 1;
        op32r = uint.MaxValue;
        thrown = false;
        try
        {
            uint res = checked(op32l + op32r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for unsigned i32 addition of positive number");
            return;
        }
        PassTest();
    }

    private static void TestUnsignedLongAddOvf()
    {
        StartTest("Test ulong add overflows");
        bool thrown;
        ulong op64l = 1;
        ulong op64r = 2;
        if (checked(op64l + op64r) != 3)
        {
            FailTest("No overflow failed"); // check not always throwing an exception
            return;
        }
        op64l = 1;
        op64r = ulong.MaxValue;
        thrown = false;
        try
        {
            ulong res = checked(op64l + op64r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for unsigned i64 addition of positive number");
            return;
        }
        PassTest();
    }

    private static void TestSignedLongSubOvf()
    {
        StartTest("Test long sub overflows");
        bool thrown;
        long op64l = -2;
        long op64r = long.MaxValue;
        thrown = false;
        try
        {
            long res = checked(op64l - op64r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i64 substraction of positive number");
            return;
        }
        thrown = false;
        op64l = long.MaxValue; // subtract negative to overflow above the MaxValue
        op64r = -1;
        try
        {
            long res = checked(op64l - op64r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i64 addition of negative number");
            return;
        }
        EndTest(true);
    }

    private static void TestSignedIntSubOvf()
    {
        StartTest("Test int sub overflows");
        bool thrown;
        int op32l = 5;
        int op32r = 2;
        if (checked(op32l - op32r) != 3)
        {
            FailTest("No overflow failed"); // check not always throwing an exception
            return;
        }
        op32l = -2;
        op32r = int.MaxValue;
        thrown = false;
        try
        {
            int res = checked(op32l - op32r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i32 subtraction of positive number");
            return;
        }

        thrown = false;
        op32l = int.MaxValue; // subtract negative to overflow above the MaxValue
        op32r = -1;
        try
        {
            int res = checked(op32l - op32r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i32 subtraction of negative number");
            return;
        }
        PassTest();
    }

    private static void TestUnsignedIntSubOvf()
    {
        StartTest("Test uint sub overflows");
        bool thrown;
        uint op32l = 5;
        uint op32r = 2;
        if (checked(op32l - op32r) != 3)
        {
            FailTest("No overflow failed"); // check not always throwing an exception
            return;
        }
        op32l = 0;
        op32r = 1;
        thrown = false;
        try
        {
            uint res = checked(op32l - op32r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for unsigned i32 subtraction of positive number");
            return;
        }
        PassTest();
    }

    private static void TestUnsignedLongSubOvf()
    {
        StartTest("Test ulong sub overflows");
        bool thrown;
        ulong op64l = 5;
        ulong op64r = 2;
        if (checked(op64l - op64r) != 3)
        {
            FailTest("No overflow failed"); // check not always throwing an exception
            return;
        }
        op64l = 0;
        op64r = 1;
        thrown = false;
        try
        {
            ulong res = checked(op64l - op64r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for unsigned i64 addition of positive number");
            return;
        }
        PassTest();
    }

    private static void TestUnsignedIntMulOvf()
    {
        StartTest("Test uint multiply overflows");
        bool thrown;
        uint op32l = 10;
        uint op32r = 20;
        if (checked(op32l * op32r) != 200)
        {
            FailTest("No overflow failed"); // check not always throwing an exception
            return;
        }
        op32l = 2;
        op32r = (uint.MaxValue >> 1) + 1;
        thrown = false;
        try
        {
            uint res = checked(op32l * op32r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for unsigned i32 multiply of numbers");
            return;
        }
        op32l = 0;
        op32r = 0; // check does a division so make sure this case is handled
        thrown = false;
        try
        {
            uint res = checked(op32l * op32r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (thrown)
        {
            FailTest("exception not thrown for unsigned i32 multiply of zeros");
            return;
        }
        PassTest();
    }

    private static void TestUnsignedLongMulOvf()
    {
        StartTest("Test ulong multiply overflows");
        bool thrown;
        ulong op64l = 10;
        ulong op64r = 20;
        if (checked(op64l * op64r) != 200L)
        {
            FailTest("No overflow failed"); // check not always throwing an exception
            return;
        }
        op64l = 2;
        op64r = (ulong.MaxValue >> 1) + 1;
        thrown = false;
        try
        {
            ulong res = checked(op64l * op64r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for unsigned i64 multiply of numbers");
            return;
        }
        op64l = 0;
        op64r = 0; // check does a division so make sure this case is handled
        thrown = false;
        try
        {
            ulong res = checked(op64l * op64r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (thrown)
        {
            FailTest("exception not thrown for unsigned i64 multiply of zeros");
            return;
        }
        PassTest();
    }

    private static void TestSignedIntMulOvf()
    {
        StartTest("Test int multiply overflows");
        bool thrown;
        int op32l = 10;
        int op32r = -20;
        if (checked(op32l * op32r) != -200)
        {
            FailTest("No overflow failed"); // check not always throwing an exception
            return;
        }
        op32l = 2;
        op32r = (int.MaxValue >> 1) + 1;
        thrown = false;
        try
        {
            int res = checked(op32l * op32r);
            PrintLine("should have overflow but was " + res.ToString());
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i32 multiply overflow");
            return;
        }
        op32l = 2;
        op32r = (int.MinValue >> 1) - 1;
        thrown = false;
        try
        {
            int res = checked(op32l * op32r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i32 multiply underflow");
            return;
        }
        op32l = 0;
        op32r = 0; // check does a division so make sure this case is handled
        thrown = false;
        try
        {
            int res = checked(op32l * op32r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (thrown)
        {
            FailTest("exception not thrown for signed i32 multiply of zeros");
            return;
        }

        PassTest();
    }

    private static void TestSignedLongMulOvf()
    {
        StartTest("Test long multiply overflows");
        bool thrown;
        long op64l = 10;
        long op64r = -20;
        if (checked(op64l * op64r) != -200)
        {
            FailTest("No overflow failed"); // check not always throwing an exception
            return;
        }
        op64l = 2;
        op64r = (long.MaxValue >> 1) + 1;
        thrown = false;
        try
        {
            long res = checked(op64l * op64r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i64 multiply overflow");
            return;
        }
        op64l = 2;
        op64r = (long.MinValue >> 1) - 1;
        thrown = false;
        try
        {
            long res = checked(op64l * op64r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (!thrown)
        {
            FailTest("exception not thrown for signed i64 multiply underflow");
            return;
        }
        op64l = 0;
        op64r = 0; // check does a division so make sure this case is handled
        thrown = false;
        try
        {
            long res = checked(op64l * op64r);
        }
        catch (OverflowException)
        {
            thrown = true;
        }
        if (thrown)
        {
            FailTest("exception not thrown for signed i64 multiply of zeros");
            return;
        }
        PassTest();
    }

    private static unsafe void TestStackTrace()
    {
        StartTest("Test StackTrace");
#if DEBUG
        EndTest(new StackTrace().ToString().Contains("TestStackTrace"), new StackTrace().ToString());
#else
        EndTest(new StackTrace().ToString().Contains("wasm-function"));
#endif
    }

    static void TestJavascriptCall()
    {
        StartTest("Test Javascript call");

        IntPtr resultPtr = JSInterop.InternalCalls.InvokeJSUnmarshalled(out string exception, "Answer", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        EndTest(resultPtr.ToInt32() == 42);
    }

    static void TestPalRandom()
    {
        StartTest("Test pal_random.lib.js integration");

        EndTest(Guid.NewGuid() != Guid.NewGuid());
    }

    static void TestGlobalization()
    {
        StartTest("Test simple globalization");

        CultureInfo jp = CultureInfo.GetCultureInfo("jp");
        if (jp.CompareInfo.Compare("さようなら", "サヨウナラ", CompareOptions.IgnoreKanaType) != 0)
        {
            FailTest("Kana-insensitive comparison failed");
            return;
        }

        CultureInfo ru = CultureInfo.GetCultureInfo("ru");
        if (ru.TextInfo.ToTitleCase("до свидания") != "До Свидания")
        {
            FailTest("ToTitleCase(ru) comparison failed");
            return;
        }

        TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
        if (tz.BaseUtcOffset != TimeSpan.FromHours(3))
        {
            FailTest("Moscow time not UTC+3");
            return;
        }

        if (TimeZoneInfo.GetSystemTimeZones().Count == 0)
        {
            FailTest("System time zones missing");
            return;
        }

        PassTest();
    }

    static void TestDefaultConstructorOf()
    {
        StartTest("Test DefaultConstructorOf");
        var c = Activator.CreateInstance<ClassForNre>();
        EndTest(c != null);
    }

    internal struct LargeArrayBuilder<T>
    {
        private readonly int _maxCapacity;

        public LargeArrayBuilder(bool initialize)
            : this(maxCapacity: int.MaxValue)
        {
        }

        public LargeArrayBuilder(int maxCapacity)
            : this()
        {
            _maxCapacity = maxCapacity;
        }
    }

    static void TestStructUnboxOverload()
    {
        StartTest("Test DefaultConstructorOf");
        var s = new LargeArrayBuilder<string>(true);
        var s2 = new LargeArrayBuilder<string>(1);
        EndTest(true); // testing compilation 
    }

    static void TestGetSystemArrayEEType()
    {
        StartTest("Test can call GetSystemArrayEEType through CalliIntrinsic");
        IList e = new string[] { "1" };
        foreach (string s in e)
        {
        }
        EndTest(true); // testing compilation 
    }

    static void TestBoolCompare()
    {
        StartTest("Test Bool.Equals");
        bool expected = true;
        bool actual = true;
        EndTest(expected.Equals(actual));
    }

    private static byte[] bytes = { 0xff };

    static void TestReadByteArray()
    {
        StartTest("Test ldelemt from byte arry");

        int i = (int)bytes[0];
        EndTest(i == 0xff);
    }

    // This test was generated to test DiyFp return values.
    static void TestDoublePrint()
    {
        StartTest("Test Double ToString");

        EndTest(1d.ToString() == "1");
    }

    static void TestGenStructContains()
    {
        StartTest("Test Double ToString");

        var col = new List<ValueTuple<char, char>>() { new ValueTuple<char, char>('a', 'b') };

        var contains = col.Contains(new ValueTuple<char, char>('a', 'b'));

        EndTest(contains);
    }

    [InlineArray(3)]
    internal struct ReturnArea
    {
        private ulong buffer;

        internal unsafe nint AddressOfReturnArea()
        {
            return (nint)Unsafe.AsPointer(ref buffer);
        }
    }

    internal class ThreadStaticAlignCheck1
    {
        [ThreadStatic]
        [FixedAddressValueType]
        internal static ReturnArea returnArea = default;
    }

    internal class Padder
    {
        private object o1;
    }

    internal class ThreadStaticAlignCheck2
    {
        [ThreadStatic]
        [FixedAddressValueType]
        internal static ReturnArea returnArea = default;
    }

    static void TestThreadStaticAlignment()
    {
        StartTest("Test ThreadStatic Alignment");

        // Assume that these are allocated sequentially, use a padding object of size 12 ( mod 8 is not 0 ) to move the alignment of the second AddressOfReturnArea in case the first is
        // coincidentally aligned 8.
        var ts1Addr = ThreadStaticAlignCheck1.returnArea.AddressOfReturnArea();
        var p = new Padder();
        var ts2Addr = ThreadStaticAlignCheck2.returnArea.AddressOfReturnArea();

        EndTest((((nint)ts1Addr) % 8 == 0) && (((nint)ts2Addr) % 8 == 0));
    }

    static ushort ReadUInt16()
    {
        // something with MSB set
        return 0x828f;
    }

    // there's no actual implementation for this we just want the reverse delegate created
    [DllImport("*")]
    internal static extern bool SomeExternalUmanagedFunction(DelegateToCallFromUnmanaged callback);

    unsafe internal delegate bool DelegateToCallFromUnmanaged(char* charPtr);
}

// Separate class since you can't mix async with unsafe.
class EventLoopTestClass
{
    public static void TestEventLoopIntegration()
    {
        Task.Run(async () =>
        {
            try
            {
                await TestEventLoopIntegrationImpl();
            }
            catch (Exception e)
            {
                Environment.FailFast($"Unhandled exception: {e}");
            }
        });
    }

    private static async Task TestEventLoopIntegrationImpl()
    {
        const int Count = 10;

        int counter = 0;
        Task head = new Task(() => { });
        Task tail = head;
        for (int i = 0; i < Count; i++)
        {
            tail = tail.ContinueWith((_) => counter++);
        }

        head.Start();
        await tail;
        EndEventLoopTest(counter == Count, "Event loop integration (thread pool)");

        TimeSpan delay = TimeSpan.FromMilliseconds(25);
        long startTime = Stopwatch.GetTimestamp();
        using (PeriodicTimer timer = new PeriodicTimer(delay))
        {
            await timer.WaitForNextTickAsync();
        }
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTime);
        EndEventLoopTest(elapsed >= delay, "Event loop integration (timers)");
    }

    private static void EndEventLoopTest(bool success, string test)
    {
        if (success)
        {
            Program.PrintLine($"{test}: ok");
        }
        else
        {
            // We have already returned the exit code by now, so indicate failure with a fail fast.
            Environment.FailFast($"{test} failed");
        }
    }
}

namespace JSInterop
{
    internal static class InternalCalls
    {
        [DllImport("js", EntryPoint = "corert_wasm_invoke_js_unmarshalled")]
        private static extern IntPtr InvokeJSUnmarshalledInternal(string js, int length, IntPtr p1, IntPtr p2, IntPtr p3, out string exception);

        public static IntPtr InvokeJSUnmarshalled(out string exception, string js, IntPtr p1, IntPtr p2, IntPtr p3)
        {
            return InvokeJSUnmarshalledInternal(js, js.Length, p1, p2, p3, out exception);
        }
    }
}

public class ClassForNre
{
    public int F;
    public void NonVirtual() { }
}


public class ClassWithFloat
{
    public static float F;
}

public class SampleClassWithGenericDelegate
{
    public static bool CallDelegate<T>(T[] items)
    {
        return new Stack<T>(items).CallDelegate(DoWork);
    }

    public static bool DoWork<T>(T[] items)
    {
        Program.PrintLine("DoWork");
        return items.Length > 0;
    }
}

public class Stack<T>
{
    T[] items;

    public Stack(T[] items)
    {
        this.items = items;
    }

    public bool CallDelegate(StackDelegate d)
    {
        Program.PrintLine("CallDelegate");
        Program.PrintLine(items.Length.ToString());
        return d(items);
    }

    public delegate bool StackDelegate(T[] items);
}

public struct BoxStubTest
{
    public string Value;
    public override string ToString()
    {
        return Value;
    }

    public string GetValue()
    {
        Program.PrintLine("BoxStubTest.GetValue called");
        Program.PrintLine(Value);
        return Value;
    }
}

public class TestClass
{
    public string TestString { get; set; }
    public int TestInt { get; set; }

    public TestClass(int number)
    {
        if (number != 1337)
            throw new Exception();
    }

    public void TestMethod(string str)
    {
        TestString = str;
        if (TestString == str)
            Program.PrintLine("Instance method call test: Ok.");
    }
    public virtual void TestVirtualMethod(string str)
    {
        Program.PrintLine("Virtual Slot Test: Ok If second");
    }

    public virtual void TestVirtualMethod2(string str)
    {
        Program.PrintLine("Virtual Slot Test 2: Ok");
    }

    public int InstanceDelegateTarget()
    {
        return TestInt;
    }

    public virtual void VirtualDelegateTarget()
    {
        Program.FailTest("Virtual delegate incorrectly dispatched to base.");
    }
}

public class TestDerivedClass : TestClass
{
    public TestDerivedClass(int number) : base(number)
    {

    }
    public override void TestVirtualMethod(string str)
    {
        Program.PrintLine("Virtual Slot Test: Ok");
        base.TestVirtualMethod(str);
    }

    public override string ToString()
    {
        throw new Exception();
    }

    public override void VirtualDelegateTarget()
    {
        Program.PassTest();
        Program.PrintLine("Virtual Delegate Test: Ok");
    }
}

public class StaticsInited
{
    public static bool BeforeFieldInitInited;
    public static bool NonBeforeFieldInitInited;
}

public class BeforeFieldInitTest
{
    public static int TestField = BeforeFieldInit();

    public static void Nop() { }

    static int BeforeFieldInit()
    {
        StaticsInited.BeforeFieldInitInited = true;
        return 3;
    }
}

public class NonBeforeFieldInitTest
{
    public static int TestField;

    public static void Nop() { }

    static NonBeforeFieldInitTest()
    {
        TestField = 4;
        StaticsInited.NonBeforeFieldInitInited = true;
    }
}

public interface ICastingTest1
{
    int GetValue();
}

public interface ICastingTest2
{
    int GetValue();
}

public abstract class CastingTestClass
{
    public abstract int GetValue();
}

public class DerivedCastingTestClass1 : CastingTestClass, ICastingTest1
{
    public override int GetValue() => 1;
}

public class DerivedCastingTestClass2 : CastingTestClass, ICastingTest2
{
    public override int GetValue() => 2;
}

public interface ITestItf
{
    int GetValue();
}

public struct ItfStruct : ITestItf
{
    public int GetValue()
    {
        return 4;
    }
}

public sealed class MySealedClass
{
    uint _data;

    public MySealedClass()
    {
        _data = 104;
    }

    public MySealedClass(uint data)
    {
        _data = data;
    }

    public uint GetData()
    {
        return _data;
    }

    public override int GetHashCode()
    {
        return (int)_data * 2;
    }

    public override string ToString()
    {
        Program.PrintLine("MySealedClass.ToString called. Data:");
        Program.PrintLine(_data.ToString());
        return _data.ToString();
    }
}

public struct StructWithDouble
{
    public double DoubleField;
}

public struct StructWithObjectAndDouble
{
    public object ObjectField;
    public double DoubleField;
}

public class Gen<T>
{
    internal Type TestTypeOf()
    {
        return typeof(T);
    }
}

public class MyUnsealedClass
{
    uint _data;

    public MyUnsealedClass()
    {
        _data = 24;
    }

    public MyUnsealedClass(uint data)
    {
        _data = data;
    }

    public uint GetData()
    {
        return _data;
    }

    public override int GetHashCode()
    {
        return (int)_data * 2;
    }

    public override string ToString()
    {
        return _data.ToString();
    }
}

public class MyBase
{
    protected uint _data;
    public MyBase(uint data)
    {
        _data = data;
    }

    public virtual uint GetData()
    {
        return _data;
    }
}

public class UnsealedDerived : MyBase
{
    public UnsealedDerived(uint data) : base(data) { }
    public override uint GetData()
    {
        return _data * 2;
    }
}

public sealed class SealedDerived : MyBase
{
    public SealedDerived(uint data) : base(data) { }
    public override uint GetData()
    {
        return _data * 3;
    }
}

class ClassWithSealedVTable : ISomeItf
{
    public int GetValue()
    {
        return 37;
    }
}

interface ISomeItf
{
    int GetValue();
}

class ClassWithFourThreadStatics
{
    [ThreadStatic] static int classStatic;
    [ThreadStatic] static int classStatic2 = 2;
    [ThreadStatic] static int classStatic3;
    [ThreadStatic] static int classStatic4;
    [ThreadStatic] static int classStatic5;

    public int GetStatic()
    {
        return classStatic2;
    }

    public void IncrementStatics()
    {
        classStatic++;
        classStatic2++;
        classStatic3++;
        classStatic4++;
        classStatic5++;
    }
}

class AnotherClassWithFourThreadStatics
{
    [ThreadStatic] static int classStatic = 13;
    [ThreadStatic] static int classStatic2;
    [ThreadStatic] static int classStatic3;
    [ThreadStatic] static int classStatic4;
    [ThreadStatic] static int classStatic5;

    public int GetStatic()
    {
        return classStatic;
    }

    /// <summary>
    /// stops field unused compiler error, but never called
    /// </summary>
    public void IncrementStatics()
    {
        classStatic2++;
        classStatic3++;
        classStatic4++;
        classStatic5++;
    }
}

class DisposableTest : IDisposable
{
    public int Count = 0;

    public void Dispose()
    {
        Count++;
    }
}

class FieldStatics
{
    static int X;
    static int Y;
    static string S1;
    static string S2;

    public bool TestGetSet()
    {
        if (!(X == 0 && Y == 0 && S1 == null && S2 == null)) return false;

        X = 17;
        Y = 347;
        S1 = "first string";
        S2 = "a different string";

        return X == 17 && Y == 347 && S1 == "first string" && S2 == "a different string";
    }
}

namespace System.Runtime.InteropServices
{

    [AttributeUsage((System.AttributeTargets.Method | System.AttributeTargets.Class))]
    internal class McgIntrinsicsAttribute : Attribute
    {
    }
}
