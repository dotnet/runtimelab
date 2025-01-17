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
        public IEnvironment Marshal(BaseDecl decl, ITypeDatabase typeDatabase)
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
            var typeRecord = env.TypeDatabase.GetTypeRecordOrThrow(moduleDecl.Name, structDecl.FullyQualifiedNameWithoutModule);

            var ISwiftObjectMethodWriter = new ISwiftObjectMethodWriter(writer, env.TypeDatabase, moduleDecl, structDecl);

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
            writer.WriteLine($"public unsafe struct {structDecl.Name} : {typeof(ISwiftObject).Name} {{");
            writer.Indent++;

            // Emit each field in the struct
            foreach (var fieldDecl in structDecl.Fields)
            {
                string accessModifier = fieldDecl.Visibility == Visibility.Public ? "public" : "private";
                var fieldRecord = env.TypeDatabase.GetTypeRecordOrThrow(fieldDecl.SwiftTypeSpec);
                writer.WriteLine($"{accessModifier} {fieldRecord.CSTypeIdentifier} {fieldDecl.Name};");

                // TODO: Fix memory access violation
                // // Verify field against Swift type information
                // if (swiftTypeInfo.HasValue && !VerifyFieldRecord(swiftTypeInfo.Value, structDecl.Fields.IndexOf(fieldDecl), fieldDecl))
                // {
                //     Console.WriteLine("Field record does not match the field declaration");
                // }
            }
            writer.WriteLine();

            ISwiftObjectMethodWriter.WriteFrozenStructImplementation();

            base.HandleBaseDecl(writer, structDecl.Types, conductor, env.TypeDatabase);
            base.HandleBaseDecl(writer, structDecl.Methods, conductor, env.TypeDatabase);

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
        public IEnvironment Marshal(BaseDecl decl, ITypeDatabase typeDatabase)
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
            var moduleDecl = structDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(structDecl.ModuleDecl));

            var ISwiftObjectMethodWriter = new ISwiftObjectMethodWriter(writer, env.TypeDatabase, moduleDecl, structDecl);

            writer.WriteLine($"public unsafe class {structDecl.Name} : IDisposable, {typeof(ISwiftObject).Name}");
            writer.WriteLine("{");
            writer.Indent++;

            WritePrivateFields(writer, structDecl);
            WriteDisposeMethod(writer);
            WriteFinalizer(writer, structDecl);
            WritePayloadSize(writer);
            WritePayload(writer);

            ISwiftObjectMethodWriter.WriteNonFrozenStructImplementation();

            writer.WriteLine();

            base.HandleBaseDecl(writer, structDecl.Types, conductor, env.TypeDatabase);
            base.HandleBaseDecl(writer, structDecl.Methods, conductor, env.TypeDatabase);

            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }

        /// <summary>
        /// Writes the private fields for the class.
        /// </summary>
        private static void WritePrivateFields(IndentedTextWriter writer, StructDecl structDecl)
        {
            writer.WriteLine($"static nuint _payloadSize = SwiftObjectHelper<{structDecl.Name}>.GetTypeMetadata().Size;");
            writer.WriteLine("SwiftHandle _payload = SwiftHandle.Zero;");
            writer.WriteLine("bool _disposed = false;");
            writer.WriteLine();
        }

        /// <summary>
        /// Writes the Dispose method for the class.
        /// </summary>
        private static void WriteDisposeMethod(IndentedTextWriter writer)
        {
            var text = $$"""
            public void Dispose()
            {
                if (!_disposed)
                {
                    NativeMemory.Free((void*)_payload);
                    _payload = SwiftHandle.Zero;
                    _disposed = true;
                    GC.SuppressFinalize(this);
                }
            }
            """;

            writer.WriteLines(text);
            writer.WriteLine();
        }

        /// <summary>
        /// Writes the finalizer for the class.
        /// </summary>
        private static void WriteFinalizer(IndentedTextWriter writer, StructDecl structDecl)
        {
            var text = $$"""
            ~{{structDecl.Name}}()
            {
                NativeMemory.Free((void*)_payload);
                _payload = SwiftHandle.Zero;
            }
            """;

            writer.WriteLines(text);
            writer.WriteLine();
        }

        /// <summary>
        /// Writes the payload size accessor for the class.
        /// </summary>
        private static void WritePayloadSize(IndentedTextWriter writer)
        {
            writer.WriteLine("public static nuint PayloadSize => _payloadSize;");
            writer.WriteLine();
        }

        /// <summary>
        /// Writes the payload accessor for the class.
        /// </summary>
        private static void WritePayload(IndentedTextWriter writer)
        {
            writer.WriteLine("public SwiftHandle Payload => _payload;");
            writer.WriteLine();
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
        public IEnvironment Marshal(BaseDecl decl, ITypeDatabase typeDatabase)
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

            base.HandleBaseDecl(writer, classDecl.Types, conductor, env.TypeDatabase);
            base.HandleBaseDecl(writer, classDecl.Methods, conductor, env.TypeDatabase);

            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }
    }

    /// <summary>
    /// Class responsible for emitting the necessary code for ISwiftObject methods.
    /// </summary>
    class ISwiftObjectMethodWriter
    {
        private readonly IndentedTextWriter _writer;
        private readonly ITypeDatabase _typeDatabase;
        private readonly ModuleDecl _moduleDecl;
        private readonly StructDecl _structDecl;

        public ISwiftObjectMethodWriter(IndentedTextWriter writer, ITypeDatabase typeDatabase, ModuleDecl moduleDecl, StructDecl structDecl)
        {
            _writer = writer;
            _typeDatabase = typeDatabase;
            _moduleDecl = moduleDecl;
            _structDecl = structDecl;
        }

        /// <summary>
        /// Writes the implementation for ISwiftObject methods for non-frozen structs.
        /// </summary>
        public void WriteNonFrozenStructImplementation()
        {
            WriteGetTypeMetadata();
            WriteNewFromPayloadNonFrozenStruct();
            WriteMarshalToSwift();
        }

        /// <summary>
        /// Writes the implementation for ISwiftObject methods for frozen structs.
        /// </summary>
        public void WriteFrozenStructImplementation()
        {
            WriteGetTypeMetadata();
            WriteNewFromPayloadFrozenStruct();
            WriteMarshalToSwift();
        }

        /// <summary>
        /// Writes the GetTypeMetadata method for the struct along with the PInvoke method.
        /// </summary>
        private void WriteGetTypeMetadata()
        {
            _writer.WriteLine("static TypeMetadata ISwiftObject.GetTypeMetadata() => PInvoke_getMetadata();");
            _writer.WriteLine();

            string libPath = _typeDatabase.GetLibraryPath(_moduleDecl.Name);

            var pinvokeText = $$"""
            [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
            [DllImport("{{libPath}}", EntryPoint = "{{_structDecl.MangledName}}Ma")]
            internal static extern TypeMetadata PInvoke_getMetadata();
            """;

            _writer.WriteLines(pinvokeText);
            _writer.WriteLine();
        }

        /// <summary>
        /// Writes the NewFromPayload method for the struct.
        /// </summary>
        private void WriteNewFromPayloadFrozenStruct()
        {
            var text = $$"""
            static ISwiftObject ISwiftObject.NewFromPayload(SwiftHandle handle)
            {
                return *({{_structDecl.Name}}*)handle;
            }
            """;

            _writer.WriteLines(text);
            _writer.WriteLine();
        }

        /// <summary>
        /// Writes the NewFromPayload method for the struct.
        /// </summary>
        private void WriteNewFromPayloadNonFrozenStruct()
        {
            var text = $$"""
            static ISwiftObject ISwiftObject.NewFromPayload(SwiftHandle handle)
            {
                return new {{_structDecl.Name}}(handle);
            }
            """;

            _writer.WriteLines(text);
            _writer.WriteLine();

            EmitPrivateConstructor();
        }

        /// <summary>
        /// Writes the private constructor accepting a SwiftHandle.
        /// </summary>
        private void EmitPrivateConstructor()
        {
            var text = $$"""
            unsafe {{_structDecl.Name}}(SwiftHandle handle)
            {
                _payload = handle;
            }
            """;

            _writer.WriteLines(text);
            _writer.WriteLine();
        }

        /// <summary>
        /// Writes the MarshalToSwift method for the struct.
        /// </summary>
        private void WriteMarshalToSwift()
        {
            _writer.WriteLine("IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest) => throw new NotImplementedException();");
            _writer.WriteLine();
        }
    }
}
