// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Represents an environment interface. It should contain data required to emit C# code.
    /// </summary>
    public interface IEnvironment
    {
        public TypeDatabase TypeDatabase { get; } // TODO: Replace with proper interface. Consider base class.
    }

    /// <summary>
    /// Represents a module environment.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the ModuleEnvironment class.
    /// </remarks>
    /// <param name="moduleDecl">The module declaration.</param>
    public class ModuleEnvironment(BaseDecl moduleDecl, TypeDatabase typeDatabase) : IEnvironment
    {
        /// <summary>
        /// Gets the module declaration.
        /// </summary>
        public BaseDecl ModuleDecl { get; private set; } = moduleDecl;
        public TypeDatabase TypeDatabase { get; } = typeDatabase;
    }

    /// <summary>
    /// Represents a type environment.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the TypeEnvironment class.
    /// </remarks>
    /// <param name="typeDecl">The type declaration.</param>
    public class TypeEnvironment(BaseDecl typeDecl, TypeDatabase typeDatabase) : IEnvironment
    {
        /// <summary>
        /// Gets the type declaration.
        /// </summary>
        public BaseDecl TypeDecl { get; private set; } = typeDecl;
        public TypeDatabase TypeDatabase { get; } = typeDatabase;
    }

    /// <summary>
    /// Represents a method environment.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the MethodEnvironment class.
    /// </remarks>
    /// <param name="methodDecl">The method declaration.</param>
    public class MethodEnvironment(BaseDecl methodDecl, TypeDatabase typeDatabase) : IEnvironment
    {
        /// <summary>
        /// Gets the method declaration.
        /// </summary>
        public BaseDecl MethodDecl { get; private set; } = methodDecl;

        /// <summary>
        /// Gets the PInvoke prefix.
        /// </summary>
        public string PInvokePrefix { get; private set; } = "PInvoke_";

        public TypeDatabase TypeDatabase { get; } = typeDatabase;
    }
}
