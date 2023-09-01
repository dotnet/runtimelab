// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.ManagedZLib;

// For compression with fixed Huffman codes
public class StaticTreeTables
{
    public static readonly CtData[] StaticLengthTree = new CtData[DeflateTrees.LitLenCodes + 2]  { //My change to just an array
        new CtData { Freq = 12, Len = 8}, new CtData { Freq =140, Len = 8}, new CtData { Freq = 76, Len = 8 }, new CtData { Freq = 204, Len = 8 }, new CtData { Freq = 44, Len = 8 },
        new CtData { Freq = 172, Len = 8 }, new CtData { Freq = 108, Len = 8 }, new CtData { Freq = 236, Len = 8 }, new CtData { Freq = 28, Len = 8 }, new CtData { Freq = 156, Len = 8 },
        new CtData { Freq = 92, Len = 8 }, new CtData { Freq = 220, Len = 8 }, new CtData { Freq = 60, Len = 8 }, new CtData { Freq = 188, Len = 8 }, new CtData { Freq = 124, Len = 8 },
        new CtData { Freq = 252, Len = 8 }, new CtData { Freq = 2, Len = 8 }, new CtData { Freq = 130, Len = 8 }, new CtData { Freq = 66, Len = 8 }, new CtData { Freq = 194, Len = 8 },
        new CtData { Freq = 34, Len = 8 }, new CtData { Freq = 162, Len = 8 }, new CtData { Freq = 98, Len = 8 }, new CtData { Freq = 226, Len = 8 }, new CtData { Freq = 18, Len = 8 },
        new CtData { Freq = 146, Len = 8 }, new CtData { Freq = 82, Len = 8 }, new CtData { Freq = 210, Len = 8 }, new CtData { Freq = 50, Len = 8 }, new CtData { Freq = 178, Len = 8 },
        new CtData { Freq = 114, Len = 8 }, new CtData { Freq = 242, Len = 8 }, new CtData { Freq = 10, Len = 8 }, new CtData { Freq = 138, Len = 8 }, new CtData { Freq = 74, Len = 8 },
        new CtData { Freq = 202, Len = 8 }, new CtData { Freq = 42, Len = 8 }, new CtData { Freq = 170, Len = 8 }, new CtData { Freq = 106, Len = 8 }, new CtData { Freq = 234, Len = 8 },
        new CtData { Freq = 26, Len = 8 }, new CtData { Freq = 154, Len = 8 }, new CtData { Freq = 90, Len = 8 }, new CtData { Freq = 218, Len = 8 }, new CtData { Freq = 58, Len = 8 },
        new CtData { Freq = 186, Len = 8 }, new CtData { Freq = 122, Len = 8 }, new CtData { Freq = 250, Len = 8 }, new CtData { Freq = 6, Len = 8 }, new CtData { Freq = 134, Len = 8 },
        new CtData { Freq = 70, Len = 8 }, new CtData { Freq = 198, Len = 8 }, new CtData { Freq = 38, Len = 8 }, new CtData { Freq = 166, Len = 8 }, new CtData { Freq = 102, Len = 8 },
        new CtData { Freq = 230, Len = 8 }, new CtData { Freq = 22, Len = 8 }, new CtData { Freq = 150, Len = 8 }, new CtData { Freq = 86, Len = 8 }, new CtData { Freq = 214, Len = 8 },
        new CtData { Freq = 54, Len = 8 }, new CtData { Freq = 182, Len = 8 }, new CtData { Freq = 118, Len = 8 }, new CtData { Freq = 246, Len = 8 }, new CtData { Freq = 14, Len = 8 },
        new CtData { Freq = 142, Len = 8 }, new CtData { Freq = 78, Len = 8 }, new CtData { Freq = 206, Len = 8 }, new CtData { Freq = 46, Len = 8 }, new CtData { Freq = 174, Len = 8 },
        new CtData { Freq = 110, Len = 8 }, new CtData { Freq = 238, Len = 8 }, new CtData { Freq = 30, Len = 8 }, new CtData { Freq = 158, Len = 8 }, new CtData { Freq = 94, Len = 8 },
        new CtData { Freq = 222, Len = 8 }, new CtData { Freq = 62, Len = 8 }, new CtData { Freq = 190, Len = 8 }, new CtData { Freq = 126, Len = 8 }, new CtData { Freq = 254, Len = 8 },
        new CtData { Freq = 1, Len = 8 }, new CtData { Freq = 129, Len = 8 }, new CtData { Freq = 65, Len = 8 }, new CtData { Freq = 193, Len = 8 }, new CtData { Freq = 33, Len = 8 },
        new CtData { Freq = 161, Len = 8 }, new CtData { Freq = 97, Len = 8 }, new CtData { Freq = 225, Len = 8 }, new CtData { Freq = 17, Len = 8 }, new CtData { Freq = 145, Len = 8 },
        new CtData { Freq = 81, Len = 8 }, new CtData { Freq = 209, Len = 8 }, new CtData { Freq = 49, Len = 8 }, new CtData { Freq = 177, Len = 8 }, new CtData { Freq = 113, Len = 8 },
        new CtData { Freq = 241, Len = 8 }, new CtData { Freq = 9, Len = 8 }, new CtData { Freq = 137, Len = 8 }, new CtData { Freq = 73, Len = 8 }, new CtData { Freq = 201, Len = 8 },
        new CtData { Freq = 41, Len = 8 }, new CtData { Freq = 169, Len = 8 }, new CtData { Freq = 105, Len = 8 }, new CtData { Freq = 233, Len = 8 }, new CtData { Freq = 25, Len = 8 },
        new CtData { Freq = 153, Len = 8 }, new CtData { Freq = 89, Len = 8 }, new CtData { Freq = 217, Len = 8 }, new CtData { Freq = 57, Len = 8 }, new CtData { Freq = 185, Len = 8 },
        new CtData { Freq = 121, Len = 8 }, new CtData { Freq = 249, Len = 8 }, new CtData { Freq = 5, Len = 8 }, new CtData { Freq = 133, Len = 8 }, new CtData { Freq = 69, Len = 8 },
        new CtData { Freq = 197, Len = 8 }, new CtData { Freq = 37, Len = 8 }, new CtData { Freq = 165, Len = 8 }, new CtData { Freq = 101, Len = 8 }, new CtData { Freq = 229, Len = 8 },
        new CtData { Freq = 21, Len = 8 }, new CtData { Freq = 149, Len = 8 }, new CtData { Freq = 85, Len = 8 }, new CtData { Freq = 213, Len = 8 }, new CtData { Freq = 53, Len = 8 },
        new CtData { Freq = 181, Len = 8 }, new CtData { Freq = 117, Len = 8 }, new CtData { Freq = 245, Len = 8 }, new CtData { Freq = 13, Len = 8 }, new CtData { Freq = 141, Len = 8 },
        new CtData { Freq = 77, Len = 8 }, new CtData { Freq = 205, Len = 8 }, new CtData { Freq = 45, Len = 8 }, new CtData { Freq = 173, Len = 8 }, new CtData { Freq = 109, Len = 8 },
        new CtData { Freq = 237, Len = 8 }, new CtData { Freq = 29, Len = 8 }, new CtData { Freq = 157, Len = 8 }, new CtData { Freq = 93, Len = 8 }, new CtData { Freq = 221, Len = 8 },
        new CtData { Freq = 61, Len = 8 }, new CtData { Freq = 189, Len = 8 }, new CtData { Freq = 125, Len = 8 }, new CtData { Freq = 253, Len = 8 }, new CtData { Freq = 19, Len = 9 },
        new CtData { Freq = 275, Len = 9 }, new CtData { Freq = 147, Len = 9 }, new CtData { Freq = 403, Len = 9 }, new CtData { Freq = 83, Len = 9 }, new CtData { Freq = 339, Len = 9 },
        new CtData { Freq = 211, Len = 9 }, new CtData { Freq = 467, Len = 9 }, new CtData { Freq = 51, Len = 9 }, new CtData { Freq = 307, Len = 9 }, new CtData { Freq = 179, Len = 9 },
        new CtData { Freq = 435, Len = 9 }, new CtData { Freq = 115, Len = 9 }, new CtData { Freq = 371, Len = 9 }, new CtData { Freq = 243, Len = 9 }, new CtData { Freq = 499, Len = 9 },
        new CtData { Freq = 11, Len = 9 }, new CtData { Freq = 267, Len = 9 }, new CtData { Freq = 139, Len = 9 }, new CtData { Freq = 395, Len = 9 }, new CtData { Freq = 75, Len = 9 },
        new CtData { Freq = 331, Len = 9 }, new CtData { Freq = 203, Len = 9 }, new CtData { Freq = 459, Len = 9 }, new CtData { Freq = 43, Len = 9 }, new CtData { Freq = 299, Len = 9 },
        new CtData { Freq = 171, Len = 9 }, new CtData { Freq = 427, Len = 9 }, new CtData { Freq = 107, Len = 9 }, new CtData { Freq = 363, Len = 9 }, new CtData { Freq = 235, Len = 9 },
        new CtData { Freq = 491, Len = 9 }, new CtData { Freq = 27, Len = 9 }, new CtData { Freq = 283, Len = 9 }, new CtData { Freq = 155, Len = 9 }, new CtData { Freq = 411, Len = 9 },
        new CtData { Freq = 91, Len = 9 }, new CtData { Freq = 347, Len = 9 }, new CtData { Freq = 219, Len = 9 }, new CtData { Freq = 475, Len = 9 }, new CtData { Freq = 59, Len = 9 },
        new CtData { Freq = 315, Len = 9 }, new CtData { Freq = 187, Len = 9 }, new CtData { Freq = 443, Len = 9 }, new CtData { Freq = 123, Len = 9 }, new CtData { Freq = 379, Len = 9 },
        new CtData { Freq = 251, Len = 9 }, new CtData { Freq = 507, Len = 9 }, new CtData { Freq = 7, Len = 9 }, new CtData { Freq = 263, Len = 9 }, new CtData { Freq = 135, Len = 9 },
        new CtData { Freq = 391, Len = 9 }, new CtData { Freq = 71, Len = 9 }, new CtData { Freq = 327, Len = 9 }, new CtData { Freq = 199, Len = 9 }, new CtData { Freq = 455, Len = 9 },
        new CtData { Freq = 39, Len = 9 }, new CtData { Freq = 295, Len = 9 }, new CtData { Freq = 167, Len = 9 }, new CtData { Freq = 423, Len = 9 }, new CtData { Freq = 103, Len = 9 },
        new CtData { Freq = 359, Len = 9 }, new CtData { Freq = 231, Len = 9 }, new CtData { Freq = 487, Len = 9 }, new CtData { Freq = 23, Len = 9 }, new CtData { Freq = 279, Len = 9 },
        new CtData { Freq = 151, Len = 9 }, new CtData { Freq = 407, Len = 9 }, new CtData { Freq = 87, Len = 9 }, new CtData { Freq = 343, Len = 9 }, new CtData { Freq = 215, Len = 9 },
        new CtData { Freq = 471, Len = 9 }, new CtData { Freq = 55, Len = 9 }, new CtData { Freq = 311, Len = 9 }, new CtData { Freq = 183, Len = 9 }, new CtData { Freq = 439, Len = 9 },
        new CtData { Freq = 119, Len = 9 }, new CtData { Freq = 375, Len = 9 }, new CtData { Freq = 247, Len = 9 }, new CtData { Freq = 503, Len = 9 }, new CtData { Freq = 15, Len = 9 },
        new CtData { Freq = 271, Len = 9 }, new CtData { Freq = 143, Len = 9 }, new CtData { Freq = 399, Len = 9 }, new CtData { Freq = 79, Len = 9 }, new CtData { Freq = 335, Len = 9 },
        new CtData { Freq = 207, Len = 9 }, new CtData { Freq = 463, Len = 9 }, new CtData { Freq = 47, Len = 9 }, new CtData { Freq = 303, Len = 9 }, new CtData { Freq = 175, Len = 9 },
        new CtData { Freq = 431, Len = 9 }, new CtData { Freq = 111, Len = 9 }, new CtData { Freq = 367, Len = 9 }, new CtData { Freq = 239, Len = 9 }, new CtData { Freq = 495, Len = 9 },
        new CtData { Freq = 31, Len = 9 }, new CtData { Freq = 287, Len = 9 }, new CtData { Freq = 159, Len = 9 }, new CtData { Freq = 415, Len = 9 }, new CtData { Freq = 95, Len = 9 },
        new CtData { Freq = 351, Len = 9 }, new CtData { Freq = 223, Len = 9 }, new CtData { Freq = 479, Len = 9 }, new CtData { Freq = 63, Len = 9 }, new CtData { Freq = 319, Len = 9 },
        new CtData { Freq = 191, Len = 9 }, new CtData { Freq = 447, Len = 9 }, new CtData { Freq = 127, Len = 9 }, new CtData { Freq = 383, Len = 9 }, new CtData { Freq = 255, Len = 9 },
        new CtData { Freq = 511, Len = 9 }, new CtData { Freq = 0, Len = 7 }, new CtData { Freq = 64, Len = 7 }, new CtData { Freq = 32, Len = 7 }, new CtData { Freq = 96, Len = 7 },
        new CtData { Freq = 16, Len = 7 }, new CtData { Freq = 80, Len = 7 }, new CtData { Freq = 48, Len = 7 }, new CtData { Freq = 112, Len = 7 }, new CtData { Freq = 8, Len = 7 },
        new CtData { Freq = 72, Len = 7 }, new CtData { Freq = 40, Len = 7 }, new CtData { Freq = 104, Len = 7 }, new CtData { Freq = 24, Len = 7 }, new CtData { Freq = 88, Len = 7 },
        new CtData { Freq = 56, Len = 7 }, new CtData { Freq = 120, Len = 7 }, new CtData { Freq = 4, Len = 7 }, new CtData { Freq = 68, Len = 7 }, new CtData { Freq = 36, Len = 7 },
        new CtData { Freq = 100, Len = 7 }, new CtData { Freq = 20, Len = 7 }, new CtData { Freq = 84, Len = 7 }, new CtData { Freq = 52, Len = 7 }, new CtData { Freq = 116, Len = 7 },
        new CtData { Freq = 3, Len = 8 }, new CtData { Freq = 131, Len = 8 }, new CtData { Freq = 67, Len = 8 }, new CtData { Freq = 195, Len = 8 }, new CtData { Freq = 35, Len = 8 },
        new CtData { Freq = 163, Len = 8 }, new CtData { Freq = 99, Len = 8 }, new CtData { Freq = 227, Len = 8 }
        };

