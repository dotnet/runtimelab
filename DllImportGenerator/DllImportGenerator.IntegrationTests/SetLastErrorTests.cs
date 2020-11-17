using System;
using System.Runtime.InteropServices;

using Xunit;

namespace DllImportGenerator.IntegrationTests
{
    partial class NativeExportsNE
    {
        public partial class SetLastError
        {
            [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "set_error", SetLastError = true)]
            public static partial void SetError(int error, byte shouldSetError);

            [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "set_error_return_string", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.LPWStr)]
            public static partial string SetError_NonBlittableSignature(int error, [MarshalAs(UnmanagedType.U1)] bool shouldSetError, [MarshalAs(UnmanagedType.LPWStr)] string errorString);
        }
    }

    public class SetLastErrorTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(-5)]
        public void LastWin32Error_HasExpectedValue(int error)
        {
            string errorString = error.ToString();
            string ret = NativeExportsNE.SetLastError.SetError_NonBlittableSignature(error, shouldSetError: true, errorString);
            Assert.Equal(error, Marshal.GetLastWin32Error());
            Assert.Equal(errorString, ret);

            // Clear the last error
            MarshalEx.SetLastWin32Error(0);

            NativeExportsNE.SetLastError.SetError(error, shouldSetError: 1);
            Assert.Equal(error, Marshal.GetLastWin32Error());
        }

        [Fact]
        public void ClearPreviousError()
        {
            int error = 100;
            MarshalEx.SetLastWin32Error(error);

            // Don't actually set the error in the native call. SetLastError=true should clear any existing error.
            string errorString = error.ToString();
            string ret = NativeExportsNE.SetLastError.SetError_NonBlittableSignature(error, shouldSetError: false, errorString);
            Assert.Equal(0, Marshal.GetLastWin32Error());
            Assert.Equal(errorString, ret);

            MarshalEx.SetLastWin32Error(error);

            // Don't actually set the error in the native call. SetLastError=true should clear any existing error.
            NativeExportsNE.SetLastError.SetError(error, shouldSetError: 0);
            Assert.Equal(0, Marshal.GetLastWin32Error());
        }
    }
}
