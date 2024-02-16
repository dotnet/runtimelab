// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Collections;

#if MTOUCH
using MonoTouch;
#endif

namespace Xamarin
{
    public class MachO
    {
        /* definitions from: /usr/include/mach-o/loader.h */
        /* Constant for the magic field of the mach_header (32-bit architectures) */
        internal const uint MH_MAGIC = 0xfeedface; /* the mach magic number */
        internal const uint MH_CIGAM = 0xcefaedfe; /* NXSwapInt(MH_MAGIC) */

        /* Constant for the magic field of the mach_header_64 (64-bit architectures) */
        internal const uint MH_MAGIC_64 = 0xfeedfacf; /* the 64-bit mach magic number */
        internal const uint MH_CIGAM_64 = 0xcffaedfe; /* NXSwapInt(MH_MAGIC_64) */

        /* definitions from: /usr/include/mach-o/fat.h */
        internal const uint FAT_MAGIC = 0xcafebabe;
        internal const uint FAT_CIGAM = 0xbebafeca; /* NXSwapLong(FAT_MAGIC) */

        public enum Architectures
        {
            None = 0,
            i386 = 1,
            ARMv6 = 2,
            ARMv7 = 4,
            ARMv7s = 8,
            ARM64 = 16,
            x86_64 = 32,
            ARM64e = 64,
            ARM64_32 = 128,
        }

        public enum LoadCommands : uint
        {
            //#define LC_REQ_DYLD 0x80000000
            ReqDyld = 0x80000000,
            //
            //          /* Constants for the cmd field of all load commands, the type */
            //#define   LC_SEGMENT  0x1 /* segment of this file to be mapped */
            Segment = 0x1,
            //#define   LC_SYMTAB   0x2 /* link-edit stab symbol table info */
            SymTab = 0x2,
            //#define   LC_SYMSEG   0x3 /* link-edit gdb symbol table info (obsolete) */
            SymSeg = 0x3,
            //#define   LC_THREAD   0x4 /* thread */
            Thread = 0x4,
            //#define   LC_UNIXTHREAD   0x5 /* unix thread (includes a stack) */
            UnixThread = 0x5,
            //#define   LC_LOADFVMLIB   0x6 /* load a specified fixed VM shared library */
            LoadFVMLib = 0x6,
            //#define   LC_IDFVMLIB 0x7 /* fixed VM shared library identification */
            IDFVMLib = 0x7,
            //#define   LC_IDENT    0x8 /* object identification info (obsolete) */
            Ident = 0x8,
            //#define LC_FVMFILE    0x9 /* fixed VM file inclusion (internal use) */
            FVMFile = 0x9,
            //#define LC_PREPAGE      0xa     /* prepage command (internal use) */
            Prepage = 0xa,
            //#define   LC_DYSYMTAB 0xb /* dynamic link-edit symbol table info */
            DySymTab = 0x0b,

