// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents an argument declaration.
    /// </summary>
    public record ArgumentDecl : BaseDecl
    {
        /// <summary>
        /// Type of the argument
        /// <summary>
        public required TypeSpec SwiftTypeSpec { get; set; }

        /// <summary>
        /// The private name of the argument.
        /// </summary>
        public required string PrivateName { get; set; }

        /// <summary>
        /// Indicates the inout annotation of the argument.
        /// </summary>
        public required bool IsInOut { get; set; }

        /// <summary>
        /// Indicates if the argument is generic.
        /// </summary>
        public required bool IsGeneric { get; set; }
    }
}
