// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;

namespace BindingsGeneration
{

    /// <summary>
    /// Represents a method handler factory.
    /// </summary>
    public class MethodHandlerFactory : IFactory<BaseDecl, IMethodHandler>
    {
        /// <summary>
        /// Checks if the factory can handle the declaration.
        /// </summary>
        /// <param name="decl">The base declaration.</param>
        /// <returns></returns>
        public bool Handles(BaseDecl decl)
        {
            return decl is MethodDecl;
        }

        /// <summary>
        /// Constructs a handler.
        /// </summary>
        public IMethodHandler Construct()
        {
            return new MethodHandler();
        }
    }

    /// <summary>
    /// Represents a method handler.
    /// </summary>
    public class MethodHandler : BaseHandler, IMethodHandler
    {
        public MethodHandler()
        {
        }

        /// <summary>
        /// Marshals the method declaration.
        /// </summary>
        /// <param name="methodDecl">The method declaration.</param>
        /// <param name="typeDatabase">The type database instance.</param>
        public IEnvironment Marshal(BaseDecl decl, ITypeDatabase typeDatabase)
        {
            if (decl is not MethodDecl methodDecl)
            {
                throw new ArgumentException("The provided decl must be a MethodDecl.", nameof(decl));
            }
            return new MethodEnvironment(methodDecl, typeDatabase);
        }

        /// <summary>
        /// Emits the method declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        /// <param name="conductor">The conductor instance.</param>
        public void Emit(IndentedTextWriter writer, IEnvironment env, Conductor conductor)
        {
            var methodEnv = (MethodEnvironment)env;
            var signatureHandler = new SignatureHandler(methodEnv);

            if (methodEnv.MethodDecl.GenericParameters.Any(x => x.Constraints.Count > 0))
            {
                Console.WriteLine($"Method {methodEnv.MethodDecl.Name} has unsupported generic constraints");
                return;
            }

            if (signatureHandler.GetWrapperSignature().ContainsPlaceholder)
            {
                Console.WriteLine($"Method {methodEnv.MethodDecl.Name} has unsupported signature: ({signatureHandler.GetWrapperSignature().ParametersString()}) -> {signatureHandler.GetWrapperSignature().ReturnType}");
                return;
            }

            var wrapperEmitter = new WrapperEmitter(methodEnv, signatureHandler);
            wrapperEmitter.Emit(writer);
            PInvokeEmitter.EmitPInvoke(writer, methodEnv, signatureHandler);
            writer.WriteLine();
        }
    }

    /// <summary>
    /// Represents a parameter.
    /// </summary>
    /// <param name="Type"></param>
    /// <param name="Name"></param>
    public record Parameter(string Type, string Name, string modifier = "")
    {
        public string CallString() => $"{Type} {Name}";
        public string SignatureString() => $"{modifier} {Type} {Name}";
    }

    /// <summary>
    /// Represents a signature.
    /// </summary>
    /// <param name="ReturnType"></param>
    /// <param name="Parameters"></param>
    public record Signature(string ReturnType, IReadOnlyList<Parameter> Parameters)
    {
        public bool ContainsPlaceholder => Parameters.Any(p => p.Type == "AnyType") || ReturnType == "AnyType";
        public string ParametersString() => string.Join(", ", Parameters.Select(p => p.SignatureString()));

        public string CallArgumentsString() => string.Join(", ", Parameters.Select(p => GetCallArgumentString(p)));

        public static string GetCallArgumentString(Parameter parameter)
        {
            return parameter switch
            {
                { Type: "SwiftHandle" } => $"{parameter.Name}.Payload",
                { modifier: "out" } => $"out var {parameter.Name}",
                _ => parameter.Name
            };
        }
    }

    public class WrapperSignatureBuilder
    {
        private string _returnType = "invalid";
        private readonly List<Parameter> _parameters = new();
        private readonly MethodEnvironment _env;

