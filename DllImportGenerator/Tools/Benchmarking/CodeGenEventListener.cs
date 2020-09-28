using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;

using Iced.Intel;

using Decoder = Iced.Intel.Decoder;

namespace Benchmarking
{
    internal class CodeGenEventListener : EventListener
    {
        // See src/coreclr/src/vm/ClrEtwAll.man
        private const int JitKeyword = 0x10;
        private const int InteropKeyword = 0x2000;

        // Event IDs
        private const int ILStubGeneratedId = 88;
        private const int VerboseMethodLoadId = 143;

        private readonly Dictionary<ulong, PInvokeInstance> pinvokeInstances = new Dictionary<ulong, PInvokeInstance>();

        /// <summary>
        /// Event fired when code is generated for a method.
        /// </summary>
        public event EventHandler<MethodCodeGen> NewMethodCodeGen;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.Equals("Microsoft-Windows-DotNETRuntime"))
            {
                this.EnableEvents(
                    eventSource,
                    EventLevel.Verbose,
                    (EventKeywords)(JitKeyword | InteropKeyword));
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            switch (eventData.EventId)
            {
                case ILStubGeneratedId: this.OnILStubGenerated(eventData); break;
                case VerboseMethodLoadId: this.OnVerboseMethodLoad(eventData); break;
            }
        }

        private void OnNewMethodCodeGen(MethodCodeGen mcg)
        {
            var h = this.NewMethodCodeGen;
            if (h is not null)
            {
                h(this, mcg);
            }
        }

        private void OnILStubGenerated(EventWrittenEventArgs eventData)
        {
            Debug.Assert(eventData.EventId == ILStubGeneratedId);

            ulong methodId = 0;
            string fqClassName = string.Empty;
            string methodName = string.Empty;
            uint metadataToken = 0;

            // See https://docs.microsoft.com/dotnet/framework/performance/interop-etw-events
            for (int i = 0; i < eventData.Payload.Count; i++)
            {
                object payload = eventData.Payload[i];
                string payloadName = eventData.PayloadNames[i];

                if (payloadName == "StubMethodID")
                {
                    methodId = (ulong)payload;
                }
                else if (payloadName == "ManagedInteropMethodNamespace")
                {
                    fqClassName = (string)payload;
                }
                else if (payloadName == "ManagedInteropMethodName")
                {
                    methodName = (string)payload;
                }
                else if (payloadName == "ManagedInteropMethodToken")
                {
                    metadataToken = (uint)payload;
                }
            }

            Debug.Assert(methodId != 0);
            pinvokeInstances.Add(methodId, new PInvokeInstance()
            {
                MetadataToken = metadataToken,
                FullyQualifiedClassName = fqClassName,
                MethodName = methodName
            });;
        }

