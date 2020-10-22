using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal sealed class ArrayMarshallingCodeContext : StubCodeContext
    {
        private readonly string indexerIdentifier;

        public override bool PinningSupported => false;

        public override bool StackSpaceUsable => false;

        public override bool CanUseAdditionalTemporaryState => false;

        public ArrayMarshallingCodeContext(Stage currentStage, string indexerIdentifier)
        {
            CurrentStage = currentStage;
            this.indexerIdentifier = indexerIdentifier;
        }

        /// <summary>
        /// Get managed and native instance identifiers for the <paramref name="info"/>
        /// </summary>
        /// <param name="info">Object for which to get identifiers</param>
        /// <returns>Managed and native identifiers</returns>
        public override (string managed, string native) GetIdentifiers(TypePositionInfo info)
        {
            var (managed, native) = base.GetIdentifiers(info);
            return ($"{managed}[{indexerIdentifier}]", $"{native}[{indexerIdentifier}]");
        }
    }
}
