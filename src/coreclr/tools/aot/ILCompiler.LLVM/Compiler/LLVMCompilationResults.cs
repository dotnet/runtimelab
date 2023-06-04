// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ILCompiler
{
    internal sealed class LLVMCompilationResults
    {
        private readonly List<string> _files = new();

        public void Add(string file)
        {
            Debug.Assert(!_files.Contains(file));
            _files.Add(file);
        }

        public void SerializeToFile(string path)
        {
            // Sort the files here so that downstream consumers don't have to.
            _files.Sort(StringComparer.Ordinal);
            File.WriteAllLines(path, _files);
        }
    }
}