        private void OnVerboseMethodLoad(EventWrittenEventArgs eventData)
        {
            Debug.Assert(eventData.EventId == VerboseMethodLoadId);

            using var generatedCode = new StringWriter();

            ulong methodId = 0;
            ulong startAddress = 0;
            uint size = 0;
            string namespaceName = "?";
            string methodName = "?";
            string methodSignature = "?";
            uint flags = 0;
            uint metadataToken = 0;

            // See https://docs.microsoft.com/dotnet/framework/performance/method-etw-events
            for (int i = 0; i < eventData.Payload.Count; i++)
            {
                object payload = eventData.Payload[i];
                string payloadName = eventData.PayloadNames[i];

                if (payloadName == "MethodID")
                {
                    methodId = (ulong)payload;
                }
                else if (payloadName == "MethodStartAddress")
                {
                    startAddress = (ulong)payload;
                }
                else if (payloadName == "MethodSize")
                {
                    size = (uint)payload;
                }
                else if (payloadName == "MethodNamespace")
                {
                    namespaceName = (string)payload;
                }
                else if (payloadName == "MethodName")
                {
                    methodName = (string)payload;
                }
                else if (payloadName == "MethodSignature")
                {
                    methodSignature = (string)payload;
                }
                else if (payloadName == "MethodFlags")
                {
                    flags = (uint)payload;
                }
                else if (payloadName == "MethodToken")
                {
                    metadataToken = (uint)payload;
                }
            }

            bool isILStub = this.pinvokeInstances.TryGetValue(methodId, out PInvokeInstance pin);
            if (isILStub)
            {
                Debug.Assert(metadataToken == 0);
                metadataToken = pin.MetadataToken;
                namespaceName = pin.FullyQualifiedClassName;
                methodName = pin.MethodName;
            }

            string flagString = "";

            if ((flags & 0x8) != 0)
            {
                flagString += " jitted";
            }
            else
            {
                flagString += " prejitted";
            }

            uint optLevel = ((flags >> 7) & 0x7);

            switch (optLevel)
            {
                case 1: flagString += " minopts"; break;
                case 2: flagString += " fullopts"; break;
                case 3: flagString += " tier0"; break;
                case 4: flagString += " tier1"; break;
                case 5: flagString += " tier1-OSR"; break;
                default: flagString += " unknown codegen"; break;
            }

            generatedCode.WriteLine($"{namespaceName}{Type.Delimiter}{methodName}{(isILStub ? " (ILStub)" : string.Empty)}");
            generatedCode.WriteLine(methodSignature);
            generatedCode.WriteLine($"0x{startAddress:X16} {size:D6}{flagString,-20}");

            unsafe
            {
                var codeReader = new MemoryCodeReader((byte*)startAddress, size);
                var decoder = Decoder.Create(IntPtr.Size * 8, codeReader);
                decoder.IP = startAddress;
                ulong endRip = startAddress + (ulong)size;
                const int HEXBYTES_COLUMN_BYTE_LENGTH = 10;
                var instructions = new InstructionList();

                while (decoder.IP < endRip)
                {
                    // The method allocates an uninitialized element at the end of the list and
                    // returns a reference to it which is initialized by Decode().
                    decoder.Decode(out instructions.AllocUninitializedElement());
                }

                // Formatters: Masm*, Nasm*, Gas* (AT&T) and Intel* (XED)
                var formatter = new NasmFormatter();
                formatter.Options.DigitSeparator = "`";
                formatter.Options.FirstOperandCharIndex = 10;
                var output = new StringOutput();
                // Use InstructionList's ref iterator (C# 7.3) to prevent copying 32 bytes every iteration
                foreach (ref var instr in instructions)
                {
                    // Don't use instr.ToString(), it allocates more, uses masm syntax and default options
                    // Console.WriteLine(instr);
                    formatter.Format(instr, output);
                    int instrLen = instr.Length;
                    int byteBaseIndex = (int)(instr.IP - startAddress);
                    for (int i = 0; i < instrLen; i++)
                    {
                        generatedCode.Write(codeReader[byteBaseIndex + i].ToString("X2"));
                    }

                    int missingBytes = HEXBYTES_COLUMN_BYTE_LENGTH - instrLen;
                    for (int i = 0; i < missingBytes; i++)
                    {
                        generatedCode.Write("  ");
                    }

                    generatedCode.Write(' ');
                    generatedCode.WriteLine(output.ToStringAndReset());
                }
            }

            this.OnNewMethodCodeGen(new MethodCodeGen()
            {
                MetadataToken = metadataToken,
                IsILStub = isILStub,
                FullyQualifiedClassName = namespaceName,
                MethodName = methodName,
                GeneratedCode = generatedCode.ToString(),
                CodeSize = size
            });
        }

        private record PInvokeInstance
        {
            public uint MetadataToken { get; init; }
            public string FullyQualifiedClassName { get; init; }
            public string MethodName { get; init; }
        }

        private unsafe class MemoryCodeReader : CodeReader
        {
            private readonly byte* baseAddress;
            private readonly uint length;
            private uint next = 0;

            public MemoryCodeReader(byte* baseAddress, uint length)
            {
                Debug.Assert(baseAddress is not null);
                this.baseAddress = baseAddress;
                this.length = length;
            }

            public bool CanReadByte() => next < this.length;

            public override int ReadByte()
            {
                if (next >= length)
                {
                    return -1;
                }

                return baseAddress[next++];
            }

            public byte this[int i] => baseAddress[i];
        }

    }
}