            //#define   LC_LOAD_DYLIB   0xc /* load a dynamically linked shared library */
            LoadDylib = 0xc,
            //#define   LC_ID_DYLIB 0xd /* dynamically linked shared lib ident */
            IdDylib = 0xd,
            //#define LC_LOAD_DYLINKER 0xe  /* load a dynamic linker */
            LoadDylinker = 0xe,
            //#define LC_ID_DYLINKER    0xf /* dynamic linker identification */
            IdDylinker = 0xf,
            //#define   LC_PREBOUND_DYLIB 0x10  /* modules prebound for a dynamically */
            PreboundDylib = 0x10,
            //          /*  linked shared library */
            //#define   LC_ROUTINES 0x11    /* image routines */
            Routines = 0x11,
            //#define   LC_SUB_FRAMEWORK 0x12   /* sub framework */
            SubFramework = 0x12,
            //#define   LC_SUB_UMBRELLA 0x13    /* sub umbrella */
            SubUmbrella = 0x13,
            //#define   LC_SUB_CLIENT   0x14    /* sub client */
            SubClient = 0x14,
            //#define   LC_SUB_LIBRARY  0x15    /* sub library */
            SubLibrary = 0x15,
            //#define   LC_TWOLEVEL_HINTS 0x16  /* two-level namespace lookup hints */
            TwolevelHints = 0x16,
            //#define   LC_PREBIND_CKSUM  0x17  /* prebind checksum */
            PrebindChecksum = 0x17,
            //
            //          /*
            //          * load a dynamically linked shared library that is allowed to be missing
            //          * (all symbols are weak imported).
            //          */
            //#define   LC_LOAD_WEAK_DYLIB (0x18 | LC_REQ_DYLD)
            LoadWeakDylib = 0x18 | ReqDyld,
            //
            //#define   LC_SEGMENT_64   0x19    /* 64-bit segment of this file to be
            Segment64 = 0x19,
            //          mapped */
            //#define   LC_ROUTINES_64  0x1a    /* 64-bit image routines */
            Routines64 = 0x1a,
            //#define LC_UUID       0x1b    /* the uuid */
            Uuid = 0x1b,
            //#define LC_RPATH       (0x1c | LC_REQ_DYLD)    /* runpath additions */
            RPath = 0x1c | ReqDyld,
            //#define LC_CODE_SIGNATURE 0x1d    /* local of code signature */
            CodeSignature = 0x1d,
            //#define LC_SEGMENT_SPLIT_INFO 0x1e /* local of info to split segments */
            SegmentSplitInfo = 0x1e,
            //#define LC_REEXPORT_DYLIB (0x1f | LC_REQ_DYLD) /* load and re-export dylib */
            ReexportDylib = 0x1f | ReqDyld,
            //#define   LC_LAZY_LOAD_DYLIB 0x20 /* delay load of dylib until first use */
            LazyLoadDylib = 0x20,
            //#define   LC_ENCRYPTION_INFO 0x21 /* encrypted segment information */
            EncryptionInfo = 0x21,
            //#define   LC_DYLD_INFO    0x22    /* compressed dyld information */
            DyldInfo = 0x22,
            //#define   LC_DYLD_INFO_ONLY (0x22|LC_REQ_DYLD)    /* compressed dyld information only */
            DyldInfoOnly = 0x22 | ReqDyld,
            //#define   LC_LOAD_UPWARD_DYLIB (0x23 | LC_REQ_DYLD) /* load upward dylib */
            LoadUpwardDylib = 0x23 | ReqDyld,
            //#define LC_VERSION_MIN_MACOSX 0x24   /* build for MacOSX min OS version */
            VersionMinMacOS = 0x24,
            //#define LC_VERSION_MIN_IPHONEOS 0x25 /* build for iPhoneOS min OS version */
            VersionMinIPhoneOS = 0x25,
            //#define LC_FUNCTION_STARTS 0x26 /* compressed table of function start addresses */
            FunctionStarts = 0x26,
            //#define LC_DYLD_ENVIRONMENT 0x27 /* string for dyld to treat
            DyldEnvironment = 0x27,
            //          like environment variable */
            //#define LC_MAIN (0x28|LC_REQ_DYLD) /* replacement for LC_UNIXTHREAD */
            Main = 0x28 | ReqDyld,
            //#define LC_DATA_IN_CODE 0x29 /* table of non-instructions in __text */
            DataInCode = 0x29,
            //#define LC_SOURCE_VERSION 0x2A /* source version used to build binary */
            SourceVersion = 0x2a,
            //#define LC_DYLIB_CODE_SIGN_DRS 0x2B /* Code signing DRs copied from linked dylibs */
            DylibCodeSignDrs = 0x2b,
            //#define   LC_ENCRYPTION_INFO_64 0x2C /* 64-bit encrypted segment information */
            EncryptionInfo64 = 0x2c,
            //#define LC_LINKER_OPTION 0x2D /* linker options in MH_OBJECT files */
            VersionMinTVOS = 0x2f,
            //#define LC_BUILD_VERSION 0x32 /* build for platform min OS version */
            VersionMinWatchOS = 0x30,
            //#define LC_NOTE 0x31 /* arbitrary data included within a Mach-O file */
            Note = 0x31,
            //#define LC_BUILD_VERSION 0x32 /* build for platform min OS version */
            BuildVersion = 0x32,
            //#define LC_DYLD_EXPORTS_TRIE (0x33 | LC_REQ_DYLD) /* used with linkedit_data_command, payload is trie */
            DyldExportsTrie = 0x33 | ReqDyld,
            //#define LC_DYLD_CHAINED_FIXUPS (0x34 | LC_REQ_DYLD) /* used with linkedit_data_command */
            DyldChainedFixups = 0x34 | ReqDyld,
        }

        public enum Platform : uint
        {
            MacOS = 1,
            IOS = 2,
            TvOS = 3,
            WatchOS = 4,
            BridgeOS = 5,
            IOSSimulator = 7,
            TvOSSimulator = 8,
            WatchOSSimulator = 9,
        }

        internal static uint FromBigEndian(uint number)
        {
            return (((number >> 24) & 0xFF)
                | ((number >> 08) & 0xFF00)
                | ((number << 08) & 0xFF0000)
                | ((number << 24)));
        }

        internal static int FromBigEndian(int number)
        {
            return (((number >> 24) & 0xFF)
                | ((number >> 08) & 0xFF00)
                | ((number << 08) & 0xFF0000)
                | ((number << 24)));
        }

        internal static uint ToBigEndian(uint number)
        {
            return (((number >> 24) & 0xFF)
                | ((number >> 08) & 0xFF00)
                | ((number << 08) & 0xFF0000)
                | ((number << 24)));
        }

        internal static int ToBigEndian(int number)
        {
            return (((number >> 24) & 0xFF)
                | ((number >> 08) & 0xFF00)
                | ((number << 08) & 0xFF0000)
                | ((number << 24)));
        }

        static IDisposable ReadFile(ReaderParameters parameters)
        {
            var reader = parameters.Reader;
            var magic = reader.ReadUInt32();
            reader.BaseStream.Position = 0;
            switch (magic)
            {
                case MH_MAGIC:
                case MH_MAGIC_64:
                    var mf = new MachOFile(parameters);
                    mf.Read();
                    return mf;
                case FAT_MAGIC: // little-endian fat binary
                case FAT_CIGAM: // big-endian fat binary
                    {
                        var f = new FatFile(parameters);
                        f.Read();
                        return f;
                    }
                default:
                    throw new Exception(string.Format("File format not recognized: {0} (magic: 0x{1})", parameters.Filename ?? "(no file name available)", magic.ToString("X")));
            }
        }

        public static MachOFileCollection Read(string filename, ReadingMode mode)
        {
            var parameters = new ReaderParameters();
            parameters.Filename = filename;
            parameters.ReadingMode = mode;
            parameters.Reader = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.UTF8, false);
            try
            {
                return GetMachOFiles(parameters, ReadFile(parameters));
            }
            finally
            {
                if (mode == ReadingMode.Immediate)
                    parameters.Dispose();
            }
        }

        static MachOFileCollection GetMachOFiles(ReaderParameters parameters, IDisposable readOutput)
        {
            var rv = new MachOFileCollection(parameters);
            if (readOutput is FatFile fatfile)
            {
                foreach (var ff in fatfile.entries)
                    rv.Add(ff.entry);
            }
            else
            {
                rv.Add((MachOFile)readOutput);
            }
            return rv;
        }

