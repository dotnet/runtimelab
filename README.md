# AssemblyBuilder.Save()

In .NET Framework, System.Reflection.Emit namespace is used to Emit IL instructions (code) dynamically for two primary uses:
- Executing the dynamically generated IL.
- Saving the generated IL into an ECMA335 assembly down to disk.

Currently, .NET only supports the first of those two use-cases: It allows you to invoke dynamically generated code, but it doesn't yet support saving this code down to disk. This project is to prototype the implementation of that missing piece, via an external assembly.

This feature existed in .NET Frameworks (https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.assemblybuilder.save?view=netframework-4.8) 
but does not yet exist in .NET Core.