        public WrapperSignatureBuilder(MethodEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Handles the return type of the method.
        /// </summary>
        public void HandleReturnType()
        {
            var argument = _env.MethodDecl.CSSignature.First();
            if (argument.IsGeneric)
            {
                var placeholderName = _env.GenericTypeMapping[argument.SwiftTypeSpec.ToString()].PlaceholderName;
                SetReturnType(placeholderName);
            }
            else
            {
                var typeRecord = _env.TypeDatabase.GetTypeRecordOrAnyType(argument.SwiftTypeSpec);
                SetReturnType(typeRecord.CSTypeIdentifier);
            }
        }

        /// <summary>
        /// Handles the arguments of the method.
        /// </summary>
        public void HandleArguments()
        {
            foreach (var argument in _env.MethodDecl.CSSignature.Skip(1))
            {
                if (argument.IsGeneric)
                {
                    var placeholderName = _env.GenericTypeMapping[argument.SwiftTypeSpec.ToString()].PlaceholderName;
                    AddParameter(placeholderName, argument.Name);
                }
                else
                {
                    var typeRecord = _env.TypeDatabase.GetTypeRecordOrAnyType(argument.SwiftTypeSpec);
                    AddParameter(typeRecord.CSTypeIdentifier, argument.Name);
                }
            }
        }

        /// <summary>
        /// Builds the PInvoke signature.
        /// </summary>
        /// <returns>The PInvoke signature.</returns>
        public Signature Build()
        {
            return new Signature(_returnType, _parameters.ToArray());
        }


        /// <summary>
        /// Sets the return type of the method.
        /// </summary>
        /// <param name="returnType">The return type.</param>
        private void SetReturnType(string returnType)
        {
            _returnType = returnType;
        }

        /// <summary>
        /// Adds a parameter to the PInvoke signature.
        /// </summary>
        /// <param name="type">The parameter type.</param>
        /// <param name="name">The parameter name.</param>s
        private void AddParameter(string type, string name)
        {
            _parameters.Add(new Parameter(type, name));
        }
    }

    /// <summary>
    /// Represents a PInvoke signature builder.
    /// </summary>
    public class PInvokeSignatureBuilder
    {
        private string _returnType = "invalid";
        private readonly List<Parameter> _parameters = new();
        private readonly MethodEnvironment _env;

        /// <summary>
        /// Initializes a new instance of the <see cref="PInvokeSignatureBuilder"/> class.
        /// </summary>
        /// <param name="methodDecl">The method declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="typeDatabase">The type database.</param>
        public PInvokeSignatureBuilder(MethodEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Handles the return type of the method.
        /// </summary>
        public void HandleReturnType()
        {
            if (!MarshallingHelpers.MethodRequiresIndirectResult(_env))
            {
                var returnTypeRecord = _env.TypeDatabase.GetTypeRecordOrThrow(_env.MethodDecl.CSSignature.First().SwiftTypeSpec);
                SetReturnType(returnTypeRecord.CSTypeIdentifier);
            }
            else
            {
                AddParameter("SwiftIndirectResult", "swiftIndirectResult");
                SetReturnType("void");
            }
        }

        /// <summary>
        /// Handles the arguments of the method.
        /// </summary>
        public void HandleArguments()
        {
            foreach (var argument in _env.MethodDecl.CSSignature.Skip(1))
            {
                if (argument.IsGeneric)
                {
                    var payloadName = _env.GenericTypeMapping[argument.SwiftTypeSpec.ToString()].PayloadName;
                    AddParameter("IntPtr", payloadName);
                }
                else if (MarshallingHelpers.ArgumentIsMarshalledAsCSStruct(argument, _env.TypeDatabase))
                {
                    var argumentTypeRecord = _env.TypeDatabase.GetTypeRecordOrThrow(argument.SwiftTypeSpec);
                    AddParameter(argumentTypeRecord.CSTypeIdentifier, argument.Name);
                }
                else
                {
                    AddParameter("SwiftHandle", argument.Name);
                }
            }
        }

        /// <summary>
        /// Handles the metadata of generic arguments.
        /// </summary>
        public void HandleGenericMetadata()
        {
            foreach (var genericParameter in _env.MethodDecl.GenericParameters)
            {
                var metadataName = _env.GenericTypeMapping[genericParameter.TypeName].MetadataName;
                AddParameter("TypeMetadata", metadataName);
            }
        }

        /// <summary>
        /// Handles the SwiftSelf parameter of the method.
        /// </summary>
        public void HandleSwiftSelf()
        {
            if (MarshallingHelpers.MethodRequiresSwiftSelf(_env))
            {
                if (_env.ParentDecl is StructDecl structDecl && MarshallingHelpers.StructIsMarshalledAsCSStruct(structDecl))
                {
                    AddParameter($"SwiftSelf<{_env.ParentDecl.Name}>", "self");
                }
                else
                {
                    AddParameter("SwiftSelf", "self");
                }
            }
        }

        /// <summary>
        /// Handles the SwiftError parameter of the method.
        /// </summary>
        public void HandleSwiftError()
        {
            if (_env.MethodDecl.Throws)
            {
                AddParameter("SwiftError", "error", "out");
            }
        }

        /// <summary>
        /// Builds the PInvoke signature.
        /// </summary>
        /// <returns>The PInvoke signature.</returns>
        public Signature Build()
        {
            return new Signature(_returnType, _parameters.ToArray());
        }

        /// <summary>
        /// Sets the return type of the method.
        /// </summary>
        /// <param name="returnType">The return type.</param>
        private void SetReturnType(string returnType)
        {
            _returnType = returnType;
        }

        /// <summary>
        /// Adds a parameter to the PInvoke signature.
        /// </summary>
        /// <param name="type">The parameter type.</param>
        /// <param name="name">The parameter name.</param>s
        private void AddParameter(string type, string name, string modifier = "")
        {
            _parameters.Add(new Parameter(type, name, modifier));
        }
    }