        public static IEnumerable<MachOFile> Read(Stream stm, string filename = null)
        {
            if (stm == null)
                throw new ArgumentNullException(nameof(stm));
            using (var parameters = new ReaderParameters())
            {
                parameters.ReadingMode = ReadingMode.Immediate;
                parameters.Reader = new BinaryReader(stm, Encoding.UTF8, true);
                parameters.Filename = filename;
                return GetMachOFiles(parameters, ReadFile(parameters));
            }
        }

        public static bool IsMachoFile(string filename)
        {
            using (FileStream stm = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return IsMachoFile(stm);
            }
        }

        public static bool IsMachoFile(Stream stm)
        {
            using (var reader = new BinaryReader(stm, UTF8Encoding.UTF8, true))
            {
                var magic = reader.ReadUInt32();
                reader.BaseStream.Position = 0;
                switch (magic)
                {
                    case MH_MAGIC:
                    case MH_MAGIC_64:
                    case FAT_MAGIC: // little-endian fat binary
                    case FAT_CIGAM: // big-endian fat binary
                        return true;
                    default:
                        return false;
                }
            }
        }



#if MTOUCH
        // Removes all architectures from the target file, except for those in 'architectures'.
        // This method doesn't do anything if the target file is a thin mach-o file.
        // Also it doesn't do anything if the result is an empty file (i.e. none of the
        // selected architectures match any of the architectures in the file) - this is
        // only because I haven't investigated what would be needed elsewhere in the link
        // process when the entire file is removed. FIXME <--.
        public static void SelectArchitectures (string filename, ICollection<Abi> abis)
        {
            var architectures = GetArchitectures (abis);
            var tmpfile = filename + ".tmp";

            if (abis.Count == 0)
                return;

            using (var fsr = new FileStream (filename, FileMode.Open, FileAccess.Read)) {
                using (var reader = new BinaryReader (fsr)) {
                    var file = ReadFile (filename);
                    if (file is MachOFile) {
                        MTouch.Log (2, "Skipping architecture selecting of '{0}', since it only contains 1 architecture.", filename);
                        return;
                    }

                    var fatfile = (FatFile) file;
                    bool any_removed = false;

                    // remove architectures we don't want
                    for (int i = fatfile.entries.Count - 1; i >= 0; i--) {
                        var ff = fatfile.entries [i];
                        if (!architectures.Contains (ff.entry.Architecture)) {
                            any_removed = true;
                            fatfile.entries.RemoveAt (i);
                            fatfile.nfat_arch--;
                            MTouch.Log (2, "Removing architecture {0} from {1}", ff.entry.Architecture, filename);
                        }
                    }

                    if (!any_removed) {
                        MTouch.Log (2, "Architecture selection of '{0}' didn't find any architectures to remove.", filename);
                        return;
                    }

                    if (fatfile.nfat_arch == 0) {
                        MTouch.Log (2, "Skipping architecture selection of '{0}', none of the selected architectures match any of the architectures in the archive.", filename, architectures [0]);
                        return;
                    }

                    if (fatfile.nfat_arch == 1) {
                        // Thin file
                        var entry = fatfile.entries [0];
                        using (var fsw = new FileStream (tmpfile, FileMode.Create, FileAccess.Write)) {
                            using (var writer = new BinaryWriter (fsw)) {
                                entry.WriteFile (writer, reader, entry.offset);
                            }
                        }
                    } else {
                        // Fat file
                        // Re-calculate header data
                        var read_offset = new List<uint> (fatfile.entries.Count);
                        read_offset.Add (fatfile.entries [0].offset);
                        fatfile.entries [0].offset = (uint) (1 << (int) fatfile.entries [0].align);
                        for (int i = 1; i < fatfile.entries.Count; i++) {
                            read_offset.Add (fatfile.entries [i].offset);
                            fatfile.entries [i].offset = fatfile.entries [i - 1].offset + fatfile.entries [i - 1].size;
                            var alignSize = (1 << (int) fatfile.entries [i].align);
                            var align = (int) fatfile.entries [i].offset % alignSize;
                            if (align != 0)
                                fatfile.entries [i].offset += (uint) (alignSize - align);
                        }
                        // Write out the fat file
                        using (var fsw = new FileStream (tmpfile, FileMode.Create, FileAccess.Write)) {
                            using (var writer = new BinaryWriter (fsw)) {
                                // write headers
                                fatfile.WriteHeaders (writer);
                                // write data
                                for (int i = 0; i < fatfile.entries.Count; i++) {
                                    fatfile.entries [i].Write (writer, reader, read_offset [i]);
                                }
                            }
                        }
                    }
                }
            }

            File.Delete (filename);
            File.Move (tmpfile, filename);
        }
#endif

        static Dictionary<string, IEnumerable<string>> native_dependencies = new Dictionary<string, IEnumerable<string>>();

