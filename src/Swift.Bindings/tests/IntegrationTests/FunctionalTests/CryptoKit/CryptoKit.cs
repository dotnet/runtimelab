// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Swift;
using System.Security.Cryptography;
using Swift;
using Swift.Runtime;

namespace BindingsGeneration.FunctionalTests
{
    /// <summary>
    /// Represents ChaChaPoly in C#.
    /// </summary>
    public unsafe struct ChaChaPoly
    {
        /// <summary>
        /// Represents Nonce in C#.
        /// </summary>
        public unsafe class Nonce : ISwiftObject
        {
            static nuint _payloadSize = SwiftObjectHelper<Nonce>.GetTypeMetadata().Size;

            SwiftSafeHandle<Nonce> _payload = SwiftSafeHandle<Nonce>.Zero;

            public SwiftSafeHandle<Nonce> Payload => _payload;

            static TypeMetadata ISwiftObject.GetTypeMetadata() => PInvoke_getMetadata();

            [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
            [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit03ChaC4PolyO5NonceVMa")]
            internal static extern TypeMetadata PInvoke_getMetadata();

            static ISwiftObject ISwiftObject.NewFromPayload(IntPtr handle)
            {
                return new Nonce(handle);
            }

            unsafe Nonce(SwiftHandle handle)
            {
                _payload = new SwiftSafeHandle<Nonce>(handle);
            }

            int ISwiftObject.MarshalToSwift(ref Span<byte> swiftDestSpan)
            {
                var metadata = SwiftObjectHelper<Nonce>.GetTypeMetadata();
                if ((int)metadata.Size > swiftDestSpan.Length)
                {
                    throw new ArgumentException($"Span size does not match type size, Expected: {(int)metadata.Size}, Actual: {swiftDestSpan.Length}");
                }
                unsafe
                {
                    fixed (byte* swiftDest = swiftDestSpan)
                    {
                        metadata.ValueWitnessTable->InitializeWithCopy(swiftDest, (void*)_payload.DangerousGetHandle(), metadata);
                        return (int)metadata.Size;
                    }
                }
            }

            private static Dictionary<Type, string> _protocolConformanceSymbols;
            static Nonce()
            {
                _protocolConformanceSymbols = new Dictionary<Type, string>
                {

                };
            }

            static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>()
                where TProtocol : class
            {
                if (!_protocolConformanceSymbols.TryGetValue(typeof(TProtocol), out var symbolName))
                {
                    throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type Nonce and protocol {typeof(TProtocol).Name}, but no conformance was found.");
                }
                return ProtocolConformanceDescriptor.LoadFromSymbol("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", symbolName);
            }

            public Nonce(Data data)
            {
                _payload = new SwiftSafeHandle<Nonce>((IntPtr)NativeMemory.Alloc(_payloadSize));
                SwiftIndirectResult swiftIndirectResult = new SwiftIndirectResult((void*)_payload.DangerousGetHandle());

                TypeMetadata metadata = SwiftObjectHelper<Data>.GetTypeMetadata();
                ProtocolWitnessTable witnessTable = ProtocolWitnessTable.GetOrThrow<Data, ISwiftDataProtocol>();

                PInvoke_init(swiftIndirectResult, &data, metadata, witnessTable, out SwiftError error);

                if (error.Value != null)
                {
                    throw new CryptographicException();
                }
            }

            [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
            [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit03ChaC4PolyO5NonceV4dataAEx_tKc10Foundation12DataProtocolRzlufC")]
            public static unsafe extern void PInvoke_init(SwiftIndirectResult result, void* data, TypeMetadata metadata, ProtocolWitnessTable witnessTable, out SwiftError error);
        }

        /// <summary>
        /// Represents SealedBox in C#.
        /// </summary>
        public unsafe struct SealedBox
        {
#pragma warning disable 0169
            private Data _combined;
#pragma warning restore 0169

            public SealedBox(ChaChaPoly.Nonce nonce, Data ciphertext, Data tag)
            {
                TypeMetadata ciphertextMetadata = SwiftObjectHelper<Data>.GetTypeMetadata();
                TypeMetadata tagMetadata = SwiftObjectHelper<Data>.GetTypeMetadata();
                ProtocolWitnessTable ciphertextWitnessTable = ProtocolWitnessTable.GetOrThrow<Data, ISwiftDataProtocol>();
                ProtocolWitnessTable tagWitnessTable = ProtocolWitnessTable.GetOrThrow<Data, ISwiftDataProtocol>();

                this = PInvoke_init(
                    nonce.Payload,
                    &ciphertext,
                    &tag,
                    ciphertextMetadata,
                    tagMetadata,
                    ciphertextWitnessTable,
                    tagWitnessTable,
                    out SwiftError error);

                if (error.Value != null)
                {
                    throw new CryptographicException();
                }
            }

            [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
            [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit03ChaC4PolyO9SealedBoxV5nonce10ciphertext3tagAeC5NonceV_xq_tKc10Foundation12DataProtocolRzAkLR_r0_lufC")]
            public static unsafe extern ChaChaPoly.SealedBox PInvoke_init(SafeHandle nonce, void* ciphertext, void* tag, TypeMetadata ciphertextMetadata, TypeMetadata tagMetadata, ProtocolWitnessTable ciphertextWitnessTable, ProtocolWitnessTable tagWitnessTable, out SwiftError error);


            public Data Ciphertext => PInvoke_GetCiphertext(this);

            [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
            [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit03ChaC4PolyO9SealedBoxV10ciphertext10Foundation4DataVvg")]
            public static unsafe extern Data PInvoke_GetCiphertext(ChaChaPoly.SealedBox sealedBox);

            public Data Tag => PInvoke_GetTag(this);

            [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
            [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit03ChaC4PolyO9SealedBoxV3tag10Foundation4DataVvg")]
            public static unsafe extern Data PInvoke_GetTag(ChaChaPoly.SealedBox sealedBox);
        }

        /// <summary>
        /// Encrypts the plaintext using the key, nonce, and authenticated data.
        /// </summary>
        public static unsafe SealedBox seal<Plaintext, AuthenticateData>(Plaintext plaintext, SymmetricKey key, Nonce nonce, AuthenticateData aad, out SwiftError error) where Plaintext : unmanaged, ISwiftObject where AuthenticateData : unmanaged, ISwiftObject
        {
            TypeMetadata plaintextMetadata = SwiftObjectHelper<Plaintext>.GetTypeMetadata();
            TypeMetadata aadMetadata = SwiftObjectHelper<AuthenticateData>.GetTypeMetadata();
            ProtocolWitnessTable plaintextWitnessTable = ProtocolWitnessTable.GetOrThrow<Plaintext, ISwiftDataProtocol>();
            ProtocolWitnessTable aadWitnessTable = ProtocolWitnessTable.GetOrThrow<AuthenticateData, ISwiftDataProtocol>();

            SealedBox sealedBox = PInvoke_Seal(
                &plaintext,
                key.Payload,
                nonce.Payload,
                &aad,
                plaintextMetadata,
                aadMetadata,
                plaintextWitnessTable,
                aadWitnessTable,
                out error);

            return sealedBox;
        }

        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit03ChaC4PolyO4seal_5using5nonce14authenticatingAC9SealedBoxVx_AA12SymmetricKeyVAC5NonceVSgq_tK10Foundation12DataProtocolRzAoPR_r0_lFZ")]
        public static unsafe extern ChaChaPoly.SealedBox PInvoke_Seal(void* plaintext, SafeHandle key, SafeHandle nonce, void* aad, TypeMetadata plaintextMetadata, TypeMetadata aadMetadata, ProtocolWitnessTable plaintextWitnessTable, ProtocolWitnessTable aadWitnessTable, out SwiftError error);


        /// <summary>
        /// Decrypts the sealed box using the key and authenticated data.
        /// </summary>
        public static unsafe Data open<AuthenticateData>(SealedBox sealedBox, SymmetricKey key, AuthenticateData aad, out SwiftError error) where AuthenticateData : unmanaged, ISwiftObject
        {
            TypeMetadata metadata = SwiftObjectHelper<AuthenticateData>.GetTypeMetadata();
            ProtocolWitnessTable witnessTable = ProtocolWitnessTable.GetOrThrow<AuthenticateData, ISwiftDataProtocol>();

            Data data = PInvoke_Open(
                sealedBox,
                key.Payload,
                &aad,
                metadata,
                witnessTable,
                out error);

            return data;
        }


        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit03ChaC4PolyO4open_5using14authenticating10Foundation4DataVAC9SealedBoxV_AA12SymmetricKeyVxtKAG0I8ProtocolRzlFZ")]
        public static unsafe extern Data PInvoke_Open(ChaChaPoly.SealedBox sealedBox, SafeHandle key, void* aad, TypeMetadata metadata, ProtocolWitnessTable witnessTable, out SwiftError error);

    }

    /// <summary>
    /// Represents AesGcm in C#.
    /// </summary>
    public unsafe struct AesGcm
    {
        /// <summary>
        /// Represents Nonce in C#.
        /// </summary>
        public unsafe class Nonce : ISwiftObject
        {
            static nuint _payloadSize = SwiftObjectHelper<Nonce>.GetTypeMetadata().Size;

            SwiftSafeHandle<Nonce> _payload = SwiftSafeHandle<Nonce>.Zero;

            public SwiftSafeHandle<Nonce> Payload => _payload;

            static TypeMetadata ISwiftObject.GetTypeMetadata() => PInvoke_getMetadata();

            [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
            [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit3AESO3GCMO5NonceVMa")]
            internal static extern TypeMetadata PInvoke_getMetadata();

            static ISwiftObject ISwiftObject.NewFromPayload(IntPtr handle)
            {
                return new Nonce(handle);
            }

            unsafe Nonce(SwiftHandle handle)
            {
                _payload = new SwiftSafeHandle<Nonce>(handle);
            }

            int ISwiftObject.MarshalToSwift(ref Span<byte> swiftDestSpan)
            {
                var metadata = SwiftObjectHelper<Nonce>.GetTypeMetadata();
                if ((int)metadata.Size > swiftDestSpan.Length)
                {
                    throw new ArgumentException($"Span size does not match type size, Expected: {(int)metadata.Size}, Actual: {swiftDestSpan.Length}");
                }
                unsafe
                {
                    fixed (byte* swiftDest = swiftDestSpan)
                    {
                        metadata.ValueWitnessTable->InitializeWithCopy(swiftDest, (void*)_payload.DangerousGetHandle(), metadata);
                        return (int)metadata.Size;
                    }
                }
            }

            private static Dictionary<Type, string> _protocolConformanceSymbols;
            static Nonce()
            {
                _protocolConformanceSymbols = new Dictionary<Type, string>
                {

                };
            }

            static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>()
                where TProtocol : class
            {
                if (!_protocolConformanceSymbols.TryGetValue(typeof(TProtocol), out var symbolName))
                {
                    throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type Nonce and protocol {typeof(TProtocol).Name}, but no conformance was found.");
                }
                return ProtocolConformanceDescriptor.LoadFromSymbol("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", symbolName);
            }

            public Nonce(Data data)
            {
                _payload = new SwiftSafeHandle<Nonce>((IntPtr)NativeMemory.Alloc(_payloadSize));
                SwiftIndirectResult swiftIndirectResult = new SwiftIndirectResult((void*)_payload.DangerousGetHandle());

                TypeMetadata metadata = SwiftObjectHelper<Data>.GetTypeMetadata();
                ProtocolWitnessTable witnessTable = ProtocolWitnessTable.GetOrThrow<Data, ISwiftDataProtocol>();

                PInvoke_init(swiftIndirectResult, &data, metadata, witnessTable, out SwiftError error);

                if (error.Value != null)
                {
                    throw new CryptographicException();
                }
            }
        }

        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit3AESO3GCMO5NonceV4dataAGx_tKc10Foundation12DataProtocolRzlufC")]
        public static unsafe extern void PInvoke_init(SwiftIndirectResult result, void* data, TypeMetadata metadata, ProtocolWitnessTable witnessTable, out SwiftError error);

        /// <summary>
        /// Represents SealedBox in C#.
        /// </summary>
        public unsafe class SealedBox : ISwiftObject
        {
            static nuint _payloadSize = SwiftObjectHelper<SealedBox>.GetTypeMetadata().Size;

            SwiftSafeHandle<SealedBox> _payload = SwiftSafeHandle<SealedBox>.Zero;

            public SwiftSafeHandle<SealedBox> Payload => _payload;

            static TypeMetadata ISwiftObject.GetTypeMetadata() => PInvoke_getMetadata();

            [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
            [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit3AESO3GCMO9SealedBoxVMa")]
            internal static extern TypeMetadata PInvoke_getMetadata();

            static ISwiftObject ISwiftObject.NewFromPayload(IntPtr handle)
            {
                return new SealedBox(handle);
            }

            unsafe SealedBox(SwiftHandle handle)
            {
                _payload = new SwiftSafeHandle<SealedBox>(handle);
            }

            int ISwiftObject.MarshalToSwift(ref Span<byte> swiftDestSpan)
            {
                var metadata = SwiftObjectHelper<SealedBox>.GetTypeMetadata();
                if ((int)metadata.Size > swiftDestSpan.Length)
                {
                    throw new ArgumentException($"Span size does not match type size, Expected: {(int)metadata.Size}, Actual: {swiftDestSpan.Length}");
                }
                unsafe
                {
                    fixed (byte* swiftDest = swiftDestSpan)
                    {
                        metadata.ValueWitnessTable->InitializeWithCopy(swiftDest, (void*)_payload.DangerousGetHandle(), metadata);
                        return (int)metadata.Size;
                    }
                }
            }

            private static Dictionary<Type, string> _protocolConformanceSymbols;
            static SealedBox()
            {
                _protocolConformanceSymbols = new Dictionary<Type, string>
                {

                };
            }

            static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>()
                where TProtocol : class
            {
                if (!_protocolConformanceSymbols.TryGetValue(typeof(TProtocol), out var symbolName))
                {
                    throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type SealedBox and protocol {typeof(TProtocol).Name}, but no conformance was found.");
                }
                return ProtocolConformanceDescriptor.LoadFromSymbol("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", symbolName);
            }

            public SealedBox()
            {
                _payload = new SwiftSafeHandle<SealedBox>((IntPtr)NativeMemory.Alloc(_payloadSize));
            }

            public SealedBox(AesGcm.Nonce nonce, Data ciphertext, Data tag)
            {
                _payload = new SwiftSafeHandle<SealedBox>((IntPtr)NativeMemory.Alloc(_payloadSize));
                SwiftIndirectResult swiftIndirectResult = new SwiftIndirectResult((void*)_payload.DangerousGetHandle());

                TypeMetadata ciphertextMetadata = SwiftObjectHelper<Data>.GetTypeMetadata();
                TypeMetadata tagMetadata = SwiftObjectHelper<Data>.GetTypeMetadata();
                ProtocolWitnessTable ciphertextWitnessTable = ProtocolWitnessTable.GetOrThrow<Data, ISwiftDataProtocol>();
                ProtocolWitnessTable tagWitnessTable = ProtocolWitnessTable.GetOrThrow<Data, ISwiftDataProtocol>();

                PInvoke_init(
                    swiftIndirectResult,
                    nonce.Payload,
                    &ciphertext,
                    &tag,
                    ciphertextMetadata,
                    tagMetadata,
                    ciphertextWitnessTable,
                    tagWitnessTable,
                    out SwiftError error);

                if (error.Value != null)
                {
                    throw new CryptographicException();
                }
            }

            [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
            [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit3AESO3GCMO9SealedBoxV5nonce10ciphertext3tagAgE5NonceV_xq_tKc10Foundation12DataProtocolRzAmNR_r0_lufC")]
            public static unsafe extern void PInvoke_init(SwiftIndirectResult result, SafeHandle nonce, void* ciphertext, void* tag, TypeMetadata ciphertextMetadata, TypeMetadata tagMetadata, ProtocolWitnessTable ciphertextWitnessTable, ProtocolWitnessTable tagWitnessTable, out SwiftError error);


            public Data Ciphertext => PInvoke_GetCiphertext(new SwiftSelf((void*)_payload.DangerousGetHandle()));

            [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
            [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit3AESO3GCMO9SealedBoxV10ciphertext10Foundation4DataVvg")]
            public static unsafe extern Data PInvoke_GetCiphertext(SwiftSelf sealedBox);

            public Data Tag => PInvoke_GetTag(new SwiftSelf((void*)_payload.DangerousGetHandle()));

            [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
            [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit3AESO3GCMO9SealedBoxV3tag10Foundation4DataVvg")]
            public static unsafe extern Data PInvoke_GetTag(SwiftSelf sealedBox);
        }

        /// <summary>
        /// Encrypts the plaintext using the key, nonce, and authenticated data.
        /// </summary>
        public static unsafe SealedBox seal<Plaintext, AuthenticateData>(Plaintext plaintext, SymmetricKey key, Nonce nonce, AuthenticateData aad, out SwiftError error) where Plaintext : unmanaged, ISwiftObject where AuthenticateData : unmanaged, ISwiftObject
        {
            AesGcm.SealedBox sealedBox = new AesGcm.SealedBox();
            SwiftIndirectResult swiftIndirectResult = new SwiftIndirectResult((void*)sealedBox.Payload.DangerousGetHandle());


            TypeMetadata plaintextMetadata = SwiftObjectHelper<Plaintext>.GetTypeMetadata();
            TypeMetadata aadMetadata = SwiftObjectHelper<AuthenticateData>.GetTypeMetadata();
            ProtocolWitnessTable plaintextWitnessTable = ProtocolWitnessTable.GetOrThrow<Plaintext, ISwiftDataProtocol>();
            ProtocolWitnessTable aadWitnessTable = ProtocolWitnessTable.GetOrThrow<AuthenticateData, ISwiftDataProtocol>();

            PInvoke_Seal(
                swiftIndirectResult,
                &plaintext,
                key.Payload,
                nonce.Payload,
                &aad,
                plaintextMetadata,
                aadMetadata,
                plaintextWitnessTable,
                aadWitnessTable,
                out error);

            return sealedBox;
        }

        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit3AESO3GCMO4seal_5using5nonce14authenticatingAE9SealedBoxVx_AA12SymmetricKeyVAE5NonceVSgq_tK10Foundation12DataProtocolRzAqRR_r0_lFZ")]
        public static unsafe extern void PInvoke_Seal(SwiftIndirectResult result, void* plaintext, SafeHandle key, SafeHandle nonce, void* aad, TypeMetadata plaintextMetadata, TypeMetadata aadMetadata, ProtocolWitnessTable plaintextWitnessTable, ProtocolWitnessTable aadWitnessTable, out SwiftError error);

        /// <summary>
        /// Decrypts the sealed box using the key and authenticated data.
        /// </summary>
        public static unsafe Data open<AuthenticateData>(SealedBox sealedBox, SymmetricKey key, AuthenticateData aad, out SwiftError error) where AuthenticateData : unmanaged, ISwiftObject
        {
            TypeMetadata metadata = SwiftObjectHelper<AuthenticateData>.GetTypeMetadata();
            ProtocolWitnessTable witnessTable = ProtocolWitnessTable.GetOrThrow<AuthenticateData, ISwiftDataProtocol>();

            Data data = PInvoke_Open(
                sealedBox.Payload,
                key.Payload,
                &aad,
                metadata,
                witnessTable,
                out error);

            return data;
        }

        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit3AESO3GCMO4open_5using14authenticating10Foundation4DataVAE9SealedBoxV_AA12SymmetricKeyVxtKAI0I8ProtocolRzlFZ")]
        public static unsafe extern Data PInvoke_Open(SafeHandle sealedBox, SafeHandle key, void* aad, TypeMetadata metadata, ProtocolWitnessTable witnessTable, out SwiftError error);
    }

    /// <summary>
    /// Represents SymmetricKey in C#.
    /// </summary>
    public unsafe class SymmetricKey : ISwiftObject
    {
        static nuint _payloadSize = SwiftObjectHelper<SymmetricKey>.GetTypeMetadata().Size;

        SwiftSafeHandle<SymmetricKey> _payload = SwiftSafeHandle<SymmetricKey>.Zero;

        public SwiftSafeHandle<SymmetricKey> Payload => _payload;

        static TypeMetadata ISwiftObject.GetTypeMetadata() => PInvoke_getMetadata();

        [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit12SymmetricKeyVMa")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        internal static extern TypeMetadata PInvoke_getMetadata();

        static ISwiftObject ISwiftObject.NewFromPayload(IntPtr handle)
        {
            return new SymmetricKey(handle);
        }

        unsafe SymmetricKey(SwiftHandle handle)
        {
            _payload = new SwiftSafeHandle<SymmetricKey>(handle);
        }

        int ISwiftObject.MarshalToSwift(ref Span<byte> swiftDestSpan)
        {
            var metadata = SwiftObjectHelper<SymmetricKey>.GetTypeMetadata();
            if ((int)metadata.Size > swiftDestSpan.Length)
            {
                throw new ArgumentException($"Span size does not match type size, Expected: {(int)metadata.Size}, Actual: {swiftDestSpan.Length}");
            }
            unsafe
            {
                fixed (byte* swiftDest = swiftDestSpan)
                {
                    metadata.ValueWitnessTable->InitializeWithCopy(swiftDest, (void*)_payload.DangerousGetHandle(), metadata);
                    return (int)metadata.Size;
                }
            }
        }

        private static Dictionary<Type, string> _protocolConformanceSymbols;
        static SymmetricKey()
        {
            _protocolConformanceSymbols = new Dictionary<Type, string>
            {

            };
        }

        static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>()
            where TProtocol : class
        {
            if (!_protocolConformanceSymbols.TryGetValue(typeof(TProtocol), out var symbolName))
            {
                throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type SymmetricKey and protocol {typeof(TProtocol).Name}, but no conformance was found.");
            }
            return ProtocolConformanceDescriptor.LoadFromSymbol("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", symbolName);
        }

        public SymmetricKey(Data data)
        {
            _payload = new SwiftSafeHandle<SymmetricKey>((IntPtr)NativeMemory.Alloc(_payloadSize));
            SwiftIndirectResult swiftIndirectResult = new SwiftIndirectResult((void*)_payload.DangerousGetHandle());

            TypeMetadata metadata = SwiftObjectHelper<Data>.GetTypeMetadata();
            ProtocolWitnessTable witnessTable = ProtocolWitnessTable.GetOrThrow<Data, ISwiftContiguousBytes>();

            PInvoke_init(swiftIndirectResult, &data, metadata, witnessTable);
        }

        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit12SymmetricKeyV4dataACx_tc10Foundation15ContiguousBytesRzlufC")]
        public static unsafe extern void PInvoke_init(SwiftIndirectResult result, void* data, TypeMetadata metadata, ProtocolWitnessTable witnessTable);
    }

    /// <summary>
    /// Represents SymmetricKeySize in C#.
    /// </summary>
    public unsafe struct SymmetricKeySize
    {
        private readonly nint _bitCount;

        public SymmetricKeySize(nint bitCount)
        {
            SymmetricKeySize instance;
            PInvoke_init(new SwiftIndirectResult(&instance), bitCount);
            this = instance;
        }

        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        [DllImport("/System/Library/Frameworks/CryptoKit.framework/CryptoKit", EntryPoint = "$s9CryptoKit16SymmetricKeySizeV8bitCountACSi_tcfC")]
        public static unsafe extern void PInvoke_init(SwiftIndirectResult result, nint bitCount);
    }
}
