// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

.assembly extern mscorlib { }

.class public System.Runtime.JitTesting.LSSATestsIL
{
  .field private static void* s_addr;

  .method public static void Optimized_Run()
  {
    .locals (class ClassWithField obj, int8& objFieldRef)
    newobj instance void ClassWithField::.ctor()
    stloc obj

    ldloc obj
    ldflda int32 ClassWithField::Field
    stloc objFieldRef

    ldloc objFieldRef
    call void .this::Optimized_ExposedSdsu_AddressExposed(int8&)
    ldloc objFieldRef
    call void .this::Optimized_ExposedSdsu_DerivedFromAddressExposed(int8&)
    // We'd like to have this test too, but can't because it's not possible, currently, to sneak the def
    // of an untracked local into the argument list (i. e. place it between the def and use of an SDSU).
    // ldloc obj
    // call void .this::Optimized_ExposedSdsu_DerivedFromUntracked(class ClassWithField)

    ldloc obj
    call void .this::Optimized_ExposedLocal_AddressExposed(object)
    ldloca obj
    call void .this::Optimized_ExposedLocal_Untracked(object*)
    ldloc objFieldRef
    call void .this::Optimized_ExposedLocal_DerivedFromAddressExposed(int8&)
    ldloc objFieldRef
    call void .this::Optimized_ExposedLocal_DerivedFromUntracked(int8&)
    ret
  }

  .method private static void Optimized_ExposedSdsu_AddressExposed(int8& obj) noinlining
  {
    .custom instance void System.Runtime.JitTesting.LSSATestAttribute::.ctor(string) =
      {string('BB01:\n  ZEROINIT SS01\n  STORE SS00 ARG00 ARG00\n  LOAD SS00 ARG00\n  STORE SS01 TMP00 SDSU\n  LOAD SS01 TMP00')}
    .locals (int32 zeroOffset)

    // Test that we spill an address-exposed local's value to a "safe" location
    // if it is exposed. Otherwise, the safe point may modify the local and thus
    // lose the pin.
    ldc.i4 0
    stloc zeroOffset
    ldarga obj
    call void .this::SafePointCapture(void*)
    // We need this trick with a no-op "add" to coerce forward substitution
    // into substituting the address-exposed "obj" into the argument position.
    ldarg obj
    ldloc zeroOffset
    add
    call int8& .this::SafePointRelease()
    call void .this::SafePoint(int8&, int8&)
    ret
  }

  .method private static void Optimized_ExposedSdsu_DerivedFromAddressExposed(int8& obj) noinlining
  {
    .custom instance void System.Runtime.JitTesting.LSSATestAttribute::.ctor(string) =
      {string('BB01:\n  ZEROINIT SS01\n  STORE SS00 ARG00 ARG00\n  LOAD SS00 ARG00\n  STORE SS01 TMP00 SDSU\n  LOAD SS01 TMP00')}

    ldarga obj
    call void .this::SafePointCapture(void*)

    // Same idea as with the above test, but here our address-exposed local gets modified
    // while its derived value is live, not the initial SDSU it produces.
    ldarg obj
    ldc.i4 1
    add
    call int8& .this::SafePointRelease()
    call void .this::SafePoint(int8&, int8&)
    ret
  }

  // .method private static void Optimized_ExposedSdsu_DerivedFromUntracked(class ClassWithField obj) noinlining
  // {
  //   .custom instance void System.Runtime.JitTesting.LSSATestAttribute::.ctor(string) =
  //   .locals (class ClassWithField pinned pinnedObj)
  //
  //   ldarg obj
  //   stloc pinnedObj
  //
  //   // Same as above, but instead of an address-exposed base, we have an untracked one.
  //   ldloc pinnedObj
  //   ldflda int32 ClassWithField::Field
  //   ldnull
  //   stloc pinnedObj
  //   call void .this::SafePoint(int8&)
  //   ret
  // }

  .method private static void Optimized_ExposedLocal_AddressExposed(object obj) noinlining
  {
    .custom instance void System.Runtime.JitTesting.LSSATestAttribute::.ctor(string) =
      {string('BB01:\n  ZEROINIT SS01\n  STORE SS00 ARG00 ARG00\n  LOAD SS00 ARG00\n  STORE SS01 USR01 USR01/2')}
    .locals (object objCopy)

    ldarga obj
    call void .this::SafePointCapture(void*)

    // Now we're using a tracked local instead of an SDSU. Same rules apply.
    // The tests below also do the same, with variations on the kind of value.
    ldarg obj
    stloc objCopy

    call void .this::SafePointRelease()

    ldloc objCopy
    call void .this::SafePoint(object)
    ret
  }

  .method private static void Optimized_ExposedLocal_Untracked(object* pObj) noinlining
  {
    .custom instance void System.Runtime.JitTesting.LSSATestAttribute::.ctor(string) =
      {string('BB01:\n  STORE SS00 USR01 SDSU\n  LOAD SS00 USR01\n  STORE SS01 USR02 USR02/1\n  STORE SS00 USR01 SDSU')}
    .locals (object pinned pinnedObj, object objCopy)

    // Pinned locals will always be untracked, so they're convenient to use for
    // this kind of test.
    ldarg pObj
    ldind.ref
    stloc pinnedObj
    ldloc pinnedObj
    stloc objCopy

    ldnull
    stloc pinnedObj
    call void .this::SafePoint()

    ldloc objCopy
    call void .this::SafePoint(object)
    ret
  }

  .method private static void Optimized_ExposedLocal_DerivedFromAddressExposed(int8& obj) noinlining
  {
    .custom instance void System.Runtime.JitTesting.LSSATestAttribute::.ctor(string) =
      {string('BB01:\n  ZEROINIT SS01\n  STORE SS00 ARG00 ARG00\n  LOAD SS00 ARG00\n  STORE SS01 USR01 USR01/2')}
    .locals (int8& derivedObj)

    ldarga obj
    call void .this::SafePointCapture(void*)

    ldarg obj
    ldc.i4 1
    add
    stloc derivedObj

    call void .this::SafePointRelease()

    ldloc derivedObj
    call void .this::SafePoint(int8&)
    ret
  }

  .method private static void Optimized_ExposedLocal_DerivedFromUntracked(int8& obj) noinlining
  {
    .custom instance void System.Runtime.JitTesting.LSSATestAttribute::.ctor(string) =
      {string('BB01:\n  STORE SS00 USR01 SDSU\n  LOAD SS00 USR01\n  STORE SS01 USR02 USR02/1\n  STORE SS00 USR01 SDSU')}
    .locals (int8& pinned pinnedObj, int8& derivedObj)

    ldarg obj
    ldc.i4 1
    add // Defeat copy propagation (of 'obj')
    stloc pinnedObj

    ldloc pinnedObj
    ldc.i4 1
    add
    stloc derivedObj

    ldc.i4 0
    conv.i
    stloc pinnedObj
    call void .this::SafePoint()

    ldloc derivedObj
    call void .this::SafePoint(int8&)
    ret
  }

  .method private static void SafePointCapture(void* addr) noinlining
  {
    ldarg addr
    stsfld void* .this::s_addr
    ret
  }

  .method private static void SafePointRelease() noinlining
  {
    call int8& .this::SafePointRelease()
    pop
    ret
  }

  .method private static int8& SafePointRelease() noinlining
  {
    // Null out the location "s_addr" points to.
    ldsfld void* .this::s_addr
    ldc.i4 0
    conv.i
    stind.i

    // Just return some address
    ldsflda void* .this::s_addr
    ret
  }

  .method private static void SafePoint(int8&, int8&) noinlining
  {
    ret
  }

  .method private static void SafePoint(int8&) noinlining
  {
    ret
  }

  .method private static void SafePoint(object) noinlining
  {
    ret
  }

  .method private static void SafePoint() noinlining
  {
    ret
  }
}

.class ClassWithField
{
  .field public int32 Field

  .method public instance void .ctor()
  {
    ldarg 0
    call instance void .base::.ctor()
    ret
  }
}

.class public System.Runtime.JitTesting.LSSATestAttribute extends [mscorlib]System.Attribute
{
  .method public instance void .ctor(string allocation)
  {
    ldarg 0
    call instance void .base::.ctor()
    ret
  }
}
