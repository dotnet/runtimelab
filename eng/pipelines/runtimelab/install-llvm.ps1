param (
    [string]$buildConfig = "Release"
)

# LLVM is supplied in a gz file which Windows doesn't native understand, so we need gz to unpack it - TODO this is liable to fail randomly when a new version comes out and the version number changes
Invoke-WebRequest -Uri https://tukaani.org/xz/xz-5.2.5-windows.zip -OutFile xz.zip
Expand-Archive -LiteralPath xz.zip -DestinationPath .
copy bin_i686\xz.exe . # get it in the path for tar

Invoke-WebRequest -Uri https://github.com/llvm/llvm-project/releases/download/llvmorg-11.0.0/llvm-11.0.0.src.tar.xz -OutFile llvm-11.0.0.src.tar.xz

./xz -d --force llvm-11.0.0.src.tar.xz
tar -xf llvm-11.0.0.src.tar

cd llvm-11.0.0.src
mkdir build
cd build

if($buildConfig -eq "Release")
{
    & "$env:CMakePath" -G "Visual Studio 16 2019" -DCMAKE_BUILD_TYPE=Release -DLLVM_USE_CRT_DEBUG=MTd -Thost=x64 ..
}
else
{
    & "$env:CMakePath" -G "Visual Studio 16 2019" -DCMAKE_BUILD_TYPE=Debug -LLVM_USE_CRT_RELEASE=MT -Thost=x64 ..
}

& "$env:CMakePath" --build . --config $buildConfig --target LLVMCore
& "$env:CMakePath" --build . --config $buildConfig --target LLVMBitWriter
#& "$env:CMakePath" --build . --target LLVMDebugInfoDwarf