        public static IEnumerable<string> GetNativeDependencies(string libraryName)
        {
            IEnumerable<string> result;
            lock (native_dependencies)
            {
                if (native_dependencies.TryGetValue(libraryName, out result))
                    return result;
            }

            var macho_files = Read(libraryName, ReadingMode.Deferred);
            var dependencies = new HashSet<string>();
            foreach (var macho_file in macho_files)
            {
                foreach (var lc in macho_file.load_commands)
                {
                    var dyld_lc = lc as Xamarin.DylibLoadCommand;
                    if (dyld_lc != null)
                    {
                        dependencies.Add(dyld_lc.name);
                    }
                }
            }
            result = dependencies;
            lock (native_dependencies)
                native_dependencies.Add(libraryName, result);
            return result;
        }

#if MTOUCH
        public static List<Abi> GetArchitectures (string file)
        {
            var result = new List<Abi> ();

            // https://developer.apple.com/library/mac/#documentation/DeveloperTools/Conceptual/MachORuntime/Reference/reference.html

            using (var fs = File.OpenRead (file)) {
                using (var reader = new BinaryReader (fs)) {
                    int magic = reader.ReadInt32 ();
                    int architectures;
                    switch ((uint) magic) {
                    case 0xCAFEBABE: // little-endian fat binary
                        architectures = reader.ReadInt32 ();
                        for (int i = 0; i < architectures; i++) {
                            result.Add (GetArch (reader.ReadInt32 (), reader.ReadInt32 ()));
                            // skip to next entry
                            reader.ReadInt32 (); // offset
                            reader.ReadInt32 (); // size
                            reader.ReadInt32 (); // align
                        }
                        break;
                    case 0xBEBAFECA:
                        architectures = System.Net.IPAddress.NetworkToHostOrder (reader.ReadInt32 ());
                        for (int i = 0; i < architectures; i++) {
                            result.Add (GetArch (System.Net.IPAddress.NetworkToHostOrder (reader.ReadInt32 ()), System.Net.IPAddress.NetworkToHostOrder (reader.ReadInt32 ())));
                            // skip to next entry
                            reader.ReadInt32 (); // offset
                            reader.ReadInt32 (); // size
                            reader.ReadInt32 (); // align
                        }
                        break;
                    case 0xFEEDFACE: // little-endian mach-o header
                    case 0xFEEDFACF: // little-endian 64-big mach-o header
                        result.Add (GetArch (reader.ReadInt32 (), reader.ReadInt32 ()));
                        break;
                    case 0xCFFAEDFE:
                    case 0xCEFAEDFE:
                        result.Add (GetArch (System.Net.IPAddress.NetworkToHostOrder (reader.ReadInt32 ()), System.Net.IPAddress.NetworkToHostOrder (reader.ReadInt32 ())));
                        break;
                    default:
                        Console.WriteLine ("File '{0}' is neither a Universal binary nor a Mach-O binary (magic: 0x{1})", file, magic.ToString ("x"));
                        break;
                    }
                }
            }

            return result;
        }

        public static List<Architectures> GetArchitectures (ICollection<Abi> abi)
        {
            var rv = new List<Architectures> (abi.Count);
            foreach (var a in abi) {
                rv.Add ((Architectures) (a & Abi.ArchMask));
            }
            return rv;
        }

