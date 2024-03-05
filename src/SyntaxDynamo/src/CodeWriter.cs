// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO;

namespace SyntaxDynamo
{
    public class CodeWriter : ICodeWriter
    {
        int charsWrittenThisLine;
        bool indentedThisLine;

        const int kSpacesPerIndent = 4;
        const int kWrapPoint = 60;

        public CodeWriter(Stream stm)
            : this(new StreamWriter(stm))
        {
        }

        public CodeWriter(TextWriter tw)
        {
            ArgumentNullException.ThrowIfNull(tw, nameof(tw));
            TextWriter = tw;
            charsWrittenThisLine = 0;
            IndentLevel = 0;
            IsAtLineStart = true;
        }

        public static void WriteToFile(string fileName, ICodeElement element)
        {
            using (var stm = new FileStream(fileName, FileMode.Create))
            {
                CodeWriter writer = new CodeWriter(stm);
                element.WriteAll(writer);
                writer.TextWriter.Flush();
            }
        }

        public static Stream WriteToStream(ICodeElement element)
        {
            var stm = new MemoryStream();
            var codeWriter = new CodeWriter(stm);
            element.WriteAll(codeWriter);
            codeWriter.TextWriter.Flush();
            stm.Flush();
            stm.Seek(0, SeekOrigin.Begin);
            return stm;
        }

        public static string WriteToString(ICodeElement element)
        {
            using (var reader = new StreamReader(WriteToStream(element)))
                return reader.ReadToEnd();
        }

        public TextWriter TextWriter { get; private set; }

        #region ICodeWriter implementation

        public void BeginNewLine(bool prependIndents)
        {
            Write(Environment.NewLine, false);
            if (prependIndents)
                WriteIndents();
            IsAtLineStart = true;
        }

        public void EndLine()
        {
            charsWrittenThisLine = 0;
            if (indentedThisLine)
            {
                indentedThisLine = false;
                // strictly speaking, this test shouldn't be necessary
                if (IndentLevel > 0)
                    Exdent();
            }
        }

        public bool IsAtLineStart { get; private set; }

        public void Write(string code, bool allowSplit)
        {
            var characterEnum = StringInfo.GetTextElementEnumerator(code);
            while (characterEnum.MoveNext())
            {
                string c = characterEnum.GetTextElement();
                WriteUnicode(c, allowSplit);
            }
        }

        public void Write(char c, bool allowSplit)
        {
            Write(c.ToString(), allowSplit);
        }

        void WriteUnicode(string c, bool allowSplit)
        {
            var info = new StringInfo(c);
            if (info.LengthInTextElements > 1)
                throw new ArgumentOutOfRangeException(nameof(c), $"Expected a single unicode value but got '{c}'");
            WriteNoBookKeeping(c);
            IsAtLineStart = false;
            charsWrittenThisLine++;
            if (allowSplit && charsWrittenThisLine > kWrapPoint && IsWhiteSpace(c))
            {
                if (!indentedThisLine)
                {
                    Indent();
                    indentedThisLine = true;
                }
                WriteNoBookKeeping(Environment.NewLine);
                charsWrittenThisLine = 0;
                WriteIndents();
                IsAtLineStart = true;
            }
        }

        bool IsWhiteSpace(string c)
        {
            return c.Length == 1 && Char.IsWhiteSpace(c[0]);

        }

        void WriteNoBookKeeping(string s)
        {
            TextWriter.Write(s);
        }

        void WriteIndents()
        {
            int totalSpaces = IndentLevel * kSpacesPerIndent;
            // know what's embarrassing? When your indents cause breaks which generates more indents.
            for (int i = 0; i < totalSpaces; i++)
                Write(" ", false);
        }

        public void Indent()
        {
            IndentLevel++;
        }

        public void Exdent()
        {
            if (IndentLevel == 0)
                throw new Exception("IndentLevel is at 0.");
            IndentLevel--;
        }

        public int IndentLevel { get; private set; }

        #endregion
    }
}
