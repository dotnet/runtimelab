// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;
using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Factory class for creating instances of ConstructorHandler.
    /// </summary>
    public class ConstructorHandlerFactory : IFactory<BaseDecl, IMethodHandler>
    {
        /// <summary>
        /// Determines if the factory handles the specified declaration.
        /// </summary>
        /// <param name="decl">The base declaration.</param>
        public bool Handles(BaseDecl decl)
        {
            return decl is MethodDecl methodDecl && methodDecl.IsConstructor;
        }

        /// <summary>
        /// Constructs a new instance of ConstructorHandler.
        /// </summary>
        public IMethodHandler Construct()
        {
            return new ConstructorHandler();
        }
    }

    /// <summary>
    /// Handler class for constructor declarations.
    /// </summary>
    public class ConstructorHandler : BaseHandler, IMethodHandler
    {
        public ConstructorHandler()
        {
        }

        /// <summary>
        /// Marshals the specified constructor.
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
        /// Emits the code for the specified environment.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        /// <param name="conductor">The conductor instance.</param>
        public void Emit(IndentedTextWriter writer, IEnvironment env, Conductor conductor)
        {
            var methodEnv = (MethodEnvironment)env;
            if (methodEnv.SignatureHandler.GetWrapperSignature().ContainsPlaceholder)
            {
                Console.WriteLine($"Method {methodEnv.MethodDecl.Name} has unsupported signature: ({methodEnv.SignatureHandler.GetWrapperSignature().ParametersString()}) -> {methodEnv.SignatureHandler.GetWrapperSignature().ReturnType}");
                return;
            }

            var wrapperEmitter = new WrapperEmitter(methodEnv);
            wrapperEmitter.EmitConstructor(writer);
            PInvokeEmitter.EmitPInvoke(writer, methodEnv);
            writer.WriteLine();
        }
    }

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
            return decl is MethodDecl methodDecl && !methodDecl.IsConstructor;
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
            if (methodEnv.SignatureHandler.GetWrapperSignature().ContainsPlaceholder)
            {
                Console.WriteLine($"Method {methodEnv.MethodDecl.Name} has unsupported signature: ({methodEnv.SignatureHandler.GetWrapperSignature().ParametersString()}) -> {methodEnv.SignatureHandler.GetWrapperSignature().ReturnType}");
                return;
            }

            var wrapperEmitter = new WrapperEmitter(methodEnv);
            wrapperEmitter.EmitMethod(writer);
            PInvokeEmitter.EmitPInvoke(writer, methodEnv);
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
        MethodDecl MethodDecl { get; }
        BaseDecl ParentDecl { get; }
        ITypeDatabase TypeDatabase { get; }

        public WrapperSignatureBuilder(MethodDecl methodDecl, ITypeDatabase typeDatabase)
        {
            MethodDecl = methodDecl;
            ParentDecl = methodDecl.ParentDecl!;
            TypeDatabase = typeDatabase;
        }

        /// <summary>
        /// Handles the return type of the method.
        /// </summary>
        public void HandleReturnType()
        {
            var argument = MethodDecl.CSSignature.First();
            var typeRecord = TypeDatabase.GetTypeRecordOrAnyType(argument.SwiftTypeSpec);
            SetReturnType(typeRecord.CSTypeIdentifier);
        }

        /// <summary>
        /// Handles the arguments of the method.
        /// </summary>
        public void HandleArguments()
        {
            foreach (var argument in MethodDecl.CSSignature.Skip(1))
            {
                var typeRecord = TypeDatabase.GetTypeRecordOrAnyType(argument.SwiftTypeSpec);
                AddParameter(typeRecord.CSTypeIdentifier, argument.Name);
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

        MethodDecl MethodDecl { get; }
        BaseDecl ParentDecl { get; }
        ITypeDatabase TypeDatabase { get; }


        /// <summary>
        /// Initializes a new instance of the <see cref="PInvokeSignatureBuilder"/> class.
        /// </summary>
        /// <param name="methodDecl">The method declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="typeDatabase">The type database.</param>
        public PInvokeSignatureBuilder(MethodDecl methodDecl, ITypeDatabase typeDatabase)
        {
            MethodDecl = methodDecl;
            ParentDecl = methodDecl.ParentDecl!;
            TypeDatabase = typeDatabase;
        }

        /// <summary>
        /// Handles the return type of the method.
        /// </summary>
        public void HandleReturnType()
        {
            if (!MarshallingHelpers.MethodRequiresIndirectResult(MethodDecl, ParentDecl, TypeDatabase))
            {
                var returnTypeRecord = TypeDatabase.GetTypeRecordOrThrow(MethodDecl.CSSignature.First().SwiftTypeSpec);
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
            foreach (var argument in MethodDecl.CSSignature.Skip(1))
            {
                var argumentTypeRecord = TypeDatabase.GetTypeRecordOrThrow(argument.SwiftTypeSpec);
                if (MarshallingHelpers.ArgumentIsMarshalledAsCSStruct(argument, TypeDatabase))
                {
                    AddParameter(argumentTypeRecord.CSTypeIdentifier, argument.Name);
                }
                else
                {
                    AddParameter($"SwiftHandle", argument.Name);
                }
            }
        }

        /// <summary>
        /// Handles the SwiftSelf parameter of the method.
        /// </summary>
        public void HandleSwiftSelf()
        {
            if (MarshallingHelpers.MethodRequiresSwiftSelf(MethodDecl, ParentDecl))
            {
                if (ParentDecl is StructDecl structDecl && MarshallingHelpers.StructIsMarshalledAsCSStruct(structDecl))
                {
                    AddParameter($"SwiftSelf<{ParentDecl.Name}>", "self");
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
            if (MethodDecl.Throws)
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

        MethodDecl MethodDecl { get; }
        ITypeDatabase TypeDatabase { get; }

        public SignatureHandler(MethodDecl methodDecl, ITypeDatabase typeDatabase)
        {
            MethodDecl = methodDecl;
            TypeDatabase = typeDatabase;
        }

        /// <summary>
        /// Gets the PInvoke signature.
        /// </summary>
        /// <returns>The PInvoke signature.</returns>
        public Signature GetPInvokeSignature()
        {
            if (_pInvokeSignature == null)
            {
                var pInvokeSignature = new PInvokeSignatureBuilder(MethodDecl, TypeDatabase);
                pInvokeSignature.HandleReturnType();
                pInvokeSignature.HandleArguments();
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
                var wrapperSignature = new WrapperSignatureBuilder(MethodDecl, TypeDatabase);
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
        public static void EmitPInvoke(IndentedTextWriter writer, MethodEnvironment methodEnv)
        {
            var methodDecl = (MethodDecl)methodEnv.MethodDecl;
            var moduleDecl = methodDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(methodDecl.ModuleDecl));

            var pInvokeName = NameProvider.GetPInvokeName(methodDecl);
            var libPath = methodEnv.TypeDatabase.GetLibraryPath(moduleDecl.Name);

            writer.WriteLine("[UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]");
            writer.WriteLine($"[DllImport(\"{libPath}\", EntryPoint = \"{methodDecl.MangledName}\")]");

            var pInvokeSignature = methodEnv.SignatureHandler.GetPInvokeSignature();

            writer.WriteLine($"private static extern {pInvokeSignature.ReturnType} {pInvokeName}({pInvokeSignature.ParametersString()});");
        }
    }

    /// <summary>
    /// Provides methods for generating names.
    /// <summary>
    public static class NameProvider
    {
        public static string GetPInvokeName(MethodDecl methodDecl)
        {
            return $"PInvoke_{methodDecl.Name}";
        }
    }

    /// <summary>
    /// Provides methods for emitting wrappers.
    /// </summary>

    internal class WrapperEmitter
    {
        private readonly MethodDecl _methodDecl;
        private readonly BaseDecl _parentDecl;
        private readonly ITypeDatabase _typeDatabase;
        private readonly Signature _wrapperSignature;
        private readonly Signature _pInvokeSignature;
        private readonly bool _requiresIndirectResult;
        private readonly bool _requiresSwiftSelf;
        private readonly bool _requiresSwiftError;

        internal WrapperEmitter(MethodEnvironment methodEnv)
        {
            _methodDecl = methodEnv.MethodDecl;
            _parentDecl = _methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodEnv.MethodDecl.ParentDecl));
            _typeDatabase = methodEnv.TypeDatabase;

            _wrapperSignature = methodEnv.SignatureHandler.GetWrapperSignature();
            _pInvokeSignature = methodEnv.SignatureHandler.GetPInvokeSignature();
            _requiresIndirectResult = MarshallingHelpers.MethodRequiresIndirectResult(_methodDecl, _parentDecl, _typeDatabase);
            _requiresSwiftSelf = MarshallingHelpers.MethodRequiresSwiftSelf(_methodDecl, _parentDecl);
            _requiresSwiftError = _methodDecl.Throws;
        }

        /// <summary>
        /// Emits the constructor wrapper.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        internal void EmitConstructor(IndentedTextWriter writer)
        {
            EmitSignatureConstructor(writer);
            EmitBodyStart(writer);
            EmitSwiftSelf(writer);
            EmitIndirectResultConstructor(writer);
            EmitPInvokeCall(writer);
            EmitSwiftError(writer);
            EmitReturnConstructor(writer);
            EmitBodyEnd(writer);
        }

        /// <summary>
        /// Emits the method wrapper.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        internal void EmitMethod(IndentedTextWriter writer)
        {
            EmitSignatureMethod(writer);
            EmitBodyStart(writer);
            EmitSwiftSelf(writer);
            EmitIndirectResultMethod(writer);
            EmitPInvokeCall(writer);
            EmitSwiftError(writer);
            EmitReturnMethod(writer);
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

            if (_methodDecl.ParentDecl is StructDecl structDecl && MarshallingHelpers.StructIsMarshalledAsCSStruct(structDecl))
            {
                writer.WriteLine($"var self = new SwiftSelf<{_methodDecl.ParentDecl.Name}>(this);");
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

            writer.WriteLine($"_payload = (SwiftHandle)NativeMemory.Alloc(_payloadSize);");
            writer.WriteLine("var swiftIndirectResult = new SwiftIndirectResult((void*)_payload);");
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

            writer.WriteLine($"var payload = (SwiftHandle)NativeMemory.Alloc({_wrapperSignature.ReturnType}.PayloadSize);");
            writer.WriteLine("var swiftIndirectResult = new SwiftIndirectResult((void*)payload);");
            writer.WriteLine();
        }

        /// <summary>
        /// Emits the PInvoke call.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitPInvokeCall(IndentedTextWriter writer)
        {
            var voidReturn = _methodDecl.CSSignature.First().SwiftTypeSpec.IsEmptyTuple;
            var returnPrefix = (_requiresIndirectResult || voidReturn) ? "" : "var result = ";
            writer.WriteLine($"{returnPrefix}{NameProvider.GetPInvokeName(_methodDecl)}({_pInvokeSignature.CallArgumentsString()});");
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
                throw new SwiftRuntimeException("Call to Swift method {{_methodDecl.FullyQualifiedName}} failed.");
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
                if (_methodDecl.CSSignature.First().SwiftTypeSpec.IsEmptyTuple)
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
            writer.WriteLine($"public {_methodDecl.ParentDecl!.Name}({_wrapperSignature.ParametersString()})");
        }

        /// <summary>
        /// Emits the method signature.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        private void EmitSignatureMethod(IndentedTextWriter writer)
        {
            var staticKeyword = _methodDecl.MethodType == MethodType.Static || _methodDecl.ParentDecl is ModuleDecl ? "static " : "";
            var unsafeKeyword = _requiresIndirectResult ? "unsafe " : "";

            writer.WriteLine($"public {staticKeyword}{unsafeKeyword} {_wrapperSignature.ReturnType} {_methodDecl.Name}({_wrapperSignature.ParametersString()})");
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
