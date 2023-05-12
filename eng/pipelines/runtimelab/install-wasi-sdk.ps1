Invoke-WebRequest -Uri https://github.com/WebAssembly/wasi-sdk/releases/download/wasi-sdk-20/wasi-sdk-20.0.m-mingw.tar.gz -OutFile wasi-sdk-20.0.m-mingw.tar.gz

tar -xzf wasi-sdk-20.0.m-mingw.tar.gz

mv wasi-sdk-20.0+m wasi-sdk

