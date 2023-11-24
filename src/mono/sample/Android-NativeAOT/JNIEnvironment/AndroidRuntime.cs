using System;
using Java.Interop;
using Java.NativeAOT;

namespace Android.NativeAOT {

	class AndroidRuntime : JniRuntime {
		internal AndroidRuntime (IntPtr env, IntPtr vm) : base (CreateOptions (env, vm))
		{
		}
		public static CreationOptions CreateOptions (IntPtr env, IntPtr vm)
		{
			return new CreationOptions {
				EnvironmentPointer = env,
				InvocationPointer = vm,
				NewObjectRequired = false,
				UseMarshalMemberBuilder = false,
				JniAddNativeMethodRegistrationAttributePresent = false,
				ValueManager = new ManagedValueManager (),
				ObjectReferenceManager = new ManagedObjectReferenceManager (null, null),
			};
		}
	}
}