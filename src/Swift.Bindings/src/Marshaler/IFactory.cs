// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// A factory that constructs a handler.
    /// </summary>
    /// <typeparam name="BaseDecl">BaseDecl is the type of the declaration that the factory can handle.</typeparam>
    /// <typeparam name="U">Handler type that the factory constructs.</typeparam>
    public interface IFactory<BaseDecl, U>
    {
        /// <summary>
        /// Checks if the factory can handle the declaration.
        /// </summary>
        abstract bool Handles(BaseDecl decl);

        /// <summary>
        /// Constructs a handler.
        /// </summary>
        abstract U Construct();
    }
}
