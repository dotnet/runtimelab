Invoke-WebRequest -Uri https://github.com/WebAssembly/wasi-sdk/releases/download/wasi-sdk-22/wasi-sdk-22.0.m-mingw64.tar.gz -OutFile wasi-sdk-22.0.m-mingw64.tar.gz

tar -xzf wasi-sdk-22.0.m-mingw64.tar.gz

mv wasi-sdk-22.0+m wasi-sdk

# Temporary WASI-SDK 22 workaround: Until
# https://github.com/WebAssembly/wasi-libc/issues/501 is addressed, we copy
# pthread.h from the wasm32-wasi-threads include directory to the wasm32-wasi
# include directory.  See https://github.com/dotnet/runtimelab/issues/2598 for
# the issue to remove this workaround once WASI-SDK 23 is released.

cp wasi-sdk/share/wasi-sysroot/include/wasm32-wasi-threads/pthread.h wasi-sdk/share/wasi-sysroot/include/wasm32-wasi/
