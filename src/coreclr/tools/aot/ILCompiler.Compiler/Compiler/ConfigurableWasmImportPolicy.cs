// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

namespace ILCompiler
{
    public class ConfigurableWasmImportPolicy
    {
        private readonly Dictionary<string, string> _wasmImports; // function names to module names

        public ConfigurableWasmImportPolicy(IReadOnlyList<string> wasmImports, IReadOnlyList<string> wasmImportLists)
        {
            _wasmImports = new Dictionary<string, string>();

            foreach (var file in wasmImportLists)
            {
                foreach (var entry in File.ReadLines(file))
                {
                    AddWasmImport(entry);
                }
            }

            foreach (var entry in wasmImports)
            {
                AddWasmImport(entry);
            }
        }

        private void AddWasmImport(string entry)
        {
            // Ignore comments
            if (entry.StartsWith('#'))
                return;

            entry = entry.Trim();

            // Ignore empty entries
            if (string.IsNullOrEmpty(entry))
                return;

            int separator = entry.IndexOf('!');

            if (separator != -1)
            {
                string wasmModuleName = entry.Substring(0, separator);
                string entrypointName = entry.Substring(separator + 1);

                if (_wasmImports.ContainsKey(entrypointName))
                {
                    // this is an artificial restriction because we are using just the PInvoke function name to distinguish WebAssembly imports
                    throw new Exception("WebAssembly function imports must be unique");
                }
                _wasmImports.Add(entrypointName, wasmModuleName);
            }
            else
            {
                throw new Exception("WebAssembly import entries must be of the format <module name>!<function name>");
            }
        }

        public bool TryGetWasmModule(string realMethodName, out string wasmModuleName)
        {
            return _wasmImports.TryGetValue(realMethodName, out wasmModuleName);
        }
    }
}
