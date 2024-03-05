// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo;
using System.IO;
using System.CodeDom.Compiler;
using System.CodeDom;

namespace SyntaxDynamo.CSLang
{
    public class CSConstant : CSBaseExpression
    {
        public CSConstant(string val)
        {
            ArgumentNullException.ThrowIfNull(val, nameof(val));
            Value = val;
        }

        public static explicit operator CSConstant(string val)
        {
            return new CSConstant(val);
        }

        public string Value { get; private set; }

        protected override void LLWrite(ICodeWriter writer, object? o)
        {
            writer.Write(Value, false);
        }

        public static CSConstant Val(byte b) { return new CSConstant(b.ToString()); }
        public static CSConstant Val(sbyte sb) { return new CSConstant(sb.ToString()); }
        public static CSConstant Val(ushort us) { return new CSConstant(us.ToString()); }
        public static CSConstant Val(short s) { return new CSConstant(s.ToString()); }
        public static CSConstant Val(uint ui) { return new CSConstant(ui.ToString()); }
        public static CSConstant Val(int i) { return new CSConstant(i.ToString()); }
        public static CSConstant Val(ulong ul) { return new CSConstant(ul.ToString()); }
        public static CSConstant Val(long l) { return new CSConstant(l.ToString()); }
        public static CSConstant Val(float f)
        {
            return new CSConstant(f.ToString() + "f");
        }
        public static CSConstant Val(double d) { return new CSConstant(d.ToString()); }
        public static CSConstant Val(bool b) { return new CSConstant(b ? "true" : "false"); }
        public static CSConstant Val(char c) { return new CSConstant(ToCharLiteral(c)); }
        public static CSConstant Val(string s) { return new CSConstant(ToStringLiteral(s)); }
        static CSConstant cNull = new CSConstant("null");
        public static CSConstant Null { get { return cNull; } }
        static CSConstant cIntPtrZero = new CSConstant("IntPtr.Zero");
        public static CSConstant IntPtrZero { get { return cIntPtrZero; } }
        public static CSBaseExpression ValNFloat(double d) { return new CSCastExpression(CSSimpleType.NFloat, Val(d)); }

        static string ToCharLiteral(char c)
        {
            using (var writer = new StringWriter())
            {
                using (var provider = CodeDomProvider.CreateProvider("CSharp"))
                {
                    provider.GenerateCodeFromExpression(new CodePrimitiveExpression(c), writer, new CodeGeneratorOptions());
                    return writer.ToString();
                }
            }
        }

        static string ToStringLiteral(string s)
        {
            using (var writer = new StringWriter())
            {
                using (var provider = CodeDomProvider.CreateProvider("CSharp"))
                {
                    provider.GenerateCodeFromExpression(new CodePrimitiveExpression(s), writer, new CodeGeneratorOptions());
                    return writer.ToString();
                }
            }
        }
    }
}
