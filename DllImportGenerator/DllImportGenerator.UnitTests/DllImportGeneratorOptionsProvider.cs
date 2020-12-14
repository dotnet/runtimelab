using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DllImportGenerator.UnitTests
{
    internal class DllImportGeneratorOptionsProvider : AnalyzerConfigOptionsProvider
    {
        public DllImportGeneratorOptionsProvider(bool useMarshalType, bool generateForwarders)
        {
            GlobalOptions = new GlobalGeneratorOptions(useMarshalType, generateForwarders);
        }

        public override AnalyzerConfigOptions GlobalOptions  { get; }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return EmptyOptions.Instance;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return EmptyOptions.Instance;
        }

        class GlobalGeneratorOptions : AnalyzerConfigOptions
        {
            private readonly bool _useMarshalType = false;
            private readonly bool _generateForwarders = false;
            public GlobalGeneratorOptions(bool useMarshalType, bool generateForwarders)
            {
                _useMarshalType = useMarshalType;
                _generateForwarders = generateForwarders;
            }

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                switch (key)
                {
                    case "build_property.DllImportGenerator_UseMarshalType":
                        value = _useMarshalType.ToString();
                        return true;
                    
                    case "build_property.DllImportGenerator_GenerateForwarders":
                        value = _generateForwarders.ToString();
                        return true;
                    
                    default:
                        value = null;
                        return false;
                }
            }
        }

        class EmptyOptions : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                value = null;
                return false;
            }

            public static AnalyzerConfigOptions Instance = new EmptyOptions();
        }
    }
}