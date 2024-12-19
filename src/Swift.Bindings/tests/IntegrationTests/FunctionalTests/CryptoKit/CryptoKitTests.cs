// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Swift;
using System.Security.Cryptography;
using System.Diagnostics;
using AesGcm = BindingsGeneration.FunctionalTests.AesGcm;


namespace BindingsGeneration.FunctionalTests
{
    public class CryptoKitTests : IClassFixture<CryptoKitTests.TestFixture>
    {
        private readonly TestFixture _fixture;

        public CryptoKitTests(TestFixture fixture)
        {
            _fixture = fixture;
        }

        public class TestFixture
        {
            static TestFixture()
            {
                InitializeResources();
            }

            private static void InitializeResources()
            {
                // Initialize
            }
        }

        private static unsafe void AesGcmEncrypt(
            ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> plaintext,
            Span<byte> ciphertext,
            Span<byte> tag,
            ReadOnlySpan<byte> aad)
        {
            fixed (void* keyPtr = key)
            fixed (void* noncePtr = nonce)
            fixed (void* plaintextPtr = plaintext)
            fixed (byte* ciphertextPtr = ciphertext)
            fixed (byte* tagPtr = tag)
            fixed (void* aadPtr = aad)
            {
                Data symmetricKeyData = new Data(keyPtr, key.Length);
                SymmetricKey symmetricKey = new SymmetricKey(symmetricKeyData);

                Data nonceData = new Data(noncePtr, nonce.Length);
                AesGcm.Nonce aesGcmNonce = new AesGcm.Nonce(nonceData);

                Data plaintextData = new Data(plaintextPtr, plaintext.Length);
                Data aadData = new Data(aadPtr, aad.Length);

                AesGcm.SealedBox sealedBox = AesGcm.seal(
                    plaintextData,
                    symmetricKey,
                    aesGcmNonce,
                    aadData,
                    out SwiftError error);

                if (error.Value != null)
                {
                    sealedBox.Dispose();
                    aesGcmNonce.Dispose();
                    symmetricKey.Dispose();

                    throw new CryptographicException();
                }

                Data resultCiphertext = sealedBox.Ciphertext;
                Data resultTag = sealedBox.Tag;

                resultCiphertext.CopyBytes(ciphertextPtr, resultCiphertext.Count);
                resultTag.CopyBytes(tagPtr, resultTag.Count);
            }
        }

        private static unsafe void AesGcmDecrypt(
            ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> ciphertext,
            ReadOnlySpan<byte> tag,
            Span<byte> plaintext,
            ReadOnlySpan<byte> aad)
        {
            fixed (void* keyPtr = key)
            fixed (void* noncePtr = nonce)
            fixed (byte* ciphertextPtr = ciphertext)
            fixed (byte* tagPtr = tag)
            fixed (byte* plaintextPtr = plaintext)
            fixed (void* aadPtr = aad)
            {
                Data symmetricKeyData = new Data(keyPtr, key.Length);
                SymmetricKey symmetricKey = new SymmetricKey(symmetricKeyData);

                Data nonceData = new Data(noncePtr, nonce.Length);
                AesGcm.Nonce aesGcmNonce = new AesGcm.Nonce(nonceData);

                Data ciphertextData = new Data(ciphertextPtr, ciphertext.Length);
                Data tagData = new Data(tagPtr, tag.Length);
                Data aadData = new Data(aadPtr, aad.Length);

                AesGcm.SealedBox sealedBox = new AesGcm.SealedBox(aesGcmNonce, ciphertextData, tagData);

                Data data = AesGcm.open(
                    sealedBox,
                    symmetricKey,
                    aadData,
                    out SwiftError error);

                if (error.Value != null)
                {
                    sealedBox.Dispose();
                    aesGcmNonce.Dispose();
                    symmetricKey.Dispose();

                    throw new CryptographicException();
                }

                data.CopyBytes(plaintextPtr, data.Count);
            }
        }

