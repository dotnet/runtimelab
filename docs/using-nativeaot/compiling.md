# Compiling with Native AOT

This document explains how to compile and publish your project using Native AOT toolchain. First, please _ensure that [pre-requisites](prerequisites.md) are installed_. If you are starting a new project, you may find the [HelloWorld sample](../../samples/HelloWorld/README.md) directions useful.

## Add ILCompiler package reference

To use Native AOT with your project, you need to add a reference to the ILCompiler NuGet package containing the Native AOT compiler and runtime. Make sure the `nuget.config` file for your project contains the following package sources under the `<packageSources>` element:
```xml
<add key="dotnet-experimental" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json" />
<add key="nuget" value="https://api.nuget.org/v3/index.json" />
```

If your project has no `nuget.config` file, it may be created by running
```bash
> dotnet new nugetconfig
```

from the project's root directory. New package sources must be added after the `<clear />` element if you decide to keep it.

Once you have added the package sources, add a reference to the ILCompiler package either by running
```bash
> dotnet add package Microsoft.DotNet.ILCompiler -v 7.0.0-*
```

or by adding the following element to the project file:
```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.DotNet.ILCompiler" Version="7.0.0-*" />
  </ItemGroup>
```

## Compile and publish your app

Use the `dotnet publish` command to compile and publish your app:
```bash
> dotnet publish -r <RID> -c <Configuration>
```

where `<Configuration>` is your project configuration (such as Debug or Release) and `<RID>` is the runtime identifier reflecting your host OS and architecture (one of win-x64, linux-x64, osx-x64). For example, to publish the Release build of your app for Windows x64, run the following command:
```bash
> dotnet publish -r win-x64 -c Release
```

If the compilation succeeds, the native executable will be placed under the `bin/<Configuration>/net5.0/<RID>/publish/` path relative to your project's root directory.

## Cross-architecture compilation

Native AOT toolchain allows targeting ARM64 on an x64 host and vice versa for both Windows and Linux. Cross-OS compilation, such as targeting Linux on a Windows host, is not supported. To target win-arm64 on a Windows x64 host, in addition to the `Microsoft.DotNet.ILCompiler` package reference, also add the `runtime.win-x64.Microsoft.DotNet.ILCompiler` package reference to get the x64-hosted compiler:
```xml
<PackageReference Include="Microsoft.DotNet.ILCompiler.LLVM; runtime.win-x64.Microsoft.DotNet.ILCompiler.LLVM" Version="7.0.0-alpha.1.21423.2" />
```

Note that it is important to use _the same version_ for both packages to avoid potential hard-to-debug issues (use the latest version from the [dotnet-experimental feed](https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-experimental&package=Microsoft.DotNet.ILCompiler&protocolType=NuGet)). After adding the package reference, you may publish for win-arm64 as usual:
```bash
> dotnet publish -r win-arm64 -c Release
```
### WebAssembly