    /// <summary>
    /// Provides methods for handling method signatures.
    /// </summary>
    public class SignatureHandler
    {
        private Signature? _pInvokeSignature;
        private Signature? _wrapperSignature;
        private readonly MethodEnvironment _env;

        public SignatureHandler(MethodEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Gets the PInvoke signature.
        /// </summary>
        /// <returns>The PInvoke signature.</returns>
        public Signature GetPInvokeSignature()
        {
            if (_pInvokeSignature == null)
            {
                var pInvokeSignature = new PInvokeSignatureBuilder(_env);
                pInvokeSignature.HandleReturnType();
                pInvokeSignature.HandleArguments();
                pInvokeSignature.HandleGenericMetadata();
                pInvokeSignature.HandleSwiftSelf();
                pInvokeSignature.HandleSwiftError();
                _pInvokeSignature = pInvokeSignature.Build();
            }
            return _pInvokeSignature;
        }

        /// <summary>
        /// Gets the wrapper method signature.
        /// </summary>
        /// <returns>The wrapper method signature.</returns>
        public Signature GetWrapperSignature()
        {
            if (_wrapperSignature == null)
            {
                var wrapperSignature = new WrapperSignatureBuilder(_env);
                wrapperSignature.HandleReturnType();
                wrapperSignature.HandleArguments();
                _wrapperSignature = wrapperSignature.Build();
            }
            return _wrapperSignature;
        }
    }

    /// <summary>
    /// Provides methods for emitting PInvoke signatures.
    /// </summary>
    internal static class PInvokeEmitter
    {
        /// <summary>
        /// Emits the PInvoke signature.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="methodEnv">The method environment.</param>
        public static void EmitPInvoke(IndentedTextWriter writer, MethodEnvironment methodEnv, SignatureHandler signatureHandler)
        {
            var methodDecl = (MethodDecl)methodEnv.MethodDecl;
            var moduleDecl = methodDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(methodDecl.ModuleDecl));

            var pInvokeName = NameProvider.GetPInvokeName(methodDecl);
            var libPath = methodEnv.TypeDatabase.GetLibraryPath(moduleDecl.Name);

            writer.WriteLine("[UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]");
            writer.WriteLine($"[DllImport(\"{libPath}\", EntryPoint = \"{methodDecl.MangledName}\")]");

            var pInvokeSignature = signatureHandler.GetPInvokeSignature();

            writer.WriteLine($"private static extern {pInvokeSignature.ReturnType} {pInvokeName}({pInvokeSignature.ParametersString()});");
        }
    }

    /// <summary>
    /// Provides methods for emitting wrappers.
    /// </summary>

    internal class WrapperEmitter
    {
        private readonly MethodEnvironment _env;
        private readonly Signature _wrapperSignature;
        private readonly Signature _pInvokeSignature;
        private readonly bool _requiresIndirectResult;
        private readonly bool _requiresSwiftSelf;
        private readonly bool _requiresSwiftError;

        internal WrapperEmitter(MethodEnvironment methodEnv, SignatureHandler signatureHandler)
        {
            _env = methodEnv;

            _wrapperSignature = signatureHandler.GetWrapperSignature();
            _pInvokeSignature = signatureHandler.GetPInvokeSignature();

            _requiresIndirectResult = MarshallingHelpers.MethodRequiresIndirectResult(methodEnv);
            _requiresSwiftSelf = MarshallingHelpers.MethodRequiresSwiftSelf(methodEnv);
            _requiresSwiftError = _env.MethodDecl.Throws;
        }

