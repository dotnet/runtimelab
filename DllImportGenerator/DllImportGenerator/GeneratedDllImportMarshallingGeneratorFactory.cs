using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Interop
{
    class GeneratedDllImportMarshallingGeneratorFactory : DefaultMarshallingGeneratorFactory<DllImportGeneratorOptions>
    {
        public GeneratedDllImportMarshallingGeneratorFactory(DllImportGeneratorOptions options) : base(options) { }

        protected override IMarshallingGenerator CreateCore(TypePositionInfo info, StubCodeContext context)
        {
            if (Options.GenerateForwarders)
            {
                return Forwarder;
            }

            if (info.IsNativeReturnPosition && !info.IsManagedReturnPosition)
            {
                // Use marshaller for native HRESULT return / exception throwing
                System.Diagnostics.Debug.Assert(info.ManagedType.SpecialType == SpecialType.System_Int32);
                return HResultException;
            }

            return base.CreateCore(info, context);
        }
    }
}
