param (
    [string]$buildConfig
)

# LLVM is supplied in a gz file which Windows doesn't natively understand, so we need gz to unpack it - TODO this is liable to fail randomly when a new version comes out and the version number changes
Invoke-WebRequest -Uri https://tukaani.org/xz/xz-5.2.5-windows.zip -OutFile xz.zip
Expand-Archive -LiteralPath xz.zip -DestinationPath .
copy bin_i686\xz.exe . # get it in the path for tar

Invoke-WebRequest -Uri https://github.com/llvm/llvm-project/releases/download/llvmorg-15.0.6/llvm-15.0.6.src.tar.xz -OutFile llvm-15.0.6.src.tar.xz

./xz -d --force llvm-15.0.6.src.tar.xz
tar -xf llvm-15.0.6.src.tar

Invoke-WebRequest -Uri https://github.com/llvm/llvm-project/releases/download/llvmorg-15.0.6/cmake-15.0.6.src.tar.xz -OutFile cmake-15.0.6.src.tar.xz

./xz -d --force cmake-15.0.6.src.tar.xz
tar -xf cmake-15.0.6.src.tar
mv cmake-15.0.6.src cmake

cd llvm-15.0.6.src
mkdir build
cd build

if($buildConfig -eq "Release")
{
    & "$env:CMakePath" -G "Visual Studio 17 2022" -DCMAKE_BUILD_TYPE=Release -DLLVM_USE_CRT_RELEASE=MT -DLLVM_INCLUDE_BENCHMARKS=OFF -Thost=x64 ..
}
else
{
    & "$env:CMakePath" -G "Visual Studio 17 2022" -DCMAKE_BUILD_TYPE=Debug -DLLVM_USE_CRT_DEBUG=MTd -DLLVM_INCLUDE_BENCHMARKS=OFF -Thost=x64 ..
}

& "$env:CMakePath" --build . --config $buildConfig --target LLVMCore
& "$env:CMakePath" --build . --config $buildConfig --target LLVMBitWriter
#& "$env:CMakePath" --build . --target LLVMDebugInfoDwarf