        /// <summary>
        /// Emits the wrapper.
        /// </summary>
        /// <param name="writer"></param>
        internal void Emit(IndentedTextWriter writer)
        {
            if (_env.MethodDecl.IsConstructor)
            {
                EmitConstructor(writer);
            }
            else
            {
                EmitMethod(writer);
            }
        }

        /// <summary>
        /// Emits the constructor wrapper.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitConstructor(IndentedTextWriter writer)
        {
            EmitSignatureConstructor(writer);
            EmitBodyStart(writer);

            EmitDeclarationsForAllocations(writer);

            EmitTryBlockStart(writer);

            EmitSwiftSelf(writer);
            EmitIndirectResultConstructor(writer);
            EmitGenericArguments(writer);
            EmitPInvokeCall(writer);
            EmitSwiftError(writer);
            EmitReturnConstructor(writer);

            EmitTryBlockEnd(writer);

            EmitFinally(writer);

            EmitBodyEnd(writer);
        }

        /// <summary>
        /// Emits the declarations for allocations.
        /// </summary>
        private void EmitDeclarationsForAllocations(IndentedTextWriter writer)
        {
            foreach (var argument in _env.MethodDecl.CSSignature.Skip(1).Where(a => a.IsGeneric))
            {
                var (typeName, metadataName, payloadName) = _env.GenericTypeMapping[argument.SwiftTypeSpec.ToString()];
                writer.WriteLine($"IntPtr {payloadName} = IntPtr.Zero;");
            }
        }

        /// <summary>
        /// Emits the method wrapper.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitMethod(IndentedTextWriter writer)
        {
            EmitSignatureMethod(writer);
            EmitBodyStart(writer);

            EmitDeclarationsForAllocations(writer);

            EmitTryBlockStart(writer);

            EmitSwiftSelf(writer);
            EmitIndirectResultMethod(writer);
            EmitGenericArguments(writer);
            EmitPInvokeCall(writer);
            EmitSwiftError(writer);
            EmitReturnMethod(writer);

            EmitTryBlockEnd(writer);

            EmitFinally(writer);

            EmitBodyEnd(writer);
        }

        /// <summary>
        /// Emits the SwiftSelf variable.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitSwiftSelf(IndentedTextWriter writer)
        {
            if (!_requiresSwiftSelf)
            {
                return;
            }

            if (_env.ParentDecl is StructDecl structDecl && MarshallingHelpers.StructIsMarshalledAsCSStruct(structDecl))
            {
                writer.WriteLine($"var self = new SwiftSelf<{_env.ParentDecl.Name}>(this);");
            }
            else
            {
                writer.WriteLine("var self = new SwiftSelf((void*)_payload);");
            }

            writer.WriteLine();
        }

        /// <summary>
        /// Emits the IndirectResult set up in constructor context.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitIndirectResultConstructor(IndentedTextWriter writer)
        {
            if (!_requiresIndirectResult)
            {
                return;
            }

            var text = $$"""
            _payload = (SwiftHandle)NativeMemory.Alloc(_payloadSize);
            var swiftIndirectResult = new SwiftIndirectResult((void*)_payload);
            """;

            writer.WriteLines(text);
            writer.WriteLine();
        }


        /// <summary>
        /// Emits the IndirectResult set up in method context.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitIndirectResultMethod(IndentedTextWriter writer)
        {
            if (!_requiresIndirectResult)
            {
                return;
            }

            var text = $$"""
            var returnMetadata = TypeMetadata.GetTypeMetadataOrThrow<{{_wrapperSignature.ReturnType}}>();
            var payload = (SwiftHandle)NativeMemory.Alloc(returnMetadata.Size);
            var swiftIndirectResult = new SwiftIndirectResult((void*)payload);
            """;

            writer.WriteLines(text);
            writer.WriteLine();
        }

        /// <summary>
        /// Emits the generic arguments setup.
        /// </summary>
        private void EmitGenericArguments(IndentedTextWriter writer)
        {
            foreach (var argument in _env.MethodDecl.CSSignature.Skip(1).Where(a => a.IsGeneric))
            {
                var (typeName, metadataName, payloadName) = _env.GenericTypeMapping[argument.SwiftTypeSpec.ToString()];
                var text = $$"""
                var {{metadataName}} = TypeMetadata.GetTypeMetadataOrThrow<{{typeName}}>();
                {{payloadName}} = (IntPtr)NativeMemory.Alloc({{metadataName}}.Size);
                SwiftMarshal.MarshalToSwift({{argument.Name}}, {{payloadName}});
                """;
                writer.WriteLines(text);
            }
            writer.WriteLine();
        }

