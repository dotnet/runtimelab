// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnsafeBufferPointerTestsBindings;
using System.Diagnostics;
using System.Security.Cryptography;
using Swift.Runtime;

namespace Test
{
    public class MainClass
    {
        public static int Main(string[] args)
        {
            byte[] key = RandomNumberGenerator.GetBytes(32); // Generate a 256-bit key
            byte[] nonce = RandomNumberGenerator.GetBytes(12); // Generate a 96-bit nonce
            byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
            byte[] aad = System.Text.Encoding.UTF8.GetBytes("Additional Authenticated Data");

            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[16]; // ChaCha20Poly1305 tag size
            Console.WriteLine($"Plaintext: {BitConverter.ToString(plaintext)}");

            ChaCha20Poly1305Encrypt(
                key,
                nonce,
                plaintext,
                ciphertext,
                tag,
                aad);

            Console.WriteLine($"Ciphertext: {BitConverter.ToString(ciphertext)}");
            Console.WriteLine($"Tag: {BitConverter.ToString(tag)}");

            Array.Clear(plaintext, 0, plaintext.Length);

            ChaCha20Poly1305Decrypt(
                key,
                nonce,
                ciphertext,
                tag,
                plaintext,
                aad
            );

            string decryptedMessage = System.Text.Encoding.UTF8.GetString(plaintext);
            Console.WriteLine($"Decrypted: {decryptedMessage}");
            return "Hello, World!" == decryptedMessage ? 1 : 0;
        }

        internal static unsafe void ChaCha20Poly1305Encrypt(
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
            fixed (void* ciphertextPtr = ciphertext)
            fixed (void* tagPtr = tag)
            fixed (void* aadPtr = aad)
            {
                const int Success = 1;

                UnsafeRawBufferPointer keyBuffer = new UnsafeRawBufferPointer(keyPtr, key.Length);
                UnsafeRawBufferPointer nonceBuffer = new UnsafeRawBufferPointer(noncePtr, nonce.Length);
                UnsafeRawBufferPointer plaintextBuffer = new UnsafeRawBufferPointer(plaintextPtr, plaintext.Length);
                UnsafeMutableBufferPointer ciphertextBuffer = new UnsafeMutableBufferPointer(ciphertextPtr, ciphertext.Length);
                UnsafeMutableBufferPointer tagBuffer = new UnsafeMutableBufferPointer(tagPtr, tag.Length);
                UnsafeRawBufferPointer aadBuffer = new UnsafeRawBufferPointer(aadPtr, aad.Length);

                int result = UnsafeBufferPointerTests.AppleCryptoNative_ChaCha20Poly1305Encrypt(
                                    keyBuffer,
                                    nonceBuffer,
                                    plaintextBuffer,
                                    ciphertextBuffer,
                                    tagBuffer,
                                    aadBuffer);

                if (result != Success)
                {
                    Debug.Assert(result == 0);
                    Console.WriteLine("Encryption failed");
                }
            }
        }

        internal static unsafe void ChaCha20Poly1305Decrypt(
            ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> ciphertext,
            ReadOnlySpan<byte> tag,
            Span<byte> plaintext,
            ReadOnlySpan<byte> aad)
        {
            fixed (byte* keyPtr = key)
            fixed (byte* noncePtr = nonce)
            fixed (byte* ciphertextPtr = ciphertext)
            fixed (byte* tagPtr = tag)
            fixed (byte* plaintextPtr = plaintext)
            fixed (byte* aadPtr = aad)
            {
                const int Success = 1;
                const int AuthTagMismatch = -1;

                UnsafeRawBufferPointer keyBuffer = new UnsafeRawBufferPointer(keyPtr, key.Length);
                UnsafeRawBufferPointer nonceBuffer = new UnsafeRawBufferPointer(noncePtr, nonce.Length);
                UnsafeRawBufferPointer ciphertextBuffer = new UnsafeRawBufferPointer(ciphertextPtr, ciphertext.Length);
                UnsafeRawBufferPointer tagBuffer = new UnsafeRawBufferPointer (tagPtr, tag.Length);
                UnsafeMutableBufferPointer plaintextBuffer = new UnsafeMutableBufferPointer(plaintextPtr, plaintext.Length);
                UnsafeRawBufferPointer aadBuffer = new UnsafeRawBufferPointer(aadPtr, aad.Length);

                int result = UnsafeBufferPointerTests.AppleCryptoNative_ChaCha20Poly1305Decrypt(
                    keyBuffer,
                    nonceBuffer,
                    ciphertextBuffer,
                    tagBuffer,
                    plaintextBuffer,
                    aadBuffer);

                if (result != Success)
                {
                    CryptographicOperations.ZeroMemory(plaintext);

                    if (result == AuthTagMismatch)
                    {
                        throw new AuthenticationTagMismatchException();
                    }
                    else
                    {
                        Debug.Assert(result == 0);
                        throw new CryptographicException();
                    }
                }
            }
        }
    }
}
