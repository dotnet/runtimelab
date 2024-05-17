// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents an environment interface. It should contain data required to emit C# code.
    /// </summary>
    public interface IEnvironment
    {
    }

    /// <summary>
    /// Represents a module environment.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the ModuleEnvironment class.
    /// </remarks>
    /// <param name="moduleDecl">The module declaration.</param>
    public class ModuleEnvironment(BaseDecl moduleDecl) : IEnvironment
    {
        /// <summary>
        /// Gets the module declaration.
        /// </summary>
        public BaseDecl ModuleDecl { get; private set; } = moduleDecl;
    }

    /// <summary>
    /// Represents a type environment.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the TypeEnvironment class.
    /// </remarks>
    /// <param name="typeDecl">The type declaration.</param>
    public class TypeEnvironment(BaseDecl typeDecl) : IEnvironment
    {
        /// <summary>
        /// Gets the type declaration.
        /// </summary>
        public BaseDecl TypeDecl { get; private set; } = typeDecl;
    }

    /// <summary>
    /// Represents a method environment.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the MethodEnvironment class.
    /// </remarks>
    /// <param name="methodDecl">The method declaration.</param>
    public class MethodEnvironment(BaseDecl methodDecl) : IEnvironment
    {
        /// <summary>
        /// Gets the method declaration.
        /// </summary>
        public BaseDecl MethodDecl { get; private set; } = methodDecl;

        /// <summary>
        /// Gets the PInvoke prefix.
        /// </summary>
        public string PInvokePrefix { get; private set; } = "PInvoke_";
    }
}
