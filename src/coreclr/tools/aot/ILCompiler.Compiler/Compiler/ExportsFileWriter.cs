// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public class ExportsFileWriter
    {
        private readonly string _exportsFile;
        private readonly bool _isExecutable;
        private readonly string[] _exportSymbols;
        private readonly List<EcmaMethod> _methods;
        private readonly TypeSystemContext _context;

        public ExportsFileWriter(TypeSystemContext context, bool isExecutable, string exportsFile, string[] exportSymbols)
        {
            _exportsFile = exportsFile;
            _isExecutable = isExecutable;
            _exportSymbols = exportSymbols;
            _context = context;
            _methods = new List<EcmaMethod>();
        }

        public void AddExportedMethods(IEnumerable<EcmaMethod> methods)
            => _methods.AddRange(methods.Where(m => m.Module != _context.SystemModule));

        public void EmitExportedMethods()
        {
            FileStream fileStream = new FileStream(_exportsFile, FileMode.Create);
            using (StreamWriter streamWriter = new StreamWriter(fileStream))
            {
                if (_context.Target.IsWindows)
                {
                    streamWriter.WriteLine("EXPORTS");
                    foreach (string symbol in _exportSymbols)
                        streamWriter.WriteLine($"   {symbol.Replace(',', ' ')}");
                    foreach (var method in _methods)
                        streamWriter.WriteLine($"   {method.GetUnmanagedCallersOnlyExportName()}");
                }
                else if (_context.Target.IsApplePlatform || _context.Target.OperatingSystem == TargetOS.Browser)
                {
                    if (_isExecutable && _context.Target.OperatingSystem == TargetOS.Browser && _exportSymbols.Length + _methods.Count != 0)
                    {
                        // With Emscripten we need to explicitly export main in case we have exports beside it in an executable.
                        streamWriter.WriteLine("_main");
                    }

                    foreach (string symbol in _exportSymbols)
                        streamWriter.WriteLine($"_{symbol}");
                    foreach (var method in _methods)
                        streamWriter.WriteLine($"_{method.GetUnmanagedCallersOnlyExportName()}");
                }
                else if (_context.Target.OperatingSystem == TargetOS.Wasi)
                {
                    foreach (string symbol in _exportSymbols)
                        streamWriter.WriteLine(symbol);
                    foreach (var method in _methods)
                        streamWriter.WriteLine(method.GetUnmanagedCallersOnlyExportName());
                }
                else
                {
                    streamWriter.WriteLine("V1.0 {");
                    if (_exportSymbols.Length != 0 || _methods.Count != 0)
                    {
                        streamWriter.WriteLine("    global:");
                        foreach (string symbol in _exportSymbols)
                            streamWriter.WriteLine($"        {symbol};");
                        foreach (var method in _methods)
                            streamWriter.WriteLine($"        {method.GetUnmanagedCallersOnlyExportName()};");
                    }
                    streamWriter.WriteLine("    local: *;");
                    streamWriter.WriteLine("};");
                }
            }
        }
    }
}