        /// <summary>
        /// Emits the PInvoke call.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitPInvokeCall(IndentedTextWriter writer)
        {
            var voidReturn = _env.MethodDecl.CSSignature.First().SwiftTypeSpec.IsEmptyTuple;
            var returnPrefix = (_requiresIndirectResult || voidReturn) ? "" : "var result = ";
            writer.WriteLine($"{returnPrefix}{NameProvider.GetPInvokeName(_env.MethodDecl)}({_pInvokeSignature.CallArgumentsString()});");
            writer.WriteLine();
        }

        /// <summary>
        /// Emits the SwiftError handling.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitSwiftError(IndentedTextWriter writer)
        {
            if (!_requiresSwiftError)
            {
                return;
            }

            var text = $$"""
            if (error.Value != null)
            {
                throw new SwiftRuntimeException("Call to Swift method {{_env.MethodDecl.FullyQualifiedName}} failed.");
            }
            """;

            writer.WriteLines(text);
            writer.WriteLine();
        }

        /// <summary>
        /// Emits the return statement for the constructor.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitReturnConstructor(IndentedTextWriter writer)
        {
            if (!_requiresIndirectResult)
            {
                writer.WriteLine("this = result;");
            }
        }

        /// <summary>
        /// Emits the return statement for the method.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitReturnMethod(IndentedTextWriter writer)
        {
            if (_requiresIndirectResult)
            {
                writer.WriteLine($"return SwiftMarshal.MarshalFromSwift<{_wrapperSignature.ReturnType}>((SwiftHandle)swiftIndirectResult.Value);");
            }
            else
            {
                if (_env.MethodDecl.CSSignature.First().SwiftTypeSpec.IsEmptyTuple)
                {
                    writer.WriteLine("return;");
                }
                else
                {
                    writer.WriteLine("return result;");
                }
            }
        }

        /// <summary>
        /// Emits the constructor signature.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitSignatureConstructor(IndentedTextWriter writer)
        {
            var genericParams = _env.MethodDecl.IsGeneric switch
            {
                true => $"<{string.Join(", ", _env.MethodDecl.GenericParameters.Select(p => _env.GenericTypeMapping[p.TypeName].PlaceholderName))}>",
                false => ""
            };
            writer.WriteLine($"public {_env.ParentDecl.Name}{genericParams}({_wrapperSignature.ParametersString()})");
        }

        /// <summary>
        /// Emits the method signature.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitSignatureMethod(IndentedTextWriter writer)
        {
            var genericParams = _env.MethodDecl.IsGeneric switch
            {
                true => $"<{string.Join(", ", _env.MethodDecl.GenericParameters.Select(p => _env.GenericTypeMapping[p.TypeName].PlaceholderName))}>",
                false => ""
            };

            var staticKeyword = _env.MethodDecl.MethodType == MethodType.Static || _env.ParentDecl is ModuleDecl ? "static " : "";
            var unsafeKeyword = _requiresIndirectResult || _env.MethodDecl.IsGeneric ? "unsafe " : "";

            writer.WriteLine($"public {staticKeyword}{unsafeKeyword}{_wrapperSignature.ReturnType} {_env.MethodDecl.Name}{genericParams}({_wrapperSignature.ParametersString()})");
        }

        /// <summary>
        /// Emits the finally block.
        /// </summary>
        private void EmitFinally(IndentedTextWriter writer)
        {
            writer.WriteLine("finally");
            EmitBodyStart(writer);

            foreach (var argument in _env.MethodDecl.CSSignature.Skip(1).Where(a => a.IsGeneric))
            {
                var (_, _, payloadName) = _env.GenericTypeMapping[argument.SwiftTypeSpec.ToString()];
                writer.WriteLine($"NativeMemory.Free((void*){payloadName});");
            }

            EmitBodyEnd(writer);
        }

        /// <summary>
        /// Emits the try block start.
        /// </summary>
        private void EmitTryBlockStart(IndentedTextWriter writer)
        {
            writer.WriteLine("try");
            EmitBodyStart(writer);
        }

        /// <summary>
        /// Emits the try block end.
        /// </summary>
        private void EmitTryBlockEnd(IndentedTextWriter writer)
        {
            EmitBodyEnd(writer);
        }

        /// <summary>
        /// Emits the body start.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitBodyStart(IndentedTextWriter writer)
        {
            writer.WriteLine("{");
            writer.Indent++;
        }

        /// <summary>
        /// Emits the body end.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitBodyEnd(IndentedTextWriter writer)
        {
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }
    }
}
