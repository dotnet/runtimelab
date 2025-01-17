// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;

public static class IndentedTextWriterExtensions
{
    public static void WriteLines(this IndentedTextWriter _indentedTextWriter, string lines)
    {
        var lineSeparators = new char[] { '\n', '\r' };
        foreach (var line in lines.Split(lineSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            _indentedTextWriter.WriteLine(line);
        }
    }
}
