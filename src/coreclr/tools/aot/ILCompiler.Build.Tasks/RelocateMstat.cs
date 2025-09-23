// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Build.Tasks
{
    public class RelocateMstat : Task
    {
        [Required]
        public string MstatFilePath { get; set; }

        [Required]
        public string LinkMapPath { get; set; }

        public override bool Execute()
        {
            using FileStream stream = File.Open(MstatFilePath, FileMode.Open);
            using PEReader reader = new(stream);

            int methodsMethodRva = 0;
            MetadataReader mdReader = reader.GetMetadataReader();
            foreach (MethodDefinitionHandle methodDefHandle in mdReader.MethodDefinitions)
            {
                MethodDefinition methodDef = mdReader.GetMethodDefinition(methodDefHandle);
                if (mdReader.GetString(methodDef.Name) == "Methods")
                {
                    methodsMethodRva = methodDef.RelativeVirtualAddress;
                    break;
                }
            }
            if (methodsMethodRva == 0)
            {
                return true;
            }

            Dictionary<string, int> linkMap = ParseLinkMap();
            BlobReader methodsBody = reader.GetSectionData(methodsMethodRva).GetReader();
            int ilHeaderSize = ((methodsBody.ReadByte() & 0x3) != 0) ? 12 : 1;

            BlobReader relocs = reader.GetSectionData(".szreloc").GetReader();
            BlobReader names = reader.GetSectionData(".names").GetReader();
            List<(int, string)> relocList = new();
            while (relocs.RemainingBytes != 0)
            {
                int ilOffset = relocs.ReadInt32();
                names.Offset = relocs.ReadInt32();
                string target = names.ReadSerializedString();
                relocList.Add((ilOffset, target));
            }

            int sectionIndex = reader.PEHeaders.GetContainingSectionIndex(methodsMethodRva);
            int sectionVA = reader.PEHeaders.SectionHeaders[sectionIndex].VirtualAddress;
            int sectionFileOffset = reader.PEHeaders.SectionHeaders[sectionIndex].PointerToRawData;
            int methodsMethodFileOffset = methodsMethodRva - sectionVA + sectionFileOffset;

            BinaryWriter writer = new(stream);
            foreach (var (ilOffset, target) in relocList)
            {
                if (linkMap.TryGetValue(target, out int size))
                {
                    writer.Seek(methodsMethodFileOffset + ilHeaderSize + ilOffset, SeekOrigin.Begin);
                    writer.Write(size);
                }
            }
            return true;
        }

        private Dictionary<string, int> ParseLinkMap()
        {
            using StreamReader reader = new(LinkMapPath, Encoding.UTF8);

            char[] whitespace = [' '];
            bool isCode = false;
            Dictionary<string, int> result = new();
            while (reader.ReadLine() is string line)
            {
                string[] parts = line.Split(whitespace, 4, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 && parts[3] == "CODE")
                {
                    isCode = true;
                    continue;
                }
                if (!isCode)
                {
                    continue;
                }
                if (parts[3] == "DATA")
                {
                    break;
                }

                string section = parts[3];
                int symbolStart = section.LastIndexOf(":(", StringComparison.Ordinal);
                if (symbolStart > 0 && section[section.Length - 1] == ')')
                {
                    symbolStart += 2;

                    int size = int.Parse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    string symbol = section.Substring(symbolStart, section.Length - symbolStart - 1);
                    if (result.ContainsKey(symbol))
                    {
                        result[symbol] += size;
                    }
                    else
                    {
                        result.Add(symbol, size);
                    }
                }
            }
            return result;
        }
    }
}
