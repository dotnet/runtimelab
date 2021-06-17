using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DllImportGenerator.IntegrationTests
{
    internal partial class NativeExportsNE
    {
        internal partial class CallingConventions
        {
            [GeneratedDllImport(NativeExportsNE_Binary)]
            [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
            public static partial long AddLongsCdecl(long i, long j, long k, long l, long m, long n, long o, long p, long q);
            [GeneratedDllImport(NativeExportsNE_Binary)]
            [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
            public static partial long AddLongsStdcall(long i, long j, long k, long l, long m, long n, long o, long p, long q);
        }
    }

    public class CallingConventionTests
    {
    }
}
