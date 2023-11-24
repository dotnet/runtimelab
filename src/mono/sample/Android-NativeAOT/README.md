# NativeAOT Android sample app

## Description

This experiment was focused on enabling Native AOT for Android apps to achieve a smaller app size and faster app startup time. The experiment was concluded at the end .NET 8 release, with no additional efforts planned beyond that period. Its goal was to explore the possibilities and limitations of running Android apps with Native AOT, particularly in terms of size and startup time.

The .NET runtime currently supports Android platforms through the Mono JIT compiler. These platforms allow for dynamic code execution and, as a result, do not impose strict requirements on full AOT compilation. Since the Mono JIT compiler is used, the startup and app performance may be affected the first time a particular code path is executed. To address this and improve the startup time, it employs profiled AOT compilation, which AOT compiles only the startup path.

In Android apps, interaction with the JavaVM requires reflection, and the Mono compiler relies on type information that is available at runtime by bundling managed assemblies within an app. Additionally, when dealing with generic types in C#, it requires generic sharing methods, which can be larger and slower fallback options used when the compiler cannot determine the type in advance. Including managed assemblies and these methods in the final output can increase the application's size.

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
