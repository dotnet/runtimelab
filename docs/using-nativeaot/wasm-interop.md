# WebAssembly interop in NativeAOT

Traditionally, .NET uses lazy PInvoke resolution by default, meaning that managed
calls are bound to their native counterparts when the PInvoke method is first used.

WebAssembly, as a platform, does not support lazy resolution of functions coming
from outside the compiled module. This means that by default, PInvoke calls in code
compiled to WebAssembly with NativeAOT will throw `PlatformNotSupportedException`.

Interacting with native code therefore requires an additional gesture to determine
what kind of linkage should the AOT compiler use. There are two options:

1) If the native code will be linked-in statically, use [`<DirectPInvoke />`](interop.md).
2) If the native code will be provided to the WebAssembly module at instantiation time
   as an import, use the `[WasmImportLinkageAttribute]` on your PInvoke declaration.

Note that the various APIs Emscripten provides to interact with JS fall into the
first category.
