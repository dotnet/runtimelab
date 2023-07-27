using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.ManagedZLib;

public class CtData
{
    public ushort Freq { get; set; } // frequency count
    //public ushort Code { get; set; } // bit string
    public ushort Len { get; set; } // length of bit string
    //public ushort Parent { get; set; } // father node in Huffman tree
}
