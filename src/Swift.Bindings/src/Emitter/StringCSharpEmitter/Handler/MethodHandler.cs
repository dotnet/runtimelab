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
        /// <param name="decl"></param>
        public IEnvironment Marshal(BaseDecl decl, TypeDatabase typeDatabase)
        {
            return new MethodEnvironment(decl, typeDatabase);
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
            var methodDecl = (MethodDecl)methodEnv.MethodDecl;
            var parentDecl = methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));
            writer.WriteLine($"public {parentDecl.Name}({SignatureHandler.GetCSWrapperSignature(methodDecl)})");

            writer.WriteLine("{");
            writer.Indent++;

            string PInvokeName = $"{methodEnv.PInvokePrefix}{methodDecl.Name}";

            var pInvokeSignature = SignatureHandler.GetPinvokeSignature(methodDecl, methodEnv.TypeDatabase);
            string invokeArguments = pInvokeSignature.ParametersNames();

            if (methodDecl.RequiresIndirectResult(parentDecl, methodEnv.TypeDatabase))
            {
                writer.WriteLine($"_payload = (SwiftHandle)NativeMemory.Alloc(_payloadSize);");
                writer.WriteLine("var swiftIndirectResult = new SwiftIndirectResult((void*)_payload);");
                writer.WriteLine($"{PInvokeName}({invokeArguments});");
            }
            else
            {
                writer.WriteLine($"this = {PInvokeName}({invokeArguments});");
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
        public IEnvironment Marshal(BaseDecl methodDecl, TypeDatabase typeDatabase)
        {
            return new MethodEnvironment(methodDecl, typeDatabase);
        }

        /// <summary>
        /// Emits the method declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        /// <param name="conductor">The conductor instance.</param>
        /// <param name="typeDatabase">The type database.</param>
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
        /// <param name="typeDatabase">The type database.</param>
        private void EmitWrapperMethod(IndentedTextWriter writer, MethodEnvironment env)
        {
            var methodDecl = (MethodDecl)env.MethodDecl;
            var parentDecl = methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));

            string methodName = $"{env.PInvokePrefix}{methodDecl.Name}";

            writer.WriteLine($"public {((methodDecl.MethodType == MethodType.Static || parentDecl is ModuleDecl) ? "static " : "")}{methodDecl.CSSignature.First().CSTypeIdentifier.Name} {methodDecl.Name}({SignatureHandler.GetCSWrapperSignature(methodDecl)})");
            writer.WriteLine("{");
            writer.Indent++;

            if (methodDecl.RequiresSwiftSelf(parentDecl))
            {
                if (parentDecl is StructDecl structDecl && structDecl.IsMarshalledAsStruct())
                    writer.WriteLine($"var self = new SwiftSelf<{parentDecl.Name}>(this);");
                else
                    writer.WriteLine($"var self = new SwiftSelf((void*)_payload);");
            }

            // TODO: Add Indirect result marshalling to methods other than constructors

            string returnPrefix = methodDecl.CSSignature.First().CSTypeIdentifier.Name == "void" ? "" : "return ";

            var pInvokeSignature = SignatureHandler.GetPinvokeSignature(methodDecl, env.TypeDatabase);
            string invokeArguments = pInvokeSignature.ParametersNames();

            // Call the PInvoke method
            writer.WriteLine($"{returnPrefix}{methodName}({invokeArguments});");

            writer.Indent--;
            writer.WriteLine("}");
        }
    }

    /// <summary>
    /// Represents a PInvoke argument.
    /// </summary>
    /// <param name="Type"></param>
    /// <param name="Name"></param>
    internal record PInvokeArgument(string Type, string Name)
    {
        public override string ToString() => $"{Type} {Name}";
    }

    /// <summary>
    /// Represents a PInvoke signature.
    /// </summary>
    /// <param name="ReturnType"></param>
    /// <param name="Parameters"></param>
    internal record PInvokeSignature(string ReturnType, IReadOnlyList<PInvokeArgument> Parameters)
    {
        public string ParametersString() => string.Join(", ", Parameters.Select(p => p.ToString()));

        public string ParametersNames() => string.Join(", ", Parameters.Select(p =>
            p.Type == "SwiftHandle" ? $"{p.Name}.Payload" : p.Name)); // TODO: Find a better way to do this
    }

    /// <summary>
    /// Represents a PInvoke signature builder.
    /// </summary>
    internal class PInvokeSignatureBuilder
    {
        private string _returnType = "invalid";
        private readonly List<PInvokeArgument> _parameters = new();

        MethodDecl MethodDecl { get; }
        BaseDecl ParentDecl { get; }
        TypeDatabase TypeDatabase { get; }


        /// <summary>
        /// Initializes a new instance of the <see cref="PInvokeSignatureBuilder"/> class.
        /// </summary>
        /// <param name="methodDecl">The method declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="typeDatabase">The type database.</param>
        public PInvokeSignatureBuilder(MethodDecl methodDecl, BaseDecl parentDecl, TypeDatabase typeDatabase)
        {
            MethodDecl = methodDecl;
            ParentDecl = parentDecl;
            TypeDatabase = typeDatabase;
        }

        /// <summary>
        /// Handles the return type of the method.
        /// </summary>
        public void HandleReturnType()
        {
            if (!MethodDecl.RequiresIndirectResult(ParentDecl, TypeDatabase))
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
                var typeRecord = TypeDatabase.Registrar.GetType(argument);
                if (typeRecord.IsFrozen && typeRecord.IsBlittable)
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
            if (MethodDecl.RequiresSwiftSelf(ParentDecl))
            {
                if (ParentDecl is StructDecl structDecl && structDecl.IsMarshalledAsStruct())
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
        public PInvokeSignature Build()
        {
            return new PInvokeSignature(_returnType, _parameters.ToArray());
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
            _parameters.Add(new PInvokeArgument(type, name));
        }
    }

    /// <summary>
    /// Provides methods for handling method signatures.
    /// </summary>
    static class SignatureHandler
    {
        /// <summary>
        /// Gets the method parameters.
        /// </summary>
        /// <param name="methodDecl">The method declaration.</param>
        /// <param name="typeDatabase">The type database.</param>
        /// <returns>The method parameters.</returns>
        public static PInvokeSignature GetPinvokeSignature(MethodDecl methodDecl, TypeDatabase typeDatabase)
        {
            var parentDecl = methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));
            var pInvokeSignature = new PInvokeSignatureBuilder(methodDecl, parentDecl, typeDatabase);
            pInvokeSignature.HandleReturnType();
            pInvokeSignature.HandleArguments();
            pInvokeSignature.HandleSwiftSelf();
            return pInvokeSignature.Build();
        }

        /// <summary>
        /// Gets the wrapper method signature.
        /// </summary>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The wrapper method signature.</returns>
        public static string GetCSWrapperSignature(MethodDecl methodDecl)
        {
            List<ArgumentDecl> parameters = methodDecl.CSSignature.Skip(1).ToList();
            return string.Join(", ", parameters.Select(p => $"{p.CSTypeIdentifier.Name} {p.Name}").ToList());
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
        /// <param name="typeDatabase">The type database.</param>
        public static void EmitPInvoke(IndentedTextWriter writer, MethodEnvironment methodEnv)
        {
            var methodDecl = (MethodDecl)methodEnv.MethodDecl;
            var parentDecl = methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));
            var moduleDecl = methodDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(methodDecl.ModuleDecl));

            string PInvokeName = $"{methodEnv.PInvokePrefix}{methodDecl.Name}";
            string libPath = methodEnv.TypeDatabase.GetLibraryName(moduleDecl.Name);

            writer.WriteLine("[UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]");
            writer.WriteLine($"[DllImport(\"{libPath}\", EntryPoint = \"{methodDecl.MangledName}\")]");

            var pInvokeSignature = SignatureHandler.GetPinvokeSignature(methodDecl, methodEnv.TypeDatabase);

            writer.WriteLine($"private static extern {pInvokeSignature.ReturnType} {PInvokeName}({pInvokeSignature.ParametersString()});");
        }
    }
}
