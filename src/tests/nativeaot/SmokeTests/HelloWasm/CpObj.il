// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

.assembly extern mscorlib { }

.assembly CpObj { }

.class public CpObj.CpObjTest
{
  .method public hidebysig static void CpObj(valuetype CpObj.TestValue& dest, valuetype CpObj.TestValue& src)
  {
    .maxstack 8
    ldarg.0
    ldarg.1
    cpobj CpObj.TestValue
    ret
  }
}

.class public value sealed CpObj.TestValue
{
  .field public int32 Field;
}