        static Abi GetArch (int cputype, int cpusubtype)
        {
            switch (cputype) {
            case 12: // arm
                switch (cpusubtype) {
                case 6:
                    return Abi.ARMv6;
                case 9:
                    return Abi.ARMv7;
                case 11:
                    return Abi.ARMv7s;
                default:
                    return Abi.None;
                }
            case 12 | 0x01000000:
                return Abi.ARM64;
            case 7: // x86
                return Abi.i386;
            case 7 | 0x01000000: // x64
                return Abi.x86_64;
            }

            return Abi.None;
        }
#endif
    }

    public class StaticLibrary
    {
        public static bool IsStaticLibrary(BinaryReader reader)
        {
            var pos = reader.BaseStream.Position;

            var bytes = reader.ReadBytes(8);
            var rv = bytes[0] == '!' && bytes[1] == '<' && bytes[2] == 'a' && bytes[3] == 'r' && bytes[4] == 'c' && bytes[5] == 'h' && bytes[6] == '>' && bytes[7] == 0xa;
            reader.BaseStream.Position = pos;

            return rv;
        }
    }

    public enum ReadingMode
    {
        Immediate = 1,
        Deferred = 2,
    }

    sealed class ReaderParameters : IDisposable
    {
        public BinaryReader Reader;
        public string Filename;
        public ReadingMode ReadingMode { get; set; }

        #region IDisposable Support
        void Dispose(bool disposing)
        {
            Reader?.Dispose();
        }

        ~ReaderParameters()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class MachOFileCollection : List<MachOFile>, IDisposable
    {
        internal ReaderParameters Parameters { get; private set; }

        internal MachOFileCollection(ReaderParameters parameters)
        {
            Parameters = parameters;
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            Parameters.Dispose();
        }

        ~MachOFileCollection()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class MachOFile : IDisposable
    {
        internal ReaderParameters parameters { get; private set; }
        public uint magic;
        public int cputype;
        public int cpusubtype;
        public uint filetype;
        public uint ncmds;
        public uint sizeofcmds;
        public uint flags;
        public uint reserved;

        public List<LoadCommand> load_commands;

        internal MachOFile(ReaderParameters parameters)
        {
            this.parameters = parameters;
        }

        internal void WriteHeader(BinaryWriter writer)
        {
            writer.Write(magic);
            writer.Write(cputype);
            writer.Write(cpusubtype);
            writer.Write(filetype);
            writer.Write(ncmds);
            writer.Write(sizeofcmds);
            writer.Write((uint)flags);
            if (magic == MachO.MH_MAGIC_64)
                writer.Write(reserved);
        }

        internal static bool IsMachOLibrary(BinaryReader reader)
        {
            var pos = reader.BaseStream.Position;
            var magic = reader.ReadUInt32();
            var rv = false;
            switch (magic)
            {
                case MachO.MH_CIGAM:
                case MachO.MH_MAGIC:
                case MachO.MH_CIGAM_64:
                case MachO.MH_MAGIC_64:
                    rv = true;
                    break;
                default:
                    rv = false;
                    break;
            }
            reader.BaseStream.Position = pos;
            return rv;
        }

        internal void Read()
        {
            /* definitions from: /usr/include/mach-o/loader.h */
            /*
			* The 32-bit mach header appears at the very beginning of the object file for
			    * 32-bit architectures.
			    */
            //  struct mach_header {
            //      uint32_t    magic;      /* mach magic number identifier */
            //      cpu_type_t  cputype;    /* cpu specifier */
            //      cpu_subtype_t   cpusubtype; /* machine specifier */
            //      uint32_t    filetype;   /* type of file */
            //      uint32_t    ncmds;      /* number of load commands */
            //      uint32_t    sizeofcmds; /* the size of all the load commands */
            //      uint32_t    flags;      /* flags */
            //  };

            /*
		    * The 64-bit mach header appears at the very beginning of object files for
		    * 64-bit architectures.
		    */
            //  struct mach_header_64 {
            //      uint32_t    magic;      /* mach magic number identifier */
            //      cpu_type_t  cputype;    /* cpu specifier */
            //      cpu_subtype_t   cpusubtype; /* machine specifier */
            //      uint32_t    filetype;   /* type of file */
            //      uint32_t    ncmds;      /* number of load commands */
            //      uint32_t    sizeofcmds; /* the size of all the load commands */
            //      uint32_t    flags;      /* flags */
            //      uint32_t    reserved;   /* reserved */
            //  };
            var reader = parameters.Reader;
            StartOffset = reader.BaseStream.Position;
            if (!MachOFile.IsMachOLibrary(reader))
            {
                if (StaticLibrary.IsStaticLibrary(reader))
                {
                    throw new Exception("Static libraries are not supported (yet).");
                }
                throw new Exception($"Unknown format for fat entry at position {StartOffset}");
            }
            magic = reader.ReadUInt32();
            cputype = reader.ReadInt32();
            cpusubtype = reader.ReadInt32();
            filetype = reader.ReadUInt32();
            ncmds = reader.ReadUInt32();
            sizeofcmds = reader.ReadUInt32();
            flags = reader.ReadUInt32();
            if (magic == MachO.MH_MAGIC_64)
                reserved = reader.ReadUInt32();
            var cmds = new List<LoadCommand>((int)ncmds);
            for (int i = 0; i < ncmds; i++)
            {
                var cmd = (MachO.LoadCommands)reader.ReadUInt32();
                reader.BaseStream.Position -= 4;
                LoadCommand lc;
                switch (cmd)
                {
                    case MachO.LoadCommands.LoadDylib:
                    case MachO.LoadCommands.LoadWeakDylib:
                    case MachO.LoadCommands.ReexportDylib:
                        lc = DylibLoadCommand.FromBinaryReader(reader);
                        break;
                    case MachO.LoadCommands.SymTab:
                        lc = SymTabLoadCommand.FromBinaryReader(this, StartOffset);
                        break;
                    case MachO.LoadCommands.VersionMinTVOS:
                    case MachO.LoadCommands.VersionMinMacOS:
                    case MachO.LoadCommands.VersionMinIPhoneOS:
                    case MachO.LoadCommands.VersionMinWatchOS:
                        lc = VersionMinOSLoadCommand.FromBinaryReader(reader);
                        break;
                    case MachO.LoadCommands.BuildVersion:
                        var buildVer = new BuildVersionCommand();
                        buildVer.cmd = reader.ReadUInt32();
                        buildVer.cmdsize = reader.ReadUInt32();
                        buildVer.platform = reader.ReadUInt32();
                        buildVer.minos = reader.ReadUInt32();
                        buildVer.sdk = reader.ReadUInt32();
                        buildVer.ntools = reader.ReadUInt32();
                        buildVer.tools = new BuildVersionCommand.BuildToolVersion[buildVer.ntools];
                        for (int j = 0; j < buildVer.ntools; j++)
                        {
                            var buildToolVer = new BuildVersionCommand.BuildToolVersion();
                            buildToolVer.tool = reader.ReadUInt32();
                            buildToolVer.version = reader.ReadUInt32();
                            buildVer.tools[j] = buildToolVer;
                        }
                        lc = buildVer;
                        break;
                    default:
                        lc = new LoadCommand();
                        lc.cmd = reader.ReadUInt32();
                        lc.cmdsize = reader.ReadUInt32();
                        reader.BaseStream.Position += lc.cmdsize - 8;
                        break;
                }
                cmds.Add(lc);
            }
            load_commands = cmds;
        }

        public MachO.Architectures Architecture
        {
            get
            {
                switch (cputype)
                {
                    case 12: // arm
                        switch (cpusubtype)
                        {
                            case 6:
                                return MachO.Architectures.ARMv6;
                            case 9:
                                return MachO.Architectures.ARMv7;
                            case 11:
                                return MachO.Architectures.ARMv7s;
                            default:
                                return MachO.Architectures.None;
                        }
                    case 12 | 0x01000000:
                        switch (cpusubtype)
                        {
                            case 2:
                                return MachO.Architectures.ARM64e;
                            case 0:
                            default:
                                return MachO.Architectures.ARM64;
                        }
                    case 12 | 0x02000000:
                        switch (cpusubtype & ~0xff000000)
                        {
                            case 1:
                                return MachO.Architectures.ARM64_32;
                            default:
                                return MachO.Architectures.ARM64;
                        }
                    case 7: // x86
                        return MachO.Architectures.i386;
                    case 7 | 0x01000000: // x64
                        return MachO.Architectures.x86_64;
                }

                return MachO.Architectures.None;
            }
        }

        public bool Is32Bit
        {
            get
            {
                switch (Architecture)
                {
                    case MachO.Architectures.ARMv6:
                    case MachO.Architectures.ARMv7:
                    case MachO.Architectures.ARMv7s:
                    case MachO.Architectures.i386:
                    case MachO.Architectures.None: // not sure what to do with None.
                        return true;
                    default:
                        return false;
                }
            }
        }

        public long StartOffset { get; private set; }

        public class MinOSVersion
        {
            public Version Version;
            public string OSName
            {
                get
                {
                    switch (Platform)
                    {
                        case MachO.Platform.IOS:
                        case MachO.Platform.IOSSimulator:
                            return "ios";
                        case MachO.Platform.MacOS:
                            return "macosx";
                        case MachO.Platform.TvOS:
                        case MachO.Platform.TvOSSimulator:
                            return "tvos";
                        case MachO.Platform.WatchOS:
                        case MachO.Platform.WatchOSSimulator:
                            return "watchos";
                        default:
                            throw new ArgumentOutOfRangeException(Platform.ToString());
                    }
                }
            }
            public MachO.Platform Platform;
            public Version Sdk;
        }

        public MinOSVersion MinOS
        {
            get
            {
                uint? version = null;
                uint? sdk = null;
                MachO.Platform platform = (MachO.Platform)0;
                foreach (var lc in load_commands)
                {
                    if (lc is VersionMinOSLoadCommand min_lc)
                    {
                        if (version.HasValue)
                            throw new NotSupportedException("File has multiple minOS load commands.");
                        version = min_lc.version;
                        sdk = min_lc.sdk;

                        switch (min_lc.Command)
                        {
                            case MachO.LoadCommands.VersionMinMacOS:
                                platform = MachO.Platform.MacOS;
                                break;
                            case MachO.LoadCommands.VersionMinIPhoneOS:
                                platform = MachO.Platform.IOS;
                                break;
                            case MachO.LoadCommands.VersionMinTVOS:
                                platform = MachO.Platform.TvOS;
                                break;
                            case MachO.LoadCommands.VersionMinWatchOS:
                                platform = MachO.Platform.WatchOS;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(min_lc.Command));
                        }
                    }
                    else if (lc is BuildVersionCommand build_lc)
                    {
                        if (version.HasValue)
                            throw new NotSupportedException("File has multiple minOS load commands.");
                        version = build_lc.minos;
                        sdk = build_lc.sdk;
                        platform = build_lc.Platform;
                    }
                }
                if (!version.HasValue)
                    return null;

                return new MinOSVersion
                {
                    Version = BuildVersionCommand.DeNibble(version.Value),
                    Platform = platform,
                    Sdk = BuildVersionCommand.DeNibble(sdk.Value)
                };
            }
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            parameters?.Dispose();
        }

        ~MachOFile()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class FatFile : IDisposable
    {
        internal ReaderParameters parameters { get; private set; }
        public uint magic;
        public uint nfat_arch;

        public List<FatEntry> entries;
        internal FatFile(ReaderParameters parameters)
        {
            this.parameters = parameters;
        }

        internal bool is_big_endian
        {
            get { return magic == MachO.FAT_CIGAM; }
        }

        internal void WriteHeader(BinaryWriter writer)
        {
            writer.Write(magic);
            if (is_big_endian)
            {
                writer.Write(MachO.ToBigEndian(nfat_arch));
            }
            else
            {
                writer.Write(nfat_arch);
            }
        }

        internal void WriteHeaders(BinaryWriter writer)
        {
            WriteHeader(writer);
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].WriteHeader(writer);
            }
        }

        internal void Read()
        {
            var reader = parameters.Reader;
            magic = reader.ReadUInt32();
            nfat_arch = reader.ReadUInt32();
            if (is_big_endian)
                nfat_arch = MachO.FromBigEndian(nfat_arch);

            entries = new List<FatEntry>((int)nfat_arch);
            for (int i = 0; i < (int)nfat_arch; i++)
            {
                var entry = new FatEntry(this);
                entry.Read();
                entries.Add(entry);
            }
            foreach (var entry in entries)
                entry.ReadEntry();
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            parameters.Dispose();
        }

        ~FatFile()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class FatEntry
    {
        FatFile parent;
        public int cputype;
        public int cpusubtype;
        public uint offset;
        public uint size;
        public uint align;

        public MachOFile entry;

        public FatEntry(FatFile parent)
        {
            this.parent = parent;
        }

        internal void WriteHeader(BinaryWriter writer)
        {
            if (parent.is_big_endian)
            {
                writer.Write(MachO.ToBigEndian(cputype));
                writer.Write(MachO.ToBigEndian(cpusubtype));
                writer.Write(MachO.ToBigEndian(offset));
                writer.Write(MachO.ToBigEndian(size));
                writer.Write(MachO.ToBigEndian(align));
            }
            else
            {
                writer.Write(cputype);
                writer.Write(cpusubtype);
                writer.Write(offset);
                writer.Write(size);
                writer.Write(align);
            }
        }

        internal void Write(BinaryWriter writer, BinaryReader reader, uint reader_offset)
        {
            writer.BaseStream.Position = offset;
            // write data
            WriteFile(writer, reader, reader_offset);
        }

        internal void WriteFile(BinaryWriter writer, BinaryReader reader, uint reader_offset)
        {
            // write data
            var ofs = writer.BaseStream.Position;
            reader.BaseStream.Position = reader_offset;
            var buffer = new byte[1 << (int)align];
            var left = (int)size;
            while (left > 0)
            {
                var read = reader.Read(buffer, 0, Math.Min(buffer.Length, left));
                writer.Write(buffer, 0, read);
                left -= read;
            }
            writer.BaseStream.Position = ofs; // restore to the post-header location.
        }

        internal void Read()
        {
            var reader = parent.parameters.Reader;
            cputype = reader.ReadInt32();
            cpusubtype = reader.ReadInt32();
            offset = reader.ReadUInt32();
            size = reader.ReadUInt32();
            align = reader.ReadUInt32();

            if (parent.is_big_endian)
            {
                cputype = MachO.FromBigEndian(cputype);
                cpusubtype = MachO.FromBigEndian(cpusubtype);
                offset = MachO.FromBigEndian(offset);
                size = MachO.FromBigEndian(size);
                align = MachO.FromBigEndian(align);
            }
        }

        internal void ReadEntry()
        {
            var reader = parent.parameters.Reader;
            reader.BaseStream.Position = offset;
            entry = new MachOFile(parent.parameters);
            entry.Read();
        }
    }

    public class LoadCommand
    {
        public uint cmd;
        public uint cmdsize;

        public MachO.LoadCommands Command
        {
            get { return (MachO.LoadCommands)cmd; }
        }

#if DEBUG
        public virtual void Dump()
        {
            Console.WriteLine("    cmd: {0}", cmd);
            Console.WriteLine("    cmdsize: {0}", cmdsize);
        }
#endif

        public override string ToString()
        {
            return Command.ToString();
        }
    }

    public class DylibLoadCommand : LoadCommand
    {
        public string name;
        public uint timestamp;
        public uint current_version;
        public uint compatibility_version;

        public static LoadCommand FromBinaryReader(BinaryReader reader)
        {
            var dlc = new DylibLoadCommand();
            dlc.cmd = reader.ReadUInt32();
            dlc.cmdsize = reader.ReadUInt32();
            /*var nameofs = */
            reader.ReadUInt32();
            dlc.timestamp = reader.ReadUInt32();
            dlc.current_version = reader.ReadUInt32();
            dlc.compatibility_version = reader.ReadUInt32();
            var namelength = dlc.cmdsize - 6 * 4;
            var namechars = reader.ReadBytes((int)namelength);
            // strip off any null characters at the end.
            for (int n = namechars.Length - 1; n >= 0; n--)
            {
                if (namechars[n] == 0)
                    namelength--;
                else
                    break;
            }
            dlc.name = System.Text.UTF8Encoding.UTF8.GetString(namechars, 0, (int)namelength);
            return dlc;
        }

#if DEBUG
        public override void Dump()
        {
            base.Dump();
            Console.WriteLine("    name: {0}", name);
            Console.WriteLine("    timestamp: {0}", timestamp);
            Console.WriteLine("    current_version: {0}", current_version);
            Console.WriteLine("    compatibility_version: {0}", compatibility_version);
        }
#endif
    }

    public class VersionMinOSLoadCommand : LoadCommand
    {
        public uint version;
        public uint sdk;
        public static LoadCommand FromBinaryReader(BinaryReader reader)
        {
            var vmlc = new VersionMinOSLoadCommand();
            vmlc.cmd = reader.ReadUInt32();
            vmlc.cmdsize = reader.ReadUInt32();
            vmlc.version = reader.ReadUInt32();
            vmlc.sdk = reader.ReadUInt32();
            return vmlc;
        }
        string ToConventionalString(uint val)
        {
            uint major = val >> 16;
            uint minor = (val >> 8) & 0xff;
            uint sub = val & 0xff;
            if (sub == 0)
                return $"{major}.{minor}";
            else
                return $"{major}.{minor}.{sub}";
        }
        string ToOperatingSystemString(uint theCmd)
        {
            switch ((MachO.LoadCommands)theCmd)
            {
                case MachO.LoadCommands.VersionMinMacOS:
                    return "macosx";
                case MachO.LoadCommands.VersionMinIPhoneOS:
                    return "ios";
                case MachO.LoadCommands.VersionMinTVOS:
                    return "tvos";
                case MachO.LoadCommands.VersionMinWatchOS:
                    return "watchos";
                default:
                    throw new ArgumentOutOfRangeException(nameof(theCmd));
            }
        }

        public string ToOSVersionString()
        {
            return $"{ToConventionalString(version)}";
        }

        public string ToOSString(bool isx8664)
        {
            return $"{ToOperatingSystemString(cmd)}{ToConventionalString(version)}";
        }
    }



    public class SymTabLoadCommand : LoadCommand
    {
        MachOFile file;
        long startOffset;

        public uint symoff;     /* symbol table offset */
        public uint nsyms;      /* number of symbol table entries */
        public uint stroff;     /* string table offset */
        public uint strsize;    /* string table size in bytes */
        NListEntry[] _nlist;

        public NListEntry[] nlist
        {
            get
            {
                if (_nlist == null)
                    ReadNList();
                return _nlist;
            }
        }

        public static LoadCommand FromBinaryReader(MachOFile file, long startOffset)
        {
            var parameters = file.parameters;
            var reader = parameters.Reader;

            var stc = new SymTabLoadCommand();
            stc.file = file;
            stc.startOffset = startOffset;
            stc.cmd = reader.ReadUInt32();
            stc.cmdsize = reader.ReadUInt32();
            stc.symoff = reader.ReadUInt32();
            stc.nsyms = reader.ReadUInt32();
            stc.stroff = reader.ReadUInt32();
            stc.strsize = reader.ReadUInt32();
            if (parameters.ReadingMode == ReadingMode.Immediate)
                stc.ReadNList();
            return stc;
        }

        public void ReadNList()
        {
            var reader = file.parameters.Reader;
            _nlist = new NListEntry[nsyms];
            long savePos = reader.BaseStream.Position;
            try
            {
                reader.BaseStream.Seek(startOffset + symoff, SeekOrigin.Begin);
                for (uint i = 0; i < nsyms; i++)
                    _nlist[i] = NListEntry.FromBinaryReader(reader, file.Is32Bit);

                for (uint i = 0; i < nsyms; i++)
                {
                    var entry = _nlist[i];
                    reader.BaseStream.Seek(startOffset + stroff + entry.n_strx, SeekOrigin.Begin);
                    entry.str = ReadStringEntry(reader);
                }
            }
            finally
            {
                reader.BaseStream.Seek(savePos, SeekOrigin.Begin);
            }
        }


        static string ReadStringEntry(BinaryReader reader)
        {
            StringBuilder builder = new StringBuilder();
            byte b;
            while ((b = reader.ReadByte()) != 0)
            {
                builder.Append((char)b);
            }
            return builder.ToString();
        }

        IEnumerable<NListEntry> PublicSymbols
        {
            get
            {
                return nlist.Where((nle, i) => nle.IsPublic && !nle.IsSymbolicDebuggerEntry);
            }
        }

#if DEBUG
        public override void Dump()
        {
            base.Dump();
            Console.WriteLine("    symoff: {0}", symoff);
            Console.WriteLine("    nsyms: {0}", nsyms);
            Console.WriteLine("    stroff: {0}", stroff);
            Console.WriteLine("    strsize: {0}", strsize);
        }
#endif
    }

    public class BuildVersionCommand : LoadCommand
    {
        public uint platform;
        public uint minos; /* X.Y.Z is encoded in nibbles xxxx.yy.zz */
        public uint sdk; /* X.Y.Z is encoded in nibbles xxxx.yy.zz */
        public uint ntools;
        public BuildToolVersion[] tools;

        public class BuildToolVersion
        {
            public uint tool;
            public uint version;
        }

        public static Version DeNibble(uint value)
        {
            var major = (int)(value >> 16);
            var minor = (int)((value >> 8) & 0xff);
            var sub = (int)(value & 0xff);
            if (sub == 0)
            {
                // This makes sure the string version is a two-part version (X.Y) when the 'sub' version is 0,
                // otherwise various toolchain tools (swiftc among others) will complain.
                return new Version(major, minor);
            }
            else
            {
                // Here we have no choice but to be a three-part version (X.Y.Z).
                return new Version(major, minor, sub);
            }
        }

        public Version MinOS
        {
            get { return DeNibble(minos); }
        }

        public Version Sdk
        {
            get { return DeNibble(sdk); }
        }

        public MachO.Platform Platform
        {
            get { return (MachO.Platform)platform; }
        }
    }

    //  #define N_UNDF  0x0     /* undefined, n_sect == NO_SECT */
    //  #define N_ABS   0x2     /* absolute, n_sect == NO_SECT */
    //  #define N_SECT  0xe     /* defined in section number n_sect */
    //  #define N_PBUD  0xc     /* prebound undefined (defined in a dylib) */
    //  #define N_INDR  0xa     /* indirect */

    public enum NListEntryType
    {
        Undefined = 0,
        Absolute = 0x2,
        InSection = 0xe,
        PreboundUndefined = 0x0c,
        Indirect = 0x0a
    };

    public class NListEntry
    {
        const int kSymbolTableMask = 0xe0,
            kPrivateExternalMask = 0x10,
            kTypeMask = 0x0e,
            kExternalMask = 0x01;

        // from nlist.h
        public int n_strx;
        public byte n_type;
        public byte n_sect;
        public short n_desc;
        public string str;

        public static NListEntry FromBinaryReader(BinaryReader reader, bool is32Bit)
        {
            NListEntry entry = is32Bit ? (NListEntry)new NListEntry32() : (NListEntry)new NListEntry64();
            return entry.FromBinaryReader(reader);
        }

        protected virtual NListEntry FromBinaryReader(BinaryReader reader)
        {
            n_strx = reader.ReadInt32();
            n_type = reader.ReadByte();
            n_sect = reader.ReadByte();
            n_desc = reader.ReadInt16();
            return this;
        }

        public NListEntryType EntryType
        {
            get
            {
                switch (n_type & kTypeMask)
                {
                    case 0x2:
                        return NListEntryType.Absolute;
                    case 0xa:
                        return NListEntryType.Indirect;
                    case 0xc:
                        return NListEntryType.PreboundUndefined;
                    case 0xe:
                        return NListEntryType.InSection;
                    default:
                        return NListEntryType.Undefined;
                }
            }
        }

        public bool IsSymbolicDebuggerEntry { get { return (n_type & kSymbolTableMask) != 0; } }
        public bool IsPublic { get { return (n_type & kExternalMask) != 0; } }
        public bool IsPrivate { get { return (n_type & kPrivateExternalMask) != 0; } }

        public override string ToString()
        {
            return str ?? "";
        }
    }

    public class NListEntry32 : NListEntry
    {
        public uint n_value;

        protected override NListEntry FromBinaryReader(BinaryReader reader)
        {
            base.FromBinaryReader(reader);
            n_value = reader.ReadUInt32();
            return this;
        }
    }

    public class NListEntry64 : NListEntry
    {
        public ulong n_value;
        protected override NListEntry FromBinaryReader(BinaryReader reader)
        {
            base.FromBinaryReader(reader);
            n_value = reader.ReadUInt64();
            return this;
        }
    }
}

