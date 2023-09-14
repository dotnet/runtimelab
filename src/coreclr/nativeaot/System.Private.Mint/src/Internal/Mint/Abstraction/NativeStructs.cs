// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Internal.Mint.Abstraction;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct MonoMethodInstanceAbstractionNativeAot
{
    public byte* name;
    public IntPtr /*MonoClass* */ klass;

    public delegate* unmanaged<MonoMethodInstanceAbstractionNativeAot*, MonoMethodSignatureInstanceAbstractionNativeAot*> get_signature; // MonoMethodSignature* (* get_signature) (MonoMethod* self);
    public delegate* unmanaged<MonoMethodInstanceAbstractionNativeAot*, MonoMethodHeaderInstanceAbstractionNativeAot*> get_header; // MonoMethodHeader* (* get_header) (MonoMethod* self);

    public IntPtr gcHandle; // FIXME
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct MonoMethodSignatureInstanceAbstractionNativeAot
{
    public int param_count;
    public byte hasthis;

    public delegate* unmanaged<MonoMethodSignatureInstanceAbstractionNativeAot*, MonoTypeInstanceAbstractionNativeAot*> ret_ult; // MonoType * (*ret_ult)(MonoMethodSignature *self);

    public IntPtr gcHandle;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct MonoMethodHeaderInstanceAbstractionNativeAot
{
    public int code_size;
    public int max_stack;
    public int num_locals;
    public int num_clauses;
    public byte init_locals;

    public IntPtr get_local_sig; // MonoType * (*get_local_sig)(MonoMethodHeader *self, int32_t i);
    public delegate* unmanaged<MonoMethodHeaderInstanceAbstractionNativeAot*, byte*> get_code; // const uint8_t * (*get_code)(MonoMethodHeader *self);
    public delegate* unmanaged<MonoMethodHeaderInstanceAbstractionNativeAot*, byte*, int> get_ip_offset; // int32_t (*get_ip_offset)(MonoMethodHeader *self, const uint8_t *ip);


    public IntPtr gcHandle;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct MonoTypeInstanceAbstractionNativeAot
{
    public int type_code; // FIXME this could be a byte. it's just CorElementType

    public IntPtr gcHandle;
}
