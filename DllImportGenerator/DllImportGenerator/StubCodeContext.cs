using System.Collections.Generic;

using Microsoft.CodeAnalysis;

namespace Microsoft.Interop
{
    public enum GenerationStage
    {
        Setup,
        Marshal,
        Pin,
        Invoke,
        Unmarshal,
        Cleanup
    }

    class StubCodeContext
    {
        public IList<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();

        public GenerationStage CurrentStage => stage;

        private GenerationStage stage;
        internal void SetStage(GenerationStage stage)
        {
            this.stage = stage;
        }

        public StubCodeContext(GenerationStage stage)
        {
            this.stage = stage;
        }

        public string GenerateReturnNativeIdentifier()
        {
            ReturnNativeIdentifier = $"{ReturnIdentifier}{GeneratedNativeIdentifierSuffix}";
            return ReturnNativeIdentifier;
        }

        public const string GeneratedNativeIdentifierSuffix = "_gen_native";

        private const string returnIdentifier = "__retVal";
        public string ReturnIdentifier => returnIdentifier;
        public string ReturnNativeIdentifier { get; private set; } = returnIdentifier;
    }
}
