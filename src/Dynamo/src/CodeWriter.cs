// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

namespace Dynamo;

/// <summary>
/// Class responsible for writing code.
/// </summary>
public class CodeWriter : ICodeWriter
{
    int charsWrittenThisLine;
    bool indentedThisLine;

    const int kWrapPoint = 120;

    /// <summary>
    /// Creates a new CodeWriter that writes to the specified stream.
    /// </summary>
    /// <param name="stm">the stream to write to</param>
    public CodeWriter(Stream stm)
        : this(new StreamWriter(stm))
    {
    }

    /// <summary>
    /// Creates a new CodeWriter that writes to the specified TextWriter.
    /// </summary>
    /// <param name="tw">the TextWriter to write to</param>
    public CodeWriter(TextWriter tw)
    {
        TextWriter = tw;
        charsWrittenThisLine = 0;
        IndentLevel = 0;
        IsAtLineStart = true;
    }

    /// <summary>
    /// Writes the specified code element to the given file
    /// </summary>
    /// <param name="fileName">the path to the file to write</param>
    /// <param name="element">the element to write to the file</param>
    public static void WriteToFile(string fileName, ICodeElement element)
    {
        using (var stm = new FileStream(fileName, FileMode.Create))
        {
            CodeWriter writer = new CodeWriter(stm);
            element.WriteAll(writer);
            writer.TextWriter.Flush();
        }
    }

    /// <summary>
    /// Writes the specified code element to the given file asynchronously
    /// </summary>
    /// <param name="fileName">the path to the file to write</param>
    /// <param name="element">the element to write to the file</param>
    /// <returns>the task to wait on</returns>
    public async Task WriteToFileAsync(string fileName, ICodeElement element)
    {
        using (var stm = new FileStream(fileName, FileMode.Create))
        {
            CodeWriter writer = new CodeWriter(stm);
            element.WriteAll(writer);
            await writer.TextWriter.FlushAsync();
        }
    }

    /// <summary>
    /// Writes the specified code element to a MemoryStream and returns the stream positioned at the beginning.
    /// </summary>
    /// <param name="element">the element to write to the file</param>
    /// <returns>the stream containing the code</returns>
    public static MemoryStream WriteToMemoryStream(ICodeElement element)
    {
        var stm = new MemoryStream();
        var codeWriter = new CodeWriter(stm);
        element.WriteAll(codeWriter);
        codeWriter.TextWriter.Flush();
        stm.Flush();
        stm.Seek(0, SeekOrigin.Begin);
        return stm;
    }

    /// <summary>
    /// Writes the specified code element to a string and returns the string.
    /// </summary>
    /// <param name="element">the element to write</param>
    /// <returns>the string containing the code</returns>
    public static string WriteToString(ICodeElement element)
    {
        using (var reader = new StreamReader(WriteToMemoryStream(element)))
            return reader.ReadToEnd();
    }

    /// <summary>
    /// Gets the TextWriter that this CodeWriter writes to.
    /// </summary>
    public TextWriter TextWriter { get; private set; }

    #region ICodeWriter implementation

    /// <inheritdoc/>
    public void BeginNewLine(bool prependIndents)
    {
        Write(Environment.NewLine, false);
        if (prependIndents)
            WriteIndents();
        IsAtLineStart = true;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public bool IsAtLineStart { get; private set; }

    /// <inheritdoc/>
    public void Write(string code, bool allowSplit)
    {
        var characterEnum = StringInfo.GetTextElementEnumerator(code);
        while (characterEnum.MoveNext())
        {
            string c = characterEnum.GetTextElement();
            WriteUnicode(c, allowSplit);
        }
    }

    /// <inheritdoc/>
    public void Write(char c, bool allowSplit)
    {
        Write(c.ToString(), allowSplit);
    }

    /// <summary>
    /// Writes a single unicode character to the stream. Note that this is necessary to handle the UTF16 in a string
    /// properly, whereas a single char will not, especially for high unicode or combining characters.
    /// </summary>
    /// <param name="c">The unicode character to write</param>
    /// <param name="allowSplit">if true, allow a line break if the line gets too long</param>
    /// <exception cref="ArgumentOutOfRangeException">thrown if c is not a single text element</exception>
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

    /// <summary>
    /// Special case for longer unicode text elements
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    bool IsWhiteSpace(string c)
    {
        return c.Length == 1 && Char.IsWhiteSpace(c[0]);

    }

    /// <summary>
    /// Writes a string to the stream without adding to the current line length.
    void WriteNoBookKeeping(string s)
    {
        TextWriter.Write(s);
    }

    /// <summary>
    /// Writes a series of tabs for each intent level.
    /// </summary>
    void WriteIndents()
    {
        for (int i = 0; i < IndentLevel; i++)
            Write("\t", false);
    }

    /// <inheritdoc/>
    public void Indent()
    {
        IndentLevel++;
    }

    /// <inheritdoc/>
    public void Exdent()
    {
        if (IndentLevel == 0)
            throw new Exception("IndentLevel is at 0.");
        IndentLevel--;
    }

    /// <inheritdoc/>
    public int IndentLevel { get; private set; }

    #endregion
}

