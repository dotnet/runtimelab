# Semantic Compatibility

Documentation on compatibility guidance and the current state. The version headings act as a rolling delta between the previous version.

## Version 1

The focus of version 1 is to support `NetCoreApp`. This implies that anything not needed by `NetCoreApp` is subject to change.

### `char` marshaller

The marshalling of `char` as [`CharSet.Ansi`](https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.charset) will not be supported. The built-in system used the [system default Windows ANSI code page](https://docs.microsoft.com/windows/win32/api/stringapiset/nf-stringapiset-widechartomultibyte) when on Windows and took the first byte of the UTF-8 encoding on non-Windows platforms. Both approaches are fundamnetally flawed and therefore not supported. If a single-byte character is expected to be marshalled it is left to the caller to convert a .NET `char` into a single `byte` prior to calling the native function.

## Verison 0

This version is the built-in IL Stub generation system that is triggered whenever a method marked with `DllImport` is invoked.