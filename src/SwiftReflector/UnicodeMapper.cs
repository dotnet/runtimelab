// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace SwiftReflector
{
    public sealed class UnicodeMapper
    {

        static Dictionary<string, string> DefaultMapping = new Dictionary<string, string> {
            { "\x03b1", "Alpha" },
            { "\x03b2", "Beta" },
            { "\x03b3", "Gamma" },
            { "\x03b4", "Delta" },
            { "\x03b5", "Epsilon" },
            { "𝑒", "LittleEpsilon"},
            { "Π", "BigPi"},
            { "\x03c0", "Pi" },
            { "𝝉", "Tau"},
            { "\x03c4", "Tau" },
            { "\x00ac", "Not" },
            { "\x2227", "And" },
            { "\x2228", "Or" },
            { "\x22bb", "Xor" },
            { "\x2295", "CirclePlus" },
            { "\x21ae", "NotLeftRightArrow" },
            { "\x2262", "NotIdentical" },
            { "\x22bc", "Nand" },
            { "\x2191", "UpArrow" },
            { "\x22bd", "Nor" },
            { "\x2193", "DownArrow" },
            { "\x22a6", "Assertion" },
            { "\x00f7", "Division" },
            { "\x00d7", "Multiplication" },
            { "\x221a", "SquareRoot" },
            { "\x221b", "CubeRoot" },
            { "\x00b1", "PlusOrMinus" },
            { "\x2213", "MinusOrPlus" },
            { "\x2224", "DoesNotDivide" },
            { "\x2208", "ElementOf" },
            { "\x220c", "NotElementOf" },
            { "\x2229", "Intersection" },
            { "\x222a", "Union" },
            { "\x2286", "Subset" },
            { "\x2282", "ProperSubset" },
            { "\x2284", "NotSubset" },
            { "\x2287", "Superset" },
            { "\x2283", "ProperSuperset" },
            { "\x2285", "NotSuperset" },
            { "\x2264", "LessThanOrEqualTo" },
            { "\x2265", "GreaterThanOrEqualTo" },
            { "\x226c", "Between" },
            { "\x2248", "Approximates" },
            { "\x2218", "Ring" },
        };

        public static UnicodeMapper Default = new UnicodeMapper();

        Dictionary<string, string> mapping;

        public UnicodeMapper()
        {
            mapping = DefaultMapping;
        }

        // While this takes a string, it should be one 'character', possibly made up of multiple
        // parts, like 🍎
        public string MapToUnicodeName(string s)
        {
            if (new StringInfo(s).LengthInTextElements != 1)
                throw new ArgumentOutOfRangeException(nameof(s));

            if (mapping.TryGetValue(s, out var result))
                return result;

            StringBuilder stringBuilder = new StringBuilder().Append("U");

            var encoding = Encoding.UTF32;
            var bytes = encoding.GetBytes(s);
            var utf32Value = BitConverter.ToUInt32(bytes, 0);
            if (utf32Value > 0xffff)
                stringBuilder.Append($"{utf32Value:X8}");
            else
                stringBuilder.Append($"{utf32Value:X4}");

            return stringBuilder.ToString();
        }

        public void AddMappingsFromFile(string fileName)
        {
            AddMappingsFromXML(File.ReadAllText(fileName));
        }

        public void AddMappingsFromXML(string mappingXML)
        {
            if (mapping == DefaultMapping)
            {
                // Clone the default mapping, since we don't want to modify it.
                mapping = new Dictionary<string, string>(mapping);
            }
            foreach (var m in ReadMappings(mappingXML))
                mapping[m.Item1] = m.Item2;
        }

        static IEnumerable<Tuple<string, string>> ReadMappings(string mappingXML)
        {
            XDocument xmlDocument = XDocument.Parse(mappingXML);
            foreach (var mapping in xmlDocument.Descendants("map"))
            {
                string from = mapping.Attribute(XName.Get("from"))?.Value;
                string to = mapping.Attribute(XName.Get("to"))?.Value;

                if (from != null && to != null)
                    yield return new Tuple<string, string>(from, to);
            }
        }
    }
}
