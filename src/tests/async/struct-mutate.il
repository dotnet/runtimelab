// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

.assembly extern System.Runtime { }
.assembly extern xunit.core { }
.assembly extern xunit.assert { }
//.assembly extern System.Console { }
.assembly 'struct-mutate' { }

.class public auto ansi beforefieldinit Async2StructMutate
       extends [System.Runtime]System.Object
{
  .class sequential ansi sealed nested private beforefieldinit S
         extends [System.Runtime]System.ValueType
  {
    .field public int32 Value
    .method public hidebysig specialname rtspecialname 
            instance void  .ctor(int32 'value') cil managed
    {
      // Code size       8 (0x8)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  stfld      int32 Async2StructMutate/S::Value
      IL_0007:  ret
    } // end of method S::.ctor

    .method public hidebysig instance void modopt([System.Runtime]System.Threading.Tasks.Task) 
            Test() cil managed
    {
      // Code size       66 (0x42)
      .maxstack  3
      IL_0000:  ldc.i4.s   100
      IL_0002:  ldarg.0
      IL_0003:  ldfld      int32 Async2StructMutate/S::Value
      IL_0008:  call       void Async2StructMutate::AssertEqual(int32,
                                                                int32)
      IL_000d:  ldarg.0
      IL_000e:  ldarg.0
      IL_000f:  ldfld      int32 Async2StructMutate/S::Value
      IL_0014:  ldc.i4.1
      IL_0015:  add
      IL_0016:  stfld      int32 Async2StructMutate/S::Value
      IL_001b:  ldarg.0
      IL_001c:  call       instance void modopt([System.Runtime]System.Threading.Tasks.Task) Async2StructMutate/S::InstanceCall()
      IL_0021:  ldc.i4.s   103
      IL_0023:  ldarg.0
      IL_0024:  ldfld      int32 Async2StructMutate/S::Value
      IL_0029:  call       void Async2StructMutate::AssertEqual(int32,
                                                                int32)
      IL_002e:  ldarg.0
      IL_002f:  call       instance void modopt([System.Runtime]System.Threading.Tasks.Task) Async2StructMutate/S::TaskButNotAsync()
      IL_0034:  ldc.i4.s   104
      IL_0036:  ldarg.0
      IL_0037:  ldfld      int32 Async2StructMutate/S::Value
      IL_003c:  call       void Async2StructMutate::AssertEqual(int32,
                                                                int32)
      IL_0041:  ret
    } // end of method S::Test

    .method private hidebysig instance void modopt([System.Runtime]System.Threading.Tasks.Task) 
            InstanceCall() cil managed
    {
      // Code size       117 (0x75)
      .maxstack  3
      .locals init (valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter V_0,
               valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable V_1)
      IL_0000:  ldc.i4.s   101
      IL_0002:  ldarg.0
      IL_0003:  ldfld      int32 Async2StructMutate/S::Value
      IL_0008:  call       void Async2StructMutate::AssertEqual(int32,
                                                                int32)
      IL_000d:  ldarg.0
      IL_000e:  ldarg.0
      IL_000f:  ldfld      int32 Async2StructMutate/S::Value
      IL_0014:  ldc.i4.1
      IL_0015:  add
      IL_0016:  stfld      int32 Async2StructMutate/S::Value
      IL_001b:  ldc.i4.s   102
      IL_001d:  ldarg.0
      IL_001e:  ldfld      int32 Async2StructMutate/S::Value
      IL_0023:  call       void Async2StructMutate::AssertEqual(int32,
                                                                int32)
      IL_0028:  call       valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable [System.Runtime]System.Threading.Tasks.Task::Yield()
      IL_002d:  stloc.1
      IL_002e:  ldloca.s   V_1
      IL_0030:  call       instance valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable::GetAwaiter()
      IL_0035:  stloc.0
      IL_0036:  ldloca.s   V_0
      IL_0038:  call       instance bool [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter::get_IsCompleted()
      IL_003d:  brtrue.s   IL_0045

      IL_003f:  ldloc.0
      IL_0040:  call       void [System.Runtime]System.Runtime.CompilerServices.RuntimeHelpers::UnsafeAwaitAwaiterFromRuntimeAsync<valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter>(!!0)
      IL_0045:  ldloca.s   V_0
      IL_0047:  call       instance void [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter::GetResult()
      IL_004c:  ldc.i4.s   102
      IL_004e:  ldarg.0
      IL_004f:  ldfld      int32 Async2StructMutate/S::Value
      IL_0054:  call       void Async2StructMutate::AssertEqual(int32,
                                                                int32)
      IL_0059:  ldarg.0
      IL_005a:  ldarg.0
      IL_005b:  ldfld      int32 Async2StructMutate/S::Value
      IL_0060:  ldc.i4.1
      IL_0061:  add
      IL_0062:  stfld      int32 Async2StructMutate/S::Value
      IL_0067:  ldc.i4.s   103
      IL_0069:  ldarg.0
      IL_006a:  ldfld      int32 Async2StructMutate/S::Value
      IL_006f:  call       void Async2StructMutate::AssertEqual(int32,
                                                                int32)
      IL_0074:  ret
    } // end of method S::InstanceCall

    .method private hidebysig instance class [System.Runtime]System.Threading.Tasks.Task 
            TaskButNotAsync() cil managed
    {
      // Code size       33 (0x21)
      .maxstack  8
      IL_0000:  ldc.i4.s   103
      IL_0002:  ldarg.0
      IL_0003:  ldfld      int32 Async2StructMutate/S::Value
      IL_0008:  call       void [xunit.assert]Xunit.Assert::Equal<int32>(!!0,
                                                                         !!0)
      IL_000d:  ldarg.0
      IL_000e:  ldarg.0
      IL_000f:  ldfld      int32 Async2StructMutate/S::Value
      IL_0014:  ldc.i4.1
      IL_0015:  add
      IL_0016:  stfld      int32 Async2StructMutate/S::Value
      IL_001b:  call       class [System.Runtime]System.Threading.Tasks.Task [System.Runtime]System.Threading.Tasks.Task::get_CompletedTask()
      IL_0020:  ret
    } // end of method S::TaskButNotAsync

  } // end of class S

  .method public hidebysig static void  TestEntryPoint() cil managed
  {
    .custom instance void [xunit.core]Xunit.FactAttribute::.ctor() = ( 01 00 00 00 ) 
    // Code size       11 (0xb)
    .maxstack  8
    .entrypoint
    IL_0000:  call       class [System.Runtime]System.Threading.Tasks.Task Async2StructMutate::Async()
    IL_0005:  callvirt   instance void [System.Runtime]System.Threading.Tasks.Task::Wait()
    IL_000a:  ret
  } // end of method Async2StructMutate::TestEntryPoint

  .method private hidebysig static void modopt([System.Runtime]System.Threading.Tasks.Task) 
          Async() cil managed
  {
    // Code size       30 (0x1e)
    .maxstack  2
    .locals init (valuetype Async2StructMutate/S V_0)
    IL_0000:  ldloca.s   V_0
    IL_0002:  ldc.i4.s   100
    IL_0004:  call       instance void Async2StructMutate/S::.ctor(int32)
    IL_0009:  ldloca.s   V_0
    IL_000b:  call       instance void modopt([System.Runtime]System.Threading.Tasks.Task) Async2StructMutate/S::Test()
    IL_0010:  ldc.i4.s   104
    IL_0012:  ldloc.0
    IL_0013:  ldfld      int32 Async2StructMutate/S::Value
    IL_0018:  call       void Async2StructMutate::AssertEqual(int32,
                                                              int32)
    IL_001d:  ret
  } // end of method Async2StructMutate::Async

  .method private hidebysig static void  AssertEqual(int32 expected,
                                                     int32 val) cil managed noinlining
  {
    // Code size       8 (0x8)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldarg.1
    IL_0002:  call       void [xunit.assert]Xunit.Assert::Equal<int32>(!!0,
                                                                       !!0)
    IL_0007:  ret
  } // end of method Async2StructMutate::AssertEqual

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [System.Runtime]System.Object::.ctor()
    IL_0006:  ret
  } // end of method Async2StructMutate::.ctor

} // end of class Async2StructMutate
