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
        public IEnvironment Marshal(BaseDecl decl, TypeDatabase typeDatabase)
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
        /// <param name="typeDatabase">The type database instance.</param>
        public void Emit(IndentedTextWriter writer, IEnvironment env, Conductor conductor)
        {
            var methodEnv = (MethodEnvironment)env;
            EmitWrapper(writer, methodEnv);
            PInvokeEmitter.EmitPInvoke(writer, methodEnv);
            writer.WriteLine();
        }

        /// <summary>
        /// Emits the wrapper method declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="methodEnv">The method environment.</param>
        /// <param name="typeDatabase">The type database instance.</param>
        private static void EmitWrapper(IndentedTextWriter writer, MethodEnvironment methodEnv)
        {
            var methodDecl = methodEnv.MethodDecl;
            var parentDecl = methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));
            writer.WriteLine($"public {parentDecl.Name}({methodEnv.SignatureHandler.GetWrapperSignature().ParametersString()})");

            writer.WriteLine("{");
            writer.Indent++;

            var pInvokeName = NameProvider.GetPInvokeName(methodDecl);

            var pInvokeSignature = methodEnv.SignatureHandler.GetPInvokeSignature();
            var invokeArguments = pInvokeSignature.CallArgumentsString();

            if (MarshallingHelpers.MethodRequiresIndirectResult(methodDecl, parentDecl, methodEnv.TypeDatabase))
            {
                writer.WriteLine($"_payload = (SwiftHandle)NativeMemory.Alloc(_payloadSize);");
                writer.WriteLine("var swiftIndirectResult = new SwiftIndirectResult((void*)_payload);");
                writer.WriteLine($"{pInvokeName}({invokeArguments});");
            }
            else
            {
                writer.WriteLine($"this = {pInvokeName}({invokeArguments});");
            }

            writer.Indent--;
            writer.WriteLine("}");
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
        public IEnvironment Marshal(BaseDecl decl, TypeDatabase typeDatabase)
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

            EmitWrapperMethod(writer, methodEnv);
            PInvokeEmitter.EmitPInvoke(writer, methodEnv);
            writer.WriteLine();
        }

        /// <summary>
        /// Emits the wrapper method declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        private void EmitWrapperMethod(IndentedTextWriter writer, MethodEnvironment env)
        {
            var methodDecl = (MethodDecl)env.MethodDecl;
            var parentDecl = methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));

            var pInvokeName = NameProvider.GetPInvokeName(methodDecl);
            var staticKeyword = methodDecl.MethodType == MethodType.Static || parentDecl is ModuleDecl ? "static " : "";

            writer.WriteLine($"public {staticKeyword}{methodDecl.CSSignature.First().CSTypeIdentifier.Name} {methodDecl.Name}({env.SignatureHandler.GetWrapperSignature().ParametersString()})");
            writer.WriteLine("{");
            writer.Indent++;

            if (MarshallingHelpers.MethodRequiresSwiftSelf(methodDecl, parentDecl))
            {
                if (parentDecl is StructDecl structDecl && MarshallingHelpers.StructIsMarshalledAsCSStruct(structDecl))
                    writer.WriteLine($"var self = new SwiftSelf<{parentDecl.Name}>(this);");
                else
                    writer.WriteLine($"var self = new SwiftSelf((void*)_payload);");
            }

            // TODO: Add Indirect result marshalling to methods other than constructors

            var returnPrefix = methodDecl.CSSignature.First().CSTypeIdentifier.Name == "void" ? "" : "return ";
            var invokeArguments = env.SignatureHandler.GetPInvokeSignature().CallArgumentsString();

            // Call the PInvoke method
            writer.WriteLine($"{returnPrefix}{pInvokeName}({invokeArguments});");

            writer.Indent--;
            writer.WriteLine("}");
        }
    }

    /// <summary>
    /// Represents a parameter.
    /// </summary>
    /// <param name="Type"></param>
    /// <param name="Name"></param>
    public record Parameter(string Type, string Name)
    {
        public override string ToString() => $"{Type} {Name}";
    }

    /// <summary>
    /// Represents a signature.
    /// </summary>
    /// <param name="ReturnType"></param>
    /// <param name="Parameters"></param>
    public record Signature(string ReturnType, IReadOnlyList<Parameter> Parameters)
    {
        public string ParametersString() => string.Join(", ", Parameters.Select(p => p.ToString()));

        public string CallArgumentsString() => string.Join(", ", Parameters.Select(p =>
            p.Type == "SwiftHandle" ? $"{p.Name}.Payload" : p.Name)); // TODO: Find a better way to do this
    }

    public class WrapperSignatureBuilder
    {
        private string _returnType = "invalid";
        private readonly List<Parameter> _parameters = new();

        MethodDecl MethodDecl { get; }
        BaseDecl ParentDecl { get; }
        TypeDatabase TypeDatabase { get; }

        public WrapperSignatureBuilder(MethodDecl methodDecl, TypeDatabase typeDatabase)
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
            var returnType = MethodDecl.CSSignature.First().CSTypeIdentifier.Name;
            SetReturnType(returnType);
        }

        /// <summary>
        /// Handles the arguments of the method.
        /// </summary>
        public void HandleArguments()
        {
            foreach (var argument in MethodDecl.CSSignature.Skip(1))
            {
                AddParameter(argument.CSTypeIdentifier.Name, argument.Name);
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
        TypeDatabase TypeDatabase { get; }


        /// <summary>
        /// Initializes a new instance of the <see cref="PInvokeSignatureBuilder"/> class.
        /// </summary>
        /// <param name="methodDecl">The method declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="typeDatabase">The type database.</param>
        public PInvokeSignatureBuilder(MethodDecl methodDecl, TypeDatabase typeDatabase)
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
                var returnType = MethodDecl.CSSignature.First().CSTypeIdentifier.Name;
                SetReturnType(returnType);
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
                if (MarshallingHelpers.ArgumentIsMarshalledAsCSStruct(argument, TypeDatabase))
                {
                    AddParameter(argument.CSTypeIdentifier.Name, argument.Name);
                }
                else
                {
                    AddParameter($"SwiftHandle", argument.Name);
                }
            }
        }

        /// <summary>
        /// Handles the Swift self parameter of the method.
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
    /// Provides methods for handling method signatures.
    /// </summary>
    public class SignatureHandler
    {
        private Signature? _pInvokeSignature;
        private Signature? _wrapperSignature;

        MethodDecl MethodDecl { get; }
        TypeDatabase TypeDatabase { get; }

        public SignatureHandler(MethodDecl methodDecl, TypeDatabase typeDatabase)
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
    public static class PInvokeEmitter
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
            var libPath = methodEnv.TypeDatabase.GetLibraryName(moduleDecl.Name);

            writer.WriteLine("[UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]");
            writer.WriteLine($"[DllImport(\"{libPath}\", EntryPoint = \"{methodDecl.MangledName}\")]");

            var pInvokeSignature = methodEnv.SignatureHandler.GetPInvokeSignature();

            writer.WriteLine($"private static extern {pInvokeSignature.ReturnType} {pInvokeName}({pInvokeSignature.ParametersString()});");
        }
    }

    public static class NameProvider
    {
        public static string GetPInvokeName(MethodDecl methodDecl)
        {
            return $"PInvoke_{methodDecl.Name}";
        }
    }
}
