using BenchmarkDotNet.Attributes;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Benchmarks
{
    partial class NativeExportsNE
    {
        public partial class NativeExportsSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            private NativeExportsSafeHandle() : base(ownsHandle: true)
            { }

            protected override bool ReleaseHandle()
            {
                bool didRelease = NativeExportsNE.ReleaseHandle(handle);
                return didRelease;
            }

            public static NativeExportsSafeHandle CreateNewHandle() => AllocateHandle();


            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "alloc_handle")]
            private static partial NativeExportsSafeHandle AllocateHandle();
        }

        [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "alloc_handle")]
        public static partial NativeExportsSafeHandle AllocateHandle();

        [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "alloc_handle_out")]
        public static partial void AllocateHandle(out NativeExportsSafeHandle handle);

        [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "release_handle")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static partial bool ReleaseHandle(nint handle);

        [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "is_handle_alive")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static partial bool IsHandleAlive(NativeExportsSafeHandle handle);

        [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "modify_handle")]
        public static partial void ModifyHandle(ref NativeExportsSafeHandle handle, [MarshalAs(UnmanagedType.I1)] bool newHandle);
    }

    public class SafeHandles
    {
        [Benchmark]
        public bool ReturnSafeHandle()
        {
            using NativeExportsNE.NativeExportsSafeHandle handle = NativeExportsNE.AllocateHandle();
            return handle.IsInvalid;
        }

        [Benchmark]
        public bool ByRefHandle()
        {
            using NativeExportsNE.NativeExportsSafeHandle handleToDispose = NativeExportsNE.AllocateHandle();
            NativeExportsNE.NativeExportsSafeHandle handle = handleToDispose;
            NativeExportsNE.ModifyHandle(ref handle, newHandle: true);
            using (handle)
            {
                return handleToDispose == handle;
            }
        }

        [Benchmark]
        public bool HandleByValue()
        {
            using NativeExportsNE.NativeExportsSafeHandle handle = NativeExportsNE.AllocateHandle();
            return NativeExportsNE.IsHandleAlive(handle);
        }
    }
}
