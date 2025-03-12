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
            return decl is StructDecl structDecl && structDecl.IsFrozen;
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

        /// <inheritdoc/>
        public IEnvironment Marshal(BaseDecl baseDecl, ITypeDatabase typeDatabase)
        {
            if (baseDecl is not StructDecl structDecl)
            {
                throw new ArgumentException("The provided decl must be a StructDecl.", nameof(baseDecl));
            }
            return new TypeEnvironment(structDecl, typeDatabase);
        }

        /// <inheritdoc/>
        public void Emit(CSharpWriter csWriter, SwiftWriter swiftWriter, IEnvironment env, Conductor conductor)
        {
            var structEnv = (TypeEnvironment)env;
            var structDecl = (StructDecl)structEnv.TypeDecl;
            var parentDecl = structDecl.ParentDecl ?? throw new ArgumentNullException(nameof(structDecl.ParentDecl));
            var moduleDecl = structDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(structDecl.ParentDecl));
            // Retrieve type info from the type database
            var typeRecord = env.TypeDatabase.GetTypeRecordOrThrow(structDecl.SwiftTypeName);

            var ISwiftObjectMethodWriter = new ISwiftObjectMethodWriter(csWriter, env.TypeDatabase, moduleDecl, structDecl);

            SwiftTypeInfo? swiftTypeInfo = typeRecord?.SwiftTypeInfo;
            bool isProjectedAsClass = MarshallingHelpers.IsFrozenStructProjectedAsClass(structDecl, env.TypeDatabase);

            if (isProjectedAsClass)
            {
                csWriter.WriteLine($"public class {structDecl.Name} : IDisposable, {typeof(ISwiftObject).Name} {{");
                csWriter.Indent++;
            }

            if (swiftTypeInfo.HasValue)
            {
                unsafe
                {
                    // Apply struct layout attributes
                    // TODO: refactor to use type metadata
                    csWriter.WriteLine($"[StructLayout(LayoutKind.Sequential, Size = {swiftTypeInfo.Value.ValueWitnessTable->Size})]");
                }
            }
            if (isProjectedAsClass)
            {
                csWriter.WriteLine($"public unsafe struct Buffer {{");
            }
            else
            {
                csWriter.WriteLine($"public unsafe struct {structDecl.Name} : {typeof(ISwiftObject).Name} {{");
            }
            csWriter.Indent++;

            csWriter.WriteLine(@"
            // For frozen structs, we need to emit fields that match the Swift struct's memory layout exactly.
            // These backing fields are required for proper memory layout and marshalling, even though they
            // are never directly accessed from C# code. The actual value access happens through Swift's
            // accessor methods.
            //
            // Important: Direct access to these fields from C# will not provide the correct value - always
            // use the generated property accessors which call into Swift.");

            foreach (PropertyDecl propertyDecl in structDecl.Properties)
            {
                if (propertyDecl.HasStorage)
                {
                    var fieldRecord = env.TypeDatabase.GetTypeRecordOrThrow(propertyDecl.SwiftTypeSpec);
                    if ((fieldRecord.Flags & TypeRecordFlags.HeapAllocated) != 0)
                    {
                        csWriter.WriteLine($"private IntPtr {propertyDecl.Name}_;  // Note: Do not access this field directly - use the property accessors");
                    }
                    else
                    {
                        csWriter.WriteLine($"private {fieldRecord.CSharpTypeName.FullyQualifiedName} {propertyDecl.Name}_;  // Note: Do not access this field directly - use the property accessors");
                    }
                }
            }

            if (isProjectedAsClass)
            {
                csWriter.Indent -= 2;
                csWriter.WriteLine("}");
                csWriter.WriteLine();
                csWriter.WriteLine("private Buffer _payload;");
                csWriter.WriteLine();
                csWriter.WriteLine("private int _disposed;");
                csWriter.WriteLine();
                csWriter.WriteLine("public Buffer Payload => _payload;");
                csWriter.WriteLine();

                WriteDisposeMethod(csWriter, structDecl);
                WriteFinalizer(csWriter, structDecl);
            }

            foreach (PropertyDecl propertyDecl in structDecl.Properties)
            {
                if (conductor.TryGetPropertyHandler(propertyDecl, out var propertyHandler))
                {
                    var propertyEnv = propertyHandler.Marshal(propertyDecl, env.TypeDatabase);
                    propertyHandler.Emit(csWriter, swiftWriter, propertyEnv, conductor);
                }
                else
                {
                    throw new InvalidOperationException($"No handler found for property {propertyDecl.Name}");
                }
            }
            csWriter.WriteLine();

            ISwiftObjectMethodWriter.WriteFrozenStructImplementation();

            base.HandleBaseDecl(csWriter, swiftWriter, structDecl.Types, conductor, env.TypeDatabase);
            base.HandleBaseDecl(csWriter, swiftWriter, structDecl.Methods, conductor, env.TypeDatabase);

            csWriter.Indent--;
            csWriter.WriteLine("}");
        }

        /// <summary>
        /// Writes the Dispose method for the class.
        /// </summary>
        private static void WriteDisposeMethod(CSharpWriter csWriter, StructDecl structDecl)
        {
            var text = $$"""
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                {
                    var metadata = SwiftObjectHelper<{{structDecl.Name}}>.GetTypeMetadata();
                    unsafe {
                        fixed (void* payload = &_payload)
                        {
                            metadata.ValueWitnessTable->Destroy((void *)payload, metadata);
                        }
                    }
                    _disposed = 1;
                }
            }
            """;

            csWriter.WriteLines(text);
            csWriter.WriteLine();
        }

        /// <summary>
        /// Writes the finalizer for the class.
        /// </summary>
        private static void WriteFinalizer(CSharpWriter csWriter, StructDecl structDecl)
        {
            var text = $$"""
            ~{{structDecl.Name}}()
            {
                Dispose(disposing: false);
            }
            """;

            csWriter.WriteLines(text);
            csWriter.WriteLine();
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
            return decl is StructDecl structDecl && !structDecl.IsFrozen;
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

        /// <inheritdoc/>
        public IEnvironment Marshal(BaseDecl baseDecl, ITypeDatabase typeDatabase)
        {
            if (baseDecl is not StructDecl structDecl)
            {
                throw new ArgumentException("The provided decl must be a StructDecl.", nameof(baseDecl));

            }
            return new TypeEnvironment(structDecl, typeDatabase);
        }

        /// <inheritdoc/>
        public void Emit(CSharpWriter csWriter, SwiftWriter swiftWriter, IEnvironment env, Conductor conductor)
        {
            var structEnv = (TypeEnvironment)env;
            var structDecl = (StructDecl)structEnv.TypeDecl;
            var moduleDecl = structDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(structDecl.ModuleDecl));

            var ISwiftObjectMethodWriter = new ISwiftObjectMethodWriter(csWriter, env.TypeDatabase, moduleDecl, structDecl);

            csWriter.WriteLine($"public unsafe class {structDecl.Name} : IDisposable, {typeof(ISwiftObject).Name}");
            csWriter.WriteLine("{");
            csWriter.Indent++;

            foreach (PropertyDecl propertyDecl in structDecl.Properties)
            {
                if (conductor.TryGetPropertyHandler(propertyDecl, out var propertyHandler))
                {
                    var propertyEnv = propertyHandler.Marshal(propertyDecl, env.TypeDatabase);
                    propertyHandler.Emit(csWriter, swiftWriter, propertyEnv, conductor);
                }
                else
                    Console.WriteLine($"No handler found for field {propertyDecl.Name}");
            }

            WritePrivateFields(csWriter, structDecl);
            WriteDisposeMethod(csWriter, structDecl);
            WriteFinalizer(csWriter, structDecl);
            WritePayloadSize(csWriter);
            WritePayload(csWriter);

            ISwiftObjectMethodWriter.WriteNonFrozenStructImplementation();

            csWriter.WriteLine();

            base.HandleBaseDecl(csWriter, swiftWriter, structDecl.Types, conductor, env.TypeDatabase);
            base.HandleBaseDecl(csWriter, swiftWriter, structDecl.Methods, conductor, env.TypeDatabase);

            csWriter.Indent--;
            csWriter.WriteLine("}");
            csWriter.WriteLine();
        }

        /// <summary>
        /// Writes the private fields for the class.
        /// </summary>
        private static void WritePrivateFields(CSharpWriter csWriter, StructDecl structDecl)
        {
            csWriter.WriteLine($"static nuint _payloadSize = SwiftObjectHelper<{structDecl.Name}>.GetTypeMetadata().Size;");
            csWriter.WriteLine("SwiftHandle _payload = SwiftHandle.Zero;");
            csWriter.WriteLine("private int _disposed = 0;");
            csWriter.WriteLine();
        }

        /// <summary>
        /// Writes the Dispose method for the class.
        /// </summary>
        private static void WriteDisposeMethod(CSharpWriter csWriter, StructDecl structDecl)
        {
            var text = $$"""
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                {
                    var metadata = SwiftObjectHelper<{{structDecl.Name}}>.GetTypeMetadata();
                    unsafe {
                        metadata.ValueWitnessTable->Destroy((void *)_payload, metadata);
                    }
                    _payload = SwiftHandle.Zero;
                    _disposed = 1;
                }
            }
            """;

            csWriter.WriteLines(text);
            csWriter.WriteLine();
        }

        /// <summary>
        /// Writes the finalizer for the class.
        /// </summary>
        private static void WriteFinalizer(CSharpWriter csWriter, StructDecl structDecl)
        {
            var text = $$"""
            ~{{structDecl.Name}}()
            {
                Dispose(disposing: false);
            }
            """;

            csWriter.WriteLines(text);
            csWriter.WriteLine();
        }

        /// <summary>
        /// Writes the payload size accessor for the class.
        /// </summary>
        private static void WritePayloadSize(CSharpWriter csWriter)
        {
            csWriter.WriteLine("public static nuint PayloadSize => _payloadSize;");
            csWriter.WriteLine();
        }

        /// <summary>
        /// Writes the payload accessor for the class.
        /// </summary>
        private static void WritePayload(CSharpWriter csWriter)
        {
            csWriter.WriteLine("public SwiftHandle Payload => _payload;");
            csWriter.WriteLine();
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

        /// <inheritdoc/>
        public IEnvironment Marshal(BaseDecl baseDecl, ITypeDatabase typeDatabase)
        {
            if (baseDecl is not ClassDecl classDecl)
            {
                throw new ArgumentException("The provided decl must be a ClassDecl.", nameof(baseDecl));
            }
            return new TypeEnvironment(classDecl, typeDatabase);
        }

        /// <inheritdoc/>
        public void Emit(CSharpWriter csWriter, SwiftWriter swiftWriter, IEnvironment env, Conductor conductor)
        {
            var classEnv = (TypeEnvironment)env;
            var classDecl = (ClassDecl)classEnv.TypeDecl;

            csWriter.WriteLine($"public unsafe class {classDecl.Name} {{");
            csWriter.Indent++;


            csWriter.WriteLine("SwiftHandle _payload = SwiftHandle.Zero;");
            csWriter.WriteLine();
            csWriter.WriteLine("public SwiftHandle Payload => _payload;");
            csWriter.WriteLine();

            base.HandleBaseDecl(csWriter, swiftWriter, classDecl.Types, conductor, env.TypeDatabase);
            base.HandleBaseDecl(csWriter, swiftWriter, classDecl.Methods, conductor, env.TypeDatabase);

            csWriter.Indent--;
            csWriter.WriteLine("}");
            csWriter.WriteLine();
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

        public ISwiftObjectMethodWriter(CSharpWriter csWriter, ITypeDatabase typeDatabase, ModuleDecl moduleDecl, StructDecl structDecl)
        {
            _writer = csWriter;
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
            WriteMarshalToSwiftNonFrozenStruct();
            WriteGetProtocolConformanceDescriptor();
        }

        /// <summary>
        /// Writes the implementation for ISwiftObject methods for frozen structs.
        /// </summary>
        public void WriteFrozenStructImplementation()
        {
            WriteGetTypeMetadata();
            WriteNewFromPayloadFrozenStruct();
            WriteMarshalToSwiftFrozenStruct();
            WriteGetProtocolConformanceDescriptor();
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
            if (MarshallingHelpers.IsFrozenStructProjectedAsClass(_structDecl, _typeDatabase))
            {
                var text = $$"""
                static unsafe ISwiftObject ISwiftObject.NewFromPayload(SwiftHandle handle)
                {
                    return new {{_structDecl.Name}} { _payload = *(Buffer*)handle };
                }

                private {{_structDecl.Name}} ()
                {
                }
                """;

                _writer.WriteLines(text);
                _writer.WriteLine();
            }
            else
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
        private void WriteMarshalToSwiftFrozenStruct()
        {
            string payloadName = "this";
            if (MarshallingHelpers.IsFrozenStructProjectedAsClass(_structDecl, _typeDatabase))
            {
                payloadName = "_payload";
            }

            var text = $$"""
            IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest)
            {
                var metadata = SwiftObjectHelper<{{_structDecl.Name}}>.GetTypeMetadata();
                unsafe {
                    fixed (void* payload = &{{payloadName}})
                    {
                    metadata.ValueWitnessTable->InitializeWithCopy((void *)swiftDest, payload, metadata);
                    }
                }
                return swiftDest;
            }
            """;

            _writer.WriteLines(text);
            _writer.WriteLine();
        }

        /// <summary>
        /// Writes the MarshalToSwift method for the struct.
        /// </summary>
        private void WriteMarshalToSwiftNonFrozenStruct()
        {
            var text = $$"""
            IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest)
            {
                var metadata = SwiftObjectHelper<{{_structDecl.Name}}>.GetTypeMetadata();
                unsafe {
                    metadata.ValueWitnessTable->InitializeWithCopy((void *)swiftDest, (void *)_payload, metadata);
                }
                return swiftDest;
            }
            """;

            _writer.WriteLines(text);
            _writer.WriteLine();
        }

        /// <summary>
        /// Writes the GetProtocolConformanceDescriptor method for the struct.
        /// </summary>
        private void WriteGetProtocolConformanceDescriptor()
        {
            WriteStaticConstructor();
            var libPath = _typeDatabase.GetLibraryPath(_moduleDecl.Name);
            var text = $$"""
            static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>()
                where TProtocol : class
            {
                if (!_protocolConformanceSymbols.TryGetValue(typeof(TProtocol), out var symbolName))
                {
                    throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type {{_structDecl.Name}} and protocol {typeof(TProtocol).Name}, but no conformance was found.");
                }

                return ProtocolConformanceDescriptor.LoadFromSymbol("{{libPath}}", symbolName);
            }
            """;

            _writer.WriteLines(text);
            _writer.WriteLine();
        }

        /// <summary>
        /// Writes the static constructor for the struct.
        /// </summary>
        private void WriteStaticConstructor()
        {
            var text = $$"""
            private static Dictionary<Type, string> _protocolConformanceSymbols;

            static {{_structDecl.Name}}()
            {
                _protocolConformanceSymbols = new Dictionary<Type, string>
                {
                    {{GenerateGetProtocolConformanceDictionaryEntries()}}
                };
            }
            """;

            _writer.WriteLines(text);
            _writer.WriteLine();
        }

        private string GenerateGetProtocolConformanceDictionaryEntries()
        {
            var libPath = _typeDatabase.GetLibraryPath(_moduleDecl.Name);
            var entries = new List<string>();
            var protocolConformanceDescriptors = DemangledSymbolsRegister.Instance.GetData(libPath).ProtocolConformanceDescriptors;

            foreach (var conformance in _structDecl.Conformances.Where(c => c.Protocol.Module == _moduleDecl.Name)) // Process only protocol conformances from current module for now
            {
                var protocol = NameProvider.GetInterfaceName(conformance.Protocol.Name);
                var typeRecord = _typeDatabase.GetTypeRecordOrThrow(_structDecl.SwiftTypeName);
                var protocolConformanceSymbol = protocolConformanceDescriptors.GetValueOrDefault((_structDecl.SwiftTypeName, conformance.Protocol)); // TODO: Get rid of TypeSpec https://github.com/dotnet/runtimelab/issues/2889

                entries.Add($"{{typeof({protocol}), \"{protocolConformanceSymbol}\"}}");
            }

            return string.Join(",\n", entries);
        }
    }

    /// <summary>
    /// Factory class for creating instances of ProtocolHandler.
    /// </summary>
    public class ProtocolHandlerFactory : IFactory<BaseDecl, ITypeHandler>
    {
        /// <summary>
        /// Determines if the factory handles the specified declaration.
        /// </summary>
        /// <param name="decl">The base declaration.</param>
        public bool Handles(BaseDecl decl)
        {
            return decl is ProtocolDecl;
        }

        /// <summary>
        /// Constructs a new instance of ProtocolHandler.
        /// </summary>
        public ITypeHandler Construct()
        {
            return new ProtocolHandler();
        }
    }

    /// <summary>
    /// Handler class for protocol declarations.
    /// </summary>
    public class ProtocolHandler : BaseHandler, ITypeHandler
    {
        public ProtocolHandler()
        {
        }

        /// <inheritdoc/>
        public IEnvironment Marshal(BaseDecl baseDecl, ITypeDatabase typeDatabase)
        {
            if (baseDecl is not ProtocolDecl protocolDecl)
            {
                throw new ArgumentException("The provided decl must be a ProtocolDecl.", nameof(baseDecl));
            }
            return new TypeEnvironment(protocolDecl, typeDatabase);
        }

        /// <inheritdoc/>
        public void Emit(CSharpWriter csWriter, SwiftWriter swiftWriter, IEnvironment env, Conductor conductor)
        {
            var protocolEnv = (TypeEnvironment)env;
            var protocolDecl = (ProtocolDecl)protocolEnv.TypeDecl;

            var interfaceName = NameProvider.GetInterfaceName(protocolDecl.Name);

            csWriter.WriteLine($"public interface {interfaceName}");
            csWriter.WriteLine("{");
            csWriter.Indent++;

            // TODO: Implement protocol methods and properties
            // base.HandleBaseDecl(writer, protocolDecl.Types, conductor, env.TypeDatabase);
            // base.HandleBaseDecl(writer, protocolDecl.Methods, conductor, env.TypeDatabase);

            csWriter.Indent--;
            csWriter.WriteLine("}");
            csWriter.WriteLine();
        }
    }
}
