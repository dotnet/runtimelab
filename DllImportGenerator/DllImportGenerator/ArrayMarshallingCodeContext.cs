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
        public const string LocalManagedIdentifierSuffix = "_local";

        private readonly string indexerIdentifier;
        private readonly StubCodeContext parentContext;
        private readonly bool stripLocalManagedIdentifierSuffix;

        public override bool PinningSupported => false;

        public override bool StackSpaceUsable => false;

        public override bool CanUseAdditionalTemporaryState => false;

        public ArrayMarshallingCodeContext(
            Stage currentStage,
            string indexerIdentifier,
            StubCodeContext parentContext,
            bool stripLocalManagedIdentifierSuffix)
        {
            CurrentStage = currentStage;
            this.indexerIdentifier = indexerIdentifier;
            this.parentContext = parentContext;
            this.stripLocalManagedIdentifierSuffix = stripLocalManagedIdentifierSuffix;
        }

        /// <summary>
        /// Get managed and native instance identifiers for the <paramref name="info"/>
        /// </summary>
        /// <param name="info">Object for which to get identifiers</param>
        /// <returns>Managed and native identifiers</returns>
        public override (string managed, string native) GetIdentifiers(TypePositionInfo info)
        {
            bool strippedSuffix = false;
            if (stripLocalManagedIdentifierSuffix && info.InstanceIdentifier.EndsWith(LocalManagedIdentifierSuffix))
            {
                strippedSuffix = true;
                info = info with { InstanceIdentifier = info.InstanceIdentifier.Substring(0, info.InstanceIdentifier.Length - LocalManagedIdentifierSuffix.Length) };
            }
            var (managed, native) = parentContext.GetIdentifiers(info);
            if (strippedSuffix)
            {
                return ($"{managed}{LocalManagedIdentifierSuffix}[{indexerIdentifier}]", $"{native}[{indexerIdentifier}]");
            }
            return ($"{managed}[{indexerIdentifier}]", $"{native}[{indexerIdentifier}]");
        }

        public override TypePositionInfo? GetTypePositionInfoForManagedIndex(int index)
        {
            // We don't have parameters to look at when we're in the middle of marshalling an array.
            return null;
        }
    }
}
