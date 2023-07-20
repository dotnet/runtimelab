﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.IO.Compression;
using System.Text;

namespace Microsoft.ManagedZLib.Benchmarks;

// Referring the dotnet/performance documentation (performance/docs/microbenchmark-design-guidelines.md)
// BenchmarkDotNet creates a type which derives from type with benchmarks. 
// So the type with benchmarks must not be sealed and it can NOT BE STATIC 
// and it has to BE PUBLIC. It also has to be a class (no structs support).
public class ManagedZLibBenchmark
{

    [GlobalSetup]
    public void Setup()
    {
        //CompressedFile = new CompressedFile(file, level, CreateStream);
    }

    [GlobalCleanup]
    //public void Cleanup() => Directory.Delete(_rootDirPath, recursive: true);

    [Benchmark]
    public void zipProccess()
    {
        //Check why it's still complaining of zip existing if we deleted it in the setUp function
        //ZipIt(_testDirPath);
        //File.Delete(_testDirPath);
    }

    private readonly string zipName = @".\resultZip.zip";
    public void MakeFile(string filename, string message)
    {
        //Creates a file
        FileStream fs = new(filename, FileMode.Create, FileAccess.ReadWrite);

        if (fs.CanWrite)
        {
            byte[] buffer = Encoding.Default.GetBytes(message);
            fs.Write(buffer, 0, buffer.Length);
        }
        fs.Flush();
        fs.Close();
    }
    public void ZipIt(string directoryPath)
    {
        ZipFile.CreateFromDirectory(directoryPath, zipName);
    }
    public void UnzipIt(string ZipFilePath, string ExtractDestination)
    {

        ZipFile.CreateFromDirectory(ZipFilePath, ExtractDestination);
    }
    public class programRun
    {
        static void Main()
        {
            //The benchmark I want to test
            BenchmarkRunner.Run<ManagedZLibBenchmark>();
        }
    }

}