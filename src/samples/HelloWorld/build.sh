#!/bin/bash
xcrun swiftc -emit-module -emit-library -enable-library-evolution -emit-module-interface HelloLibrary.swift  -o libHelloLibrary.dylib

dotnet restore ../../SwiftBindings/src/SwiftBindings.csproj
dotnet build ../../SwiftBindings/src/SwiftBindings.csproj

if [ $? -eq 0 ]; then
    echo "Build successful. Running the BindingsTool..."
    dotnet ../../../artifacts/bin/SwiftBindings/Debug/net8.0/SwiftBindings.dll -a "./HelloLibrary.abi.json" -o "./"
    dotnet run
else
    echo "Build failed. Exiting..."
    exit 1
fi