        private static unsafe void ChaCha20Poly1305Encrypt(
            ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> plaintext,
            Span<byte> ciphertext,
            Span<byte> tag,
            ReadOnlySpan<byte> aad)
        {
            fixed (void* keyPtr = key)
            fixed (void* noncePtr = nonce)
            fixed (byte* ciphertextPtr = ciphertext)
            fixed (byte* tagPtr = tag)
            fixed (byte* plaintextPtr = plaintext)
            fixed (void* aadPtr = aad)
            {
                Data symmetricKeyData = new Data(keyPtr, key.Length);
                SymmetricKey symmetricKey = new SymmetricKey(symmetricKeyData);

                Data nonceData = new Data(noncePtr, nonce.Length);
                ChaChaPoly.Nonce chaChaPolyNonce = new ChaChaPoly.Nonce(nonceData);

                Data plaintextData = new Data(plaintextPtr, plaintext.Length);
                Data aadData = new Data(aadPtr, aad.Length);

                ChaChaPoly.SealedBox sealedBox = ChaChaPoly.seal(
                    plaintextData,
                    symmetricKey,
                    chaChaPolyNonce,
                    aadData,
                    out SwiftError error);

                if (error.Value != null)
                {
                    chaChaPolyNonce.Dispose();
                    symmetricKey.Dispose();

                    throw new CryptographicException();
                }

                Data resultCiphertext = sealedBox.Ciphertext;
                Data resultTag = sealedBox.Tag;

                resultCiphertext.CopyBytes(ciphertextPtr, resultCiphertext.Count);
                resultTag.CopyBytes(tagPtr, resultTag.Count);
            }
        }

        private static unsafe void ChaCha20Poly1305Decrypt(
            ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> ciphertext,
            ReadOnlySpan<byte> tag,
            Span<byte> plaintext,
            ReadOnlySpan<byte> aad)
        {
            fixed (void* keyPtr = key)
            fixed (void* noncePtr = nonce)
            fixed (byte* ciphertextPtr = ciphertext)
            fixed (byte* tagPtr = tag)
            fixed (byte* plaintextPtr = plaintext)
            fixed (void* aadPtr = aad)
            {
                Data symmetricKeyData = new Data(keyPtr, key.Length);
                SymmetricKey symmetricKey = new SymmetricKey(symmetricKeyData);

                Data nonceData = new Data(noncePtr, nonce.Length);
                ChaChaPoly.Nonce chaChaPolyNonce = new ChaChaPoly.Nonce(nonceData);

                Data ciphertextData = new Data(ciphertextPtr, ciphertext.Length);
                Data tagData = new Data(tagPtr, tag.Length);
                Data aadData = new Data(aadPtr, aad.Length);

                ChaChaPoly.SealedBox sealedBox = new ChaChaPoly.SealedBox(chaChaPolyNonce, ciphertextData, tagData);

                Data data = ChaChaPoly.open(
                    sealedBox,
                    symmetricKey,
                    aadData,
                    out SwiftError error);

                if (error.Value != null)
                {
                    chaChaPolyNonce.Dispose();
                    symmetricKey.Dispose();

                    CryptographicOperations.ZeroMemory(plaintext);
                    throw new CryptographicException();
                }

                data.CopyBytes(plaintextPtr, data.Count);
            }
        }

        private const int KeySizeInBytes = 256 / 8;
        private const int NonceSizeInBytes = 96 / 8;
        private const int TagSizeInBytes = 128 / 8;

        [Fact]
        public static void TestUnsafePointerCryptoKit()
        {
            const int dataLength = 35;
            byte[] plaintext = Enumerable.Range(1, dataLength).Select(x => (byte)x).ToArray();
            byte[] ciphertext = new byte[dataLength];
            byte[] key = RandomNumberGenerator.GetBytes(KeySizeInBytes);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeInBytes);
            byte[] tag = new byte[TagSizeInBytes];
            byte[] decrypted = new byte[dataLength];

            ChaCha20Poly1305Encrypt(key, nonce, plaintext, ciphertext, tag, default);
            ChaCha20Poly1305Decrypt(key, nonce, ciphertext, tag, decrypted, default);

            Assert.True(plaintext.SequenceEqual(decrypted));

            decrypted = new byte[dataLength];
            AesGcmEncrypt(key, nonce, plaintext, ciphertext, tag, default);

            AesGcmDecrypt(key, nonce, ciphertext, tag, decrypted, default);

            Assert.True(plaintext.SequenceEqual(decrypted));
        }
    }
}