Install and activate Emscripten. See [Install Emscripten](https://emscripten.org/docs/getting_started/downloads.html#installation-instructions-using-the-emsdk-recommended)

For WebAssembly, it is always a cross-architecture scenario as the compiler runs on Windows/Linux/MacOS and the runtime is for WebAssembly.  WebAssembly is not integrated into the main ILCompiler so first remove (if you added it from above)

```xml
<PackageReference Include="Microsoft.DotNet.ILCompiler" Version="7.0.0-*" />
```

Then, the required package reference is
```xml
<PackageReference Include="Microsoft.DotNet.ILCompiler.LLVM; runtime.win-x64.Microsoft.DotNet.ILCompiler.LLVM" Version="7.0.0-*" />
```
and the publish command (there is no Release build currently)
```bash
> dotnet publish -r browser-wasm -c Debug /p:TargetArchitecture=wasm /p:PlatformTarget=AnyCPU /p:MSBuildEnableWorkloadResolver=false --self-contained
```

Note that the wasm-tools workload is identified as a dependency even though its not used, and this confuses the toolchain, hence `/p:MSBuildEnableWorkloadResolver=false`

#### WebAssembly native libraries
To compile a WebAssembly native library that exports a function `Answer`:
```cs
[System.Runtime.InteropServices.UnmanagedCallersOnly(EntryPoint = "Answer")]
public static int Answer()
{
    return 42;
}
```
```bash
> dotnet publish /p:NativeLib=Static /p:SelfContained=true -r browser-wasm -c Debug /p:TargetArchitecture=wasm /p:PlatformTarget=AnyCPU /p:MSBuildEnableWorkloadResolver=false /p:WasmExtraLinkerArgs="-s EXPORTED_FUNCTIONS=_Answer -s EXPORTED_RUNTIME_METHODS=cwrap" --self-contained
```

#### WebAssembly module imports
Functions in other WebAssembly modules can be imported and invoked using `DllImport` e.g.
```cs
[DllImport("*")]
static extern int random_get(byte* buf, uint size);
```
Be default emscripten will create a WebAssembly import for this function, importing from the `env` module.  This can be controlled with `WasmImport` items in the project file.  For example
```xml
<ItemGroup>
    <WasmImport Include="wasi_snapshot_preview1!random_get" />
</ItemGroup>
```
Will cause the above `random_get` to create this WebAssembly:
```
(import "wasi_snapshot_preview1" "random_get" (func $random_get (type 3)))
```
This can be used to import WASI functions that are in other modules, either as the above, in WASI, `wasi_snapshot_preview1`, or in other WebAssembly modules that may be linked with [WebAssembly module linking](https://github.com/WebAssembly/module-linking)

#### WebAssembly with WASI and the WASI SDK
WASI can be targeted by specifying `/p:WasmWasi=true` to `dotnet publish`.  This will build with options to enable running against the WASI standard.  This will disable exception handling, as currently that requires Javascript.  Note that emscripten support for
WASI is in progress and many calls do not work.

As an alternative to emscripten, the WASI SDK can be used to produce the WebAssembly. To do this first install the WASI SDK from [WASI SDK](https://github.com/WebAssembly/wasi-sdk/releases).  On Windows this is the MinGW tar file, but MinGW is not required, unpacking
the tar file is sufficient.  Then pass `/p:WasiSdkPath=<path to WASI SDK>` to `dotnet publish`.  The CoreCLR defines a few symbols that are not available when using the WASI SDK so in addition to your C# you will need to supply those symbols as stub functions in a separate C file.
An example is below
```c
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

 int emscripten_get_callstack(int flags, char* buf, int maxbytes)
{
    *buf = 0;
    return 0;
}

int __cxa_thread_atexit(void (*func)(), void *obj, void *dso_symbol)
{
	return 0;
}

int pthread_create(int sig, int i, int j, void (*func)(int))
{
	return 0;
}

void* pthread_self(void)
{
	return 0;
}

int pthread_equal(void* t1, void* t2)
{
	return 1;
}

int __errno_location()
{
	return 0;
}

int pthread_condattr_destroy(void* attr)
{
	return 0;
}

int pthread_condattr_init(void* attr)
{
	return 0;
}

int pthread_condattr_setclock(void* attr, void* clock_id)
{
	return 0;
}

int pthread_mutex_init(void* restrict mutex, const void* restrict attr)
{
	return 0;
}

int pthread_cond_init(void* restrict cond,const void* restrict attr)
{
	return 0;
}

int pthread_mutex_destroy(void* mutex)
{
	return 0;
}

int pthread_attr_init(void* attr)
{
	return 0;
}

int pthread_attr_destroy(void* attr)
{
	return 0;
}

int pthread_attr_setdetachstate(void* attrint, int x)
{
	return 0;
}

int pthread_mutex_lock(void* mutex)
{
	return 0;
}

int pthread_mutex_unlock(void* mutex)
{
	return 0;
}
int pthread_cond_broadcast(void* cond)
{
	return 0;
}

int pthread_cond_timedwait(void* restrict cond,	void* restrict mutex, const void* restrict abstime)
{
	return 0;
}
int pthread_cond_wait(void* restrict cond,void* restrict mutex)
{
	return 0;
}

int pthread_cond_destroy(void* cond)
{
	return 0;
}

int mprotect(void* addr, size_t len, int prot)
{
	return 0;
}

int pthread_getattr_np(void* thread, void* attr)
{
	return 0;
}

void* dlopen(const char* filename, int flag)
{
	return NULL;
}

char* dlerror(void)
{
	return NULL;
}

void* dlsym(void* handle, const char* symbol)
{
	return NULL;
}

int pthread_attr_getstack(void* attr, void** stackaddr, size_t* stacksize)
{
	return 0;
}

int mlock(const void* addr, size_t len)
{
	return 0;
}

int munlock(const void* addr, size_t len)
{
	return 0;
}

int posix_madvise(void* addr, size_t len, int advice)
{
	return 0;
}

int pthread_mutexattr_destroy(void* attr)
{
	return 0;
}

int pthread_mutexattr_init(void* attr)
{
	return 0;
}

int pthread_mutexattr_settype(void* attr, int type)
{
	return 0;
}

#define RLIM_INFINITY (~0UL) // In WASI SDK this is a long long, but we'll match emscripten to avoid a warning
unsigned long getrlimit(int resource, void* rlim)
{
	return RLIM_INFINITY;
}

void* __cxa_allocate_exception(size_t thrown_size)
{
	return NULL;
}

void __cxa_throw(void* thrown_exception, void* tinfo, void (*dest)(void*))
{
}

int readdir_r(void* dirp, void* entry, void** result)
{
	return 0;
}

int flock(int fd, int operation)
{
	return 0;
}

int fstatfs(int fd, void* buf)
{
	return 0;
}

void syslog(int priority, void* format, ...)
{
}

int pthread_cond_signal(void* cond)
{
	return 0;
}

int getrusage(int who, void* usage)
{
	return 0;
}

int dlclose(void* handle)
{
	return 0;
}

int signal(int sig, void (*func)(int))
{
	return 0;
}

int gai_strerror(int sig )
{
	return 0;
}

// mmap2() Overrides the weak alias that Emscripten defines which returns ENOSYS
// This is only used from GCToOSInterface::Initialize
long mmap(long addr, long len, long prot, long flags, long fd, long long off) {
	
	void* alignedAddress;

	if (addr == 0)
	{
		// alignment must match OS_PAGE_SIZE or GCToOSInterface::Initialize will fail
		if (posix_memalign(&alignedAddress, len, 65536) != 0)
		{
			return -1; // failed
		}
	}

	memset(alignedAddress, 0, len);
	return (long)alignedAddress;
}

int dotnet_browser_entropy(char* buf, int size)
{
	return 0;
}

```
This file can be compiled with the `clang` that comes with the WASI SDK or the one shipped with emscripten.  To compile this file, assuming the name is `wasi.c`, using clang:
```
<path to WASI SDK>\bin\clang --target=wasm32-unknown-wasi wasi.c -c
```
Then to link it, add `/p:WasmExtraLinkerArgs=wasi.o` to `dotnet publish`.  Due to WASI SDK and emscripten not sharing some constants in their implementations of libc we need a temporary workaround to open files in WASI.  When opening a file, pass
the flag `0x20000000` in the `FileOptions` parameter, this will switch the runtime to use the flags that the WASI SDK `open` call expects.  E.g.
```
using (var fs = new FileStream("/tmp/direction", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096, (FileOptions)0x20000000))
```

### Cross-compiling on Linux
Similarly, to target linux-arm64 on a Linux x64 host, in addition to the `Microsoft.DotNet.ILCompiler` package reference, also add the `runtime.linux-x64.Microsoft.DotNet.ILCompiler` package reference to get the x64-hosted compiler:
```xml
<PackageReference Include="Microsoft.DotNet.ILCompiler; runtime.linux-x64.Microsoft.DotNet.ILCompiler" Version="7.0.0-alpha.1.21423.2" />
```

You also need to specify the sysroot directory for Clang using the `SysRoot` property. For example, assuming you are using one of ARM64-targeting [Docker images](../workflow/building/coreclr/linux-instructions.md#Docker-Images) employed for cross-compilation by this repo, you may publish for linux-arm64 with the following command:
```bash
> dotnet publish -r linux-arm64 -c Release -p:CppCompilerAndLinker=clang-9 -p:SysRoot=/crossrootfs/arm64
```

You may also follow [cross-building instructions](../workflow/building/coreclr/cross-building.md) to create your own sysroot directory.