    public static readonly CtData[] StaticDistanceTree = new CtData[DeflateTrees.DistanceCodes]{
        new CtData { Freq =  0, Len =  5}, new CtData { Freq = 16, Len =  5}, new CtData { Freq =  8, Len =  5}, new CtData { Freq = 24, Len =  5}, new CtData { Freq =  4, Len =  5}, 
        new CtData { Freq = 20, Len =  5}, new CtData { Freq = 12, Len =  5}, new CtData { Freq = 28, Len =  5}, new CtData { Freq =  2, Len =  5}, new CtData { Freq = 18, Len =  5}, 
        new CtData { Freq = 10, Len =  5}, new CtData { Freq = 26, Len =  5}, new CtData { Freq =  6, Len =  5}, new CtData { Freq = 22, Len =  5}, new CtData { Freq = 14, Len =  5}, 
        new CtData { Freq = 30, Len =  5}, new CtData { Freq =  1, Len =  5}, new CtData { Freq = 17, Len =  5}, new CtData { Freq =  9, Len =  5}, new CtData { Freq = 25, Len =  5}, 
        new CtData { Freq =  5, Len =  5}, new CtData { Freq = 21, Len =  5}, new CtData { Freq = 13, Len =  5}, new CtData { Freq = 29, Len =  5}, new CtData { Freq =  3, Len =  5}, 
        new CtData { Freq = 19, Len =  5}, new CtData { Freq = 11, Len =  5}, new CtData { Freq = 27, Len =  5}, new CtData { Freq =  7, Len =  5}, new CtData { Freq = 23, Len =  5}
    };
    /* Distance codes. The first 256 values correspond to the distances
     * 3 .. 258, the last 256 values correspond to the top 8 bits of
     * the 15 bit distances.
     */
    public static readonly byte[] DistanceCode = new byte[] {
         0,  1,  2,  3,  4,  4,  5,  5,  6,  6,  6,  6,  7,  7,  7,  7,  8,  8,  8,  8,
         8,  8,  8,  8,  9,  9,  9,  9,  9,  9,  9,  9, 10, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11,
        11, 11, 11, 11, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
        12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 13, 13, 13, 13,
        13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
        13, 13, 13, 13, 13, 13, 13, 13, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,  0,  0, 16, 17,
        18, 18, 19, 19, 20, 20, 20, 20, 21, 21, 21, 21, 22, 22, 22, 22, 22, 22, 22, 22,
        23, 23, 23, 23, 23, 23, 23, 23, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25,
        26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26,
        26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 27, 27, 27, 27, 27, 27, 27, 27,
        27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27,
        27, 27, 27, 27, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
        28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
        28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
        28, 28, 28, 28, 28, 28, 28, 28, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29,
        29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29,
        29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29,
        29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29
    };
    public static readonly byte[] LengthCode = new byte[DeflateTrees.MaxMatch - DeflateTrees.MinMatch+1]
    {
        0,  1,  2,  3,  4,  5,  6,  7,  8,  8,  9,  9, 10, 10, 11, 11, 12, 12, 12, 12,
        13, 13, 13, 13, 14, 14, 14, 14, 15, 15, 15, 15, 16, 16, 16, 16, 16, 16, 16, 16,
        17, 17, 17, 17, 17, 17, 17, 17, 18, 18, 18, 18, 18, 18, 18, 18, 19, 19, 19, 19,
        19, 19, 19, 19, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20,
        21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 22, 22, 22, 22,
        22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 23, 23, 23, 23, 23, 23, 23, 23,
        23, 23, 23, 23, 23, 23, 23, 23, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24,
        25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25,
        25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 26, 26, 26, 26, 26, 26, 26, 26,
        26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26,
        26, 26, 26, 26, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27,
        27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 28
    };
    public static readonly int[] baseLength = new int[DeflateTrees.LengthCodes] 
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 14, 16, 20, 24, 28, 32, 40, 48, 56,
        64, 80, 96, 112, 128, 160, 192, 224, 0
    };
    public static readonly int[] baseLDistance = new int[DeflateTrees.DistanceCodes]
    {
        0,     1,     2,     3,     4,     6,     8,    12,    16,    24,
        32,    48,    64,    96,   128,   192,   256,   384,   512,   768,
        1024,  1536,  2048,  3072,  4096,  6144,  8192, 12288, 16384, 24576
    };
}
