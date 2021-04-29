using System.Runtime.InteropServices;

using Xunit;

namespace DllImportGenerator.IntegrationTests
{
    partial class NativeExportsNE
    {
        [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "invoke_callback_after_gc")]
        public static unsafe partial void InvokeAfterGC(delegate* <void> cb);

        [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "invoke_callback_blittable_args")]
        public static unsafe partial int InvokeWithBlittableArgument(delegate* <int, int, int> cb, int a, int b);
    }

    public class FunctionPointerTests
    {
        [Fact]
        public void InvokedAfterGC()
        {
            bool wasCalled = false;
            NativeExportsNE.InvokeAfterGC(Callback);
            Assert.True(wasCalled);

            void Callback()
            {
                wasCalled = true;
            }
        }

        [Fact]
        public void CalledWithArgumentsInOrder()
        {
            const int a = 100;
            const int b = 50;
            int result;

            result = NativeExportsNE.InvokeWithBlittableArgument(Callback, a, b);
            Assert.Equal(Callback(a, b), result);

            result = NativeExportsNE.InvokeWithBlittableArgument(Callback, b, a);
            Assert.Equal(Callback(b, a), result);

            static int Callback(int a, int b)
            {
                // Use a noncommutative operation to validate passed in order.
                return a - b;
            }
        }
    }
}
