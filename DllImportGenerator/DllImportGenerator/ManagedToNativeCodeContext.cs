using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Interop
{
    internal sealed class ManagedToNativeCodeContext : StubCodeContext
    {
        public override bool SingleFrameSpansNativeContext => true;

        public override bool AdditionalTemporaryStateLivesAcrossStages => true;

        /// <summary>
        /// Identifier for managed return value
        /// </summary>
        public const string ReturnIdentifier = "__retVal";
        private const string InvokeReturnIdentifier = "__invokeRetVal";
        private readonly IEnumerable<TypePositionInfo> typeInfos;

        /// <summary>
        /// Identifier for native return value
        /// </summary>
        /// <remarks>Same as the managed identifier by default</remarks>
        public string ReturnNativeIdentifier { get; set; } = ReturnIdentifier;

        public ManagedToNativeCodeContext(IEnumerable<TypePositionInfo> typeInfos)
        {
            this.typeInfos = typeInfos;
        }

        public override (string managed, string native) GetIdentifiers(TypePositionInfo info)
        {
            // If the info is in the managed return position, then we need to generate a name to use
            // for both the managed and native values since there is no name in the signature for the return value.
            if (info.IsManagedReturnPosition)
            {
                return (ReturnIdentifier, ReturnNativeIdentifier);
            }
            // If the info is in the native return position but is not in the managed return position,
            // then that means that the stub is introducing an additional info for the return position.
            // This means that there is no name in source for this info, so we must provide one here.
            // We can't use ReturnIdentifier or ReturnNativeIdentifier since that will be used by the managed return value.
            // Additionally, since all use cases today of a TypePositionInfo in the native position but not the managed
            // are for infos that aren't in the managed signature at all (PreserveSig scenario), we don't have a name
            // that we can use from source. As a result, we generate another name for the native return value
            // and use the same name for native and managed.
            else if (info.IsNativeReturnPosition)
            {
                Debug.Assert(info.ManagedIndex == TypePositionInfo.UnsetIndex);
                return (InvokeReturnIdentifier, InvokeReturnIdentifier);
            }
            else
            {
                // If the info isn't in either the managed or native return position,
                // then we can use the base implementation since we have an identifier name provided
                // in the original metadata.
                return base.GetIdentifiers(info);
            }
        }

        public override TypePositionInfo? GetTypePositionInfoForManagedIndex(int index)
        {
            foreach (var info in typeInfos)
            {
                if (info.ManagedIndex == index)
                {
                    return info;
                }
            }
            return null;
        }
    }
}
