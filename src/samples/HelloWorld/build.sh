#!/bin/bash
xcrun swiftc -emit-module -emit-library -enable-library-evolution -emit-module-interface HelloLibrary.swift  -o libHelloLibrary.dylib

dotnet restore ../../Swift.Bindings/src/Swift.Bindings.csproj
dotnet build ../../Swift.Bindings/src/Swift.Bindings.csproj

if [ $? -eq 0 ]; then
    echo "Build successful. Running the BindingsTool..."
    dotnet ../../../artifacts/bin/Swift.Bindings/Debug/net9.0/Swift.Bindings.dll -a "./HelloLibrary.abi.json" -o "./"
    dotnet run
else
    echo "Build failed. Exiting..."
    exit 1
fi
