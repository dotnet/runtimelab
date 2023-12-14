![image](https://github.com/dotnet/runtimelab/assets/11523312/a40d1e79-bcf6-4eac-ae3c-2824bf84c50d)# NativeAOT Android sample app

This experiment was focused on enabling Native AOT for Android apps to achieve a smaller app size and faster app startup time. The experiment was concluded at the end .NET 8 release, with no additional efforts planned beyond that period. Its goal was to explore the possibilities and limitations of running Android apps with Native AOT, particularly in terms of size and startup time.

The .NET runtime currently supports Android platforms through the Mono JIT compiler. These platforms allow for dynamic code execution and, as a result, do not impose strict requirements on full AOT compilation. Since the Mono JIT compiler is used, the initial execution of a specific code path may result in an increased time needed for compilation. To address this and improve the startup time, it employs profiled AOT compilation, which AOT compiles only the startup path.

In Android apps, interaction with the JavaVM requires reflection, and the Mono compiler relies on type information that is available at runtime by bundling managed assemblies within an app. Additionally, when dealing with generic types in C#, it requires generic sharing methods, which can be larger and slower fallback options used when the compiler cannot determine the type during the compilation. Including managed assemblies and gshared methods in the final output can increase the application's size.

## Motivation and goals

Our primary goal is to evaluate the potential of the Native AOT for compiling smaller and faster Xamarin/Maui Android apps. We compared Native AOT with the Mono runtime, focusing on size and speed aspects with an idea that smaller and faster apps would be more appealing to developers.

Native AOT offers features that can be beneficial for Android development. It includes the ILCompiler, which performs full program optimization and managed code trimming based on code generation information for more precise removal of unused types and unreachable methods. Additionally, it uses direct addressing without runtime relocations. The Native AOT runtime also contains essential functionalities like garbage collection, exception handling, type casting, and stack walking.

## Implementation

The Xamarin sample app with Native AOT represents an improved version of the runtime application, including a Java interop layer. Instead of utilizing the monodroid.c wrapper, it incorporates the implementation of JniRuntime without Mono embeddings and garbage collection functionalities. The initialization of JniRuntime is initiated by MonoRunner.java, which provides an invocation pointer within the JNI_OnLoad function. The Java.Interop.dll is compiled as a standalone library, eliminating the dependency on Mono embeddings.

The Java interop layer performs communication from C# into Java without the need for C wrappers. Below is an example illustrating how managed code can update a Java native component using the Java interop layer:
```csharp
var envp = new JniTransition (env);
try {
    var jclass = JniEnvironment.Types.FindClass("net/dot/MonoRunner");
    var methodId = JniEnvironment.StaticMethods.GetStaticMethodID (jclass, "setText", "(Ljava/lang/String;)V");
    unsafe {
        JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
            new JniArgumentValue (JniEnvironment.Strings.NewString (txt)),
        };
        JniEnvironment.StaticMethods.CallStaticVoidMethod (jclass, methodId, parameters);
    }
} catch (Exception e) {
    Console.Error.WriteLine ($"JNI_OnLoad: error: {e}");
    envp.SetPendingException (e);
} finally {
    envp.Dispose();
}
```
The Mono app is a simple .NET (Xamarin) Android application with a single page. While it's not a direct one-to-one comparison, both apps have the same functionality and utilize the shared Java interop layer. This interop layer is crucial for Java interactions in Xamarin and Maui applications, and we consider this comparison as close as we can get within the scope of the experiment.

## Testing methodology
When developing an Android application, two critical aspects to consider are startup time and size on disk (SOD). To compare the Mono AOT and Native AOT apps, the following measurements were conducted, prioritized by their significance:
1.	Performance startup: Hot startup time was measured using a dotnet performance script that measures time until the display is presented to the user. The average time of 10 consecutive startups was recorded.
2.	Size on disk: Measurements were taken for both unzipped and zipped bundles. It is important to note that .NET Android JITed apps are not trimmed.
3.	Build times: The duration of the msbuild build process when building applications for a device.

Another measurement that hasn't been considered in this experiment is the application's overall performance. Since the Mono AOT compiler compiles only the startup path ahead of time, it's possible that the overall performance of the app could be slower when compared to the Native AOT compiler. However, due to lack of complete Native AOT support, this measure isn't included in the experiment.

## Performance measurements

### Startup time
The following table shows the startup measurements for the Xamarin sample application from starting the app until the display is presented to the user.

Startup	| Mono | Native AOT | Diff (%)
-|-|-|-
Xamarin sample app (Samsung Galaxy S10 Lite) | 206ms | 242ms | 17.48%
Xamarin sample app (Samsung Galaxy S23) | 161.8ms | 158.2ms | -2.23%
Xamarin sample app (Pixel 5) | 204.4ms | 182ms | -10.98%
Xamarin sample app (Pixel 7) | 151.6ms | 146ms | -3.69%
Emulator | 190ms | 179ms | -5.79%

The startup time demonstrates inconsistency across various devices and a simulator. In the case of a Mono Xamarin application, the startup path is AOT compiled with profiling, which narrows the performance gap compared to the Native AOT. Furthermore, the startup time includes both the application's performance and the compositor's performance. The rendering of the application includes GPU access for initializing Vulkan or OpenGL surfaces. It's important to note that the displayed results can be influenced by factors such as the Android version and device configuration. Even on the same device, having different Android versions installed can lead to varying results. Additionally, there is a standard deviation of about 15ms in all measurements.

### Size measurements
The following tables show the size measurements for the Xamarin sample application with size information for each file contained in the bundle, and the sizes of zipped archives by using Mono and Native AOT compilers.

SOD | Mono | Native AOT | Diff (%)
-|-|-|-
Xamarin sample app | 3.2M | 4.1M | 84.375%

As expected, the Mono SOD is significantly smaller due to JIT compilation. The Native AOT SDK, bootstrap, GC, and Java.Interop library contribute to the increased SOD.

### Build time

The following table shows the build time for the Xamarin sample application.

Build | Mono | Native AOT | Diff (%)
-|-|-|-
Xamarin sample app | 15.79s | 24.67s | 28.13%

As expected, Native AOT is more intensive due to full program analysis and ahead of time compilation. It is important to note that different build approaches are used, and the comparison is not one-to-one but confirms the general assumption.

## How to build and run the application

When building for the first time (on a clean checkout) run the commands below.

Setup the local environment:
```bash
git clone https://github.com/dotnet/runtimelab.git
```
```bash
git checkout feature/nativeaot-android
```

Export the SDK and NDK root directories:
```bash
export ANDROID_SDK_ROOT=~/android-sdk                                                                             
export ANDROID_NDK_ROOT=~/android-ndk-r23c
```
Build the ilc for the host:
```bash
./build.sh clr+clr.aot

```
Build the native and managed libs for the linux-bionic target:
```bash
TARGET_BUILD_ARCH=arm64 ./build.sh -s clr.nativeaotruntime+clr.nativeaotlibs+libs -os linux-bionic
```

Copy `JNIEnvironment/Java.Interop.dll` to the coreroot directory so the ILCompiler can find it.

Generate the app bundle:
``` bash
make run
```

## Complete Java interop support with Native AOT
Java interoperability enables Android app developers to be able to call Java APIs on Android to create an app with a more native experience. Thus, adding Java interaction support to Native AOT is crucial to support Native AOT for Xamarin/Maui Android framework. This enables the use of Java APIs for drawing and interacting with Java-native components and widgets. To improve compatibility between Native AOT and Android utilizing Java interop, several key steps can be taken. First, enabling Native AOT for Android by implementing cross-compilation for the Android platforms. Second, ensuring full Java interop support in Native AOT through listed findings, such as implementing garbage collection compatibility through ConditionalWeakTable, adapting Mono embeddings, and implementing a static type registration system.
