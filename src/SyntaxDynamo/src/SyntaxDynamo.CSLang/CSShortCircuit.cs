using System;
namespace SyntaxDynamo.CSLang
{
    public class CSShortCircuit : DelegatedSimpleElement, ICSLineable
    {
        public CSShortCircuit(CSShortCircuitKind kind)
        {
            Kind = kind;
        }

        public CSShortCircuitKind Kind { get; private set; }

        protected override void LLWrite(ICodeWriter writer, object? o)
        {
            var keyword = Kind == CSShortCircuitKind.Break ? "break" : "continue";
            writer.Write(keyword, false);
        }

        public static CSLine ShortCircuit(CSShortCircuitKind kind)
        {
            return new CSLine(new CSShortCircuit(kind));
        }

        public static CSLine Continue()
        {
            return ShortCircuit(CSShortCircuitKind.Continue);
        }

        public static CSLine Break()
        {
            return ShortCircuit(CSShortCircuitKind.Break);
        }
    }
}
