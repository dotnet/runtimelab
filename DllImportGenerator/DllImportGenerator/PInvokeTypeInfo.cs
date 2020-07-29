using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Interop
{
    /// <summary>
    /// Information on Types involved in P/Invoke
    /// </summary>
    internal class PInvokeTypeInfo
    {
        public string Identifier { get; set; }
        public string StubType { get; set; }
        public string DllImportType { get; set; }
        public RefKind RefKind { get; set; }
        public AttributeData MarshalAsData { get; set; }
    }
}
