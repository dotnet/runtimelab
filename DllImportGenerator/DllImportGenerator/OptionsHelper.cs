using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Interop
{
    internal static class OptionsHelper
    {
        public static bool UseMarshalType(this AnalyzerConfigOptions options)
        {
            return options.TryGetValue("build_property.DllImportGenerator_UseMarshalType", out string? value)
                && bool.TryParse(value, out bool result)
                && result;
        }

        public static bool GenerateForwarders(this AnalyzerConfigOptions options)
        {
            return options.TryGetValue("build_property.DllImportGenerator_GenerateForwarders", out string? value)
                && bool.TryParse(value, out bool result)
                && result;
        }
    }
}
