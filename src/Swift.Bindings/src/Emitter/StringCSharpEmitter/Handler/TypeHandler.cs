// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;
using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Factory class for creating instances of FrozenStructHandler.
    /// </summary>
    public class FrozenStructHandlerFactory : IFactory<BaseDecl, ITypeHandler>
    {
        /// <summary>
        /// Determines if the factory handles the specified declaration.
        /// </summary>
        /// <param name="decl">The base declaration.</param>
        public bool Handles(BaseDecl decl)
        {
            return decl is StructDecl structDecl && MarshallingHelpers.StructIsMarshalledAsCSStruct(structDecl);
        }

        /// <summary>
        /// Constructs a new instance of StructHandler.
        /// </summary>
        public ITypeHandler Construct()
        {
            return new FrozenStructHandler();
        }
    }

    /// <summary>
    /// Handler class for frozen struct declarations.
    /// </summary>
    public class FrozenStructHandler : BaseHandler, ITypeHandler
    {
        public FrozenStructHandler()
        {
        }

        /// <summary>
        /// Marshals the specified struct declaration.
        /// </summary>
        /// <param name="structDecl">The struct declaration.</param>
        /// <param name="typeDatabase">The type database instance.</param>
        public IEnvironment Marshal(BaseDecl decl, TypeDatabase typeDatabase)
        {
            if (decl is not StructDecl structDecl)
            {
                throw new ArgumentException("The provided decl must be a StructDecl.", nameof(decl));
            }
            return new TypeEnvironment(structDecl, typeDatabase);
        }

        /// <summary>
        /// Emits the code for the specified environment.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        /// <param name="conductor">The conductor instance.</param>
        /// <param name="typeDatabase">The type database instance.</param>
        public void Emit(IndentedTextWriter writer, IEnvironment env, Conductor conductor)
        {
            var structEnv = (TypeEnvironment)env;
            var structDecl = (StructDecl)structEnv.TypeDecl;
            var parentDecl = structDecl.ParentDecl ?? throw new ArgumentNullException(nameof(structDecl.ParentDecl));
            var moduleDecl = structDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(structDecl.ParentDecl));
            // Retrieve type info from the type database
            var typeRecord = env.TypeDatabase.Registrar.GetType(moduleDecl.Name, structDecl.Name);
            SwiftTypeInfo? swiftTypeInfo = typeRecord?.SwiftTypeInfo;

            if (swiftTypeInfo.HasValue)
            {
                unsafe
                {
                    // Apply struct layout attributes
                    // TODO: refactor to use type metadata
                    writer.WriteLine($"[StructLayout(LayoutKind.Sequential, Size = {swiftTypeInfo.Value.ValueWitnessTable->Size})]");
                }
            }
            writer.WriteLine($"public unsafe struct {structDecl.Name} {{");
            writer.Indent++;

            // Emit each field in the struct
            foreach (var fieldDecl in structDecl.Fields)
            {
                string accessModifier = fieldDecl.Visibility == Visibility.Public ? "public" : "private";
                writer.WriteLine($"{accessModifier} {fieldDecl.CSTypeIdentifier.Name} {fieldDecl.Name};");

                // TODO: Fix memory access violation
                // // Verify field against Swift type information
                // if (swiftTypeInfo.HasValue && !VerifyFieldRecord(swiftTypeInfo.Value, structDecl.Fields.IndexOf(fieldDecl), fieldDecl))
                // {
                //     Console.WriteLine("Field record does not match the field declaration");
                // }
            }
            writer.WriteLine();

            base.HandleBaseDecl(writer, structDecl.Declarations, conductor, env.TypeDatabase);

            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// Verify field record with the Swift type information.
        /// </summary>
        private unsafe bool VerifyFieldRecord(SwiftTypeInfo swiftTypeInfo, int fieldIndex, FieldDecl fieldDecl)
        {
            // Access the field descriptor using pointer arithmetic
            FieldDescriptor* desc = (FieldDescriptor*)IntPtr.Add(
                (IntPtr)(((StructDescriptor*)swiftTypeInfo.Metadata->TypeDescriptor))->NominalType.FieldsPtr.Target,
                IntPtr.Size * fieldIndex
            );

            // Ensure the field number is within bounds
            if (desc->NumFields <= fieldIndex)
            {
                return false;
            }

            FieldRecord* fieldRecord = desc->GetFieldRecord(fieldIndex);

            // Check field name
            if ((System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)fieldRecord->Name.Target) ?? string.Empty) != fieldDecl.Name)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Factory class for creating instances of NonFrozenStructHandler.
    /// </summary>
    public class NonFrozenStructHandlerFactory : IFactory<BaseDecl, ITypeHandler>
    {
        /// <summary>
        /// Determines if the factory handles the specified declaration.
        /// </summary>
        /// <param name="decl">The base declaration.</param>
        public bool Handles(BaseDecl decl)
        {
            return decl is StructDecl structDecl && !MarshallingHelpers.StructIsMarshalledAsCSStruct(structDecl);
        }

        /// <summary>
        /// Constructs a new instance of StructHandler.
        /// </summary>
        public ITypeHandler Construct()
        {
            return new NonFrozenStructHandler();
        }
    }

    /// <summary>
    /// Handler class for non-frozen struct declarations.
    /// </summary>
    public class NonFrozenStructHandler : BaseHandler, ITypeHandler
    {
        public NonFrozenStructHandler()
        {
        }

        /// <summary>
        /// Marshals the specified struct declaration.
        /// </summary>
        /// <param name="structDecl">The struct declaration.</param>
        /// <param name="typeDatabase">The type database instance.</param>
        public IEnvironment Marshal(BaseDecl decl, TypeDatabase typeDatabase)
        {
            if (decl is not StructDecl structDecl)
            {
                throw new ArgumentException("The provided decl must be a StructDecl.", nameof(decl));

            }
            return new TypeEnvironment(structDecl, typeDatabase);
        }

        /// <summary>
        /// Emits the code for the specified environment.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        /// <param name="conductor">The conductor instance.</param>
        /// <param name="typeDatabase">The type database instance.</param>
        public void Emit(IndentedTextWriter writer, IEnvironment env, Conductor conductor)
        {
            var structEnv = (TypeEnvironment)env;
            var structDecl = (StructDecl)structEnv.TypeDecl;
            var parentDecl = structDecl.ParentDecl ?? throw new ArgumentNullException(nameof(structDecl.ParentDecl));
            var moduleDecl = structDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(structDecl.ModuleDecl));
            var typeRecord = env.TypeDatabase.Registrar.GetType(moduleDecl.Name, structDecl.Name);
            SwiftTypeInfo? swiftTypeInfo = typeRecord?.SwiftTypeInfo;

            writer.WriteLine($"public unsafe class {structDecl.Name} : IDisposable");
            writer.WriteLine("{");
            writer.Indent++;

            WritePrivateFields(writer);
            WriteDisposeMethod(writer, structDecl.Name);
            WriteFinalizer(writer, structDecl.Name);
            WritePayload(writer);
            WriteMetadata(writer, moduleDecl.Name, structDecl.MangledName, env.TypeDatabase);

            writer.WriteLine();
            base.HandleBaseDecl(writer, structDecl.Declarations, conductor, env.TypeDatabase);

            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes the private fields for the class.
        /// </summary>
        private static void WritePrivateFields(IndentedTextWriter writer)
        {
            writer.WriteLine();
            writer.WriteLine("private static nuint _payloadSize = Metadata.Size;");
            writer.WriteLine("private SwiftHandle _payload = SwiftHandle.Zero;");
            writer.WriteLine("private bool _disposed = false;");
        }

        /// <summary>
        /// Writes the Dispose method for the class.
        /// </summary>
        private static void WriteDisposeMethod(IndentedTextWriter writer, string className)
        {
            writer.WriteLine("public void Dispose()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (!_disposed)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("NativeMemory.Free((void*)_payload);");
            writer.WriteLine("_disposed = true;");
            writer.WriteLine("GC.SuppressFinalize(this);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes the finalizer for the class.
        /// </summary>
        private static void WriteFinalizer(IndentedTextWriter writer, string className)
        {
            writer.WriteLine($"~{className}()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("NativeMemory.Free((void*)_payload);");
            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes the metadata for the class.
        /// </summary>
        private static void WriteMetadata(IndentedTextWriter writer, string moduleName, string mangledName, TypeDatabase typeDatabase)
        {
            writer.WriteLine("public static TypeMetadata Metadata => PInvoke_getMetadata();");

            writer.WriteLine("[UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]");
            string libPath = typeDatabase.GetLibraryName(moduleName);
            writer.WriteLine($"[DllImport(\"{libPath}\", EntryPoint = \"{mangledName}Ma\")]");
            writer.WriteLine("internal static extern TypeMetadata PInvoke_getMetadata();");
        }

        /// <summary>
        /// Writes the payload accessor for the class.
        /// </summary>
        private static void WritePayload(IndentedTextWriter writer)
        {
            writer.WriteLine("public SwiftHandle Payload => _payload;");
        }
    }

    /// <summary>
    /// Factory class for creating instances of ClassHandler.
    /// </summary>
    public class ClassHandlerFactory : IFactory<BaseDecl, ITypeHandler>
    {
        /// <summary>
        /// Determines if the factory handles the specified declaration.
        /// </summary>
        /// <param name="decl">The base declaration.</param>
        public bool Handles(BaseDecl decl)
        {
            return decl is ClassDecl;
        }

        /// <summary>
        /// Constructs a new instance of ClassHandler.
        /// </summary>
        public ITypeHandler Construct()
        {
            return new ClassHandler();
        }
    }

    /// <summary>
    /// Handler class for class declarations.
    /// </summary>
    public class ClassHandler : BaseHandler, ITypeHandler
    {
        public ClassHandler()
        {
        }

        /// <summary>
        /// Marshals the specified class declaration.
        /// </summary>
        /// <param name="classDecl">The class declaration.</param>
        /// <param name="typeDatabase">The type database instance.</param>
        public IEnvironment Marshal(BaseDecl decl, TypeDatabase typeDatabase)
        {
            if (decl is not ClassDecl classDecl)
            {
                throw new ArgumentException("The provided decl must be a ClassDecl.", nameof(decl));
            }
            return new TypeEnvironment(classDecl, typeDatabase);
        }

        /// <summary>
        /// Emits the necessary code for the specified environment.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        /// <param name="conductor">The conductor instance.</param>
        /// <param name="typeDatabase">The type database instance.</param>
        public void Emit(IndentedTextWriter writer, IEnvironment env, Conductor conductor)
        {
            var classEnv = (TypeEnvironment)env;
            var classDecl = (ClassDecl)classEnv.TypeDecl;

            writer.WriteLine($"public unsafe class {classDecl.Name} {{");
            writer.Indent++;

            base.HandleBaseDecl(writer, classDecl.Declarations, conductor, env.TypeDatabase);

            writer.Indent--;
            writer.WriteLine("}");
        }
    }
}
