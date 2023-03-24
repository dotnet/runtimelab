using System.Collections.Generic;
using System;

using Mono.Cecil;
using Mono.Cecil.Rocks;
using System.Linq;
using System.Reflection.PortableExecutable;

using BlobReader = System.Reflection.Metadata.BlobReader;

namespace MstatDump;

internal class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: MstatDump <mstat file>");
            return -1;
        }

        var fileName = args[0];

        var asm = AssemblyDefinition.ReadAssembly(args[0]);
        var globalType = (TypeDefinition)asm.MainModule.LookupToken(0x02000001);

        int versionMajor = asm.Name.Version.Major;

        PEReader peReader = new PEReader(System.IO.File.OpenRead(fileName));
        BlobReader nameMapReader = default;
        if (versionMajor > 1)
        {
            PEMemoryBlock nameMap = peReader.GetSectionData(".names");
            nameMapReader = nameMap.GetReader();
        }

        var types = globalType.Methods.First(x => x.Name == "Types");
        var typeStats = GetTypes(versionMajor, nameMapReader, types).ToList();
        var typeSize = typeStats.Sum(x => x.Size);
        var typesByModules = typeStats.GroupBy(x => x.Type.Scope).Select(x => new { x.Key.Name, Sum = x.Sum(x => x.Size) }).ToList();
        Console.WriteLine($"// ********** Types Total Size {typeSize:n0}");
        foreach (var m in typesByModules.OrderByDescending(x => x.Sum))
        {
            Console.WriteLine($"{m.Name,-40} {m.Sum,7:n0}");
        }
        Console.WriteLine($"// **********");

        Console.WriteLine();

        var methods = globalType.Methods.First(x => x.Name == "Methods");
        var methodStats = GetMethods(versionMajor, methods).ToList();
        var methodSize = methodStats.Sum(x => x.Size + x.GcInfoSize + x.EhInfoSize);
        var methodsByModules = methodStats.GroupBy(x => x.Method.DeclaringType.Scope).Select(x => new { x.Key.Name, Sum = x.Sum(x => x.Size + x.GcInfoSize + x.EhInfoSize) }).ToList();
        Console.WriteLine($"// ********** Methods Total Size {methodSize:n0}");
        foreach (var m in methodsByModules.OrderByDescending(x => x.Sum))
        {
            Console.WriteLine($"{m.Name,-40} {m.Sum,7:n0}");
        }
        Console.WriteLine($"// **********");

        Console.WriteLine();

        string FindNamespace(TypeReference type)
        {
            var current = type;
            while (true)
            {
                if (!String.IsNullOrEmpty(current.Namespace))
                {
                    return current.Namespace;
                }

                if (current.DeclaringType == null)
                {
                    return current.Name;
                }

                current = current.DeclaringType;
            }
        }

        var methodsByNamespace = methodStats.Select(x => new TypeStats { Type = x.Method.DeclaringType, Size = x.Size + x.GcInfoSize + x.EhInfoSize }).Concat(typeStats).GroupBy(x => FindNamespace(x.Type)).Select(x => new { x.Key, Sum = x.Sum(x => x.Size) }).ToList();
        Console.WriteLine($"// ********** Size By Namespace");
        foreach (var m in methodsByNamespace.OrderByDescending(x => x.Sum))
        {
            Console.WriteLine($"{m.Key,-40} {m.Sum,7:n0}");
        }
        Console.WriteLine($"// **********");

        Console.WriteLine();

        var blobs = globalType.Methods.First(x => x.Name == "Blobs");
        var blobStats = GetBlobs(blobs).ToList();
        var blobSize = blobStats.Sum(x => x.Size);
        Console.WriteLine($"// ********** Blobs Total Size {blobSize:n0}");
        foreach (var m in blobStats.OrderByDescending(x => x.Size))
        {
            Console.WriteLine($"{m.Name,-40} {m.Size,7:n0}");
        }
        Console.WriteLine($"// **********");
        return 0;
    }

    public static IEnumerable<TypeStats> GetTypes(int formatVersion, BlobReader nameMapReader, MethodDefinition types)
    {
        int entrySize = formatVersion == 1 ? 2 : 3;

        types.Body.SimplifyMacros();
        var il = types.Body.Instructions;
        for (int i = 0; i + entrySize < il.Count; i += entrySize)
        {
            var type = (TypeReference)il[i + 0].Operand;
            var size = (int)il[i + 1].Operand;

            if (formatVersion > 1)
            {
                var index = (int)il[i + 2].Operand;
                nameMapReader.Offset = index;
                Console.WriteLine(nameMapReader.ReadSerializedString());
            }

            yield return new TypeStats
            {
                Type = type,
                Size = size
            };
        }
    }

    public static IEnumerable<MethodStats> GetMethods(int formatVersion, MethodDefinition methods)
    {
        int entrySize = formatVersion == 1 ? 4 : 5;

        methods.Body.SimplifyMacros();
        var il = methods.Body.Instructions;
        for (int i = 0; i + entrySize < il.Count; i += entrySize)
        {
            var method = (MethodReference)il[i + 0].Operand;
            var size = (int)il[i + 1].Operand;
            var gcInfoSize = (int)il[i + 2].Operand;
            var ehInfoSize = (int)il[i + 3].Operand;
            yield return new MethodStats
            {
                Method = method,
                Size = size,
                GcInfoSize = gcInfoSize,
                EhInfoSize = ehInfoSize
            };
        }
    }

    public static IEnumerable<BlobStats> GetBlobs(MethodDefinition blobs)
    {
        blobs.Body.SimplifyMacros();
        var il = blobs.Body.Instructions;
        for (int i = 0; i + 2 < il.Count; i += 2)
        {
            var name = (string)il[i + 0].Operand;
            var size = (int)il[i + 1].Operand;
            yield return new BlobStats
            {
                Name = name,
                Size = size
            };
        }
    }
}