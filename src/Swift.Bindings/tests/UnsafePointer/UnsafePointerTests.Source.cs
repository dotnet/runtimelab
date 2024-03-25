// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnsafePointerTestsBindings;
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
            fixed (byte* ciphertextPtr = ciphertext)
            fixed (byte* tagPtr = tag)
            fixed (void* aadPtr = aad)
            {
                const int Success = 1;

                UnsafeMutableRawPointer _keyPtr = new UnsafeMutableRawPointer(keyPtr);
                UnsafeMutableRawPointer _noncePtr = new UnsafeMutableRawPointer(noncePtr);
                UnsafeMutableRawPointer _plaintextPtr = new UnsafeMutableRawPointer(plaintextPtr);
                UnsafeMutablePointer<Byte> _ciphertextPtr = new UnsafeMutablePointer<Byte>(ciphertextPtr);
                UnsafeMutablePointer<Byte> _tagPtr = new UnsafeMutablePointer<Byte>(tagPtr);
                UnsafeMutableRawPointer _aadPtr = new UnsafeMutableRawPointer(aadPtr);

                int result = UnsafePointerTests.AppleCryptoNative_ChaCha20Poly1305Encrypt(
                                    _keyPtr, key.Length,
                                    _noncePtr, nonce.Length,
                                    _plaintextPtr, plaintext.Length,
                                    _ciphertextPtr, ciphertext.Length,
                                    _tagPtr, tag.Length,
                                    _aadPtr, aad.Length);

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
            fixed (void* keyPtr = key)
            fixed (void* noncePtr = nonce)
            fixed (void* ciphertextPtr = ciphertext)
            fixed (void* tagPtr = tag)
            fixed (byte* plaintextPtr = plaintext)
            fixed (void* aadPtr = aad)
            {
                const int Success = 1;
                const int AuthTagMismatch = -1;

                UnsafeMutableRawPointer _keyPtr = new UnsafeMutableRawPointer(keyPtr);
                UnsafeMutableRawPointer _noncePtr = new UnsafeMutableRawPointer(noncePtr);
                UnsafeMutableRawPointer _ciphertextPtr = new UnsafeMutableRawPointer(ciphertextPtr);
                UnsafeMutableRawPointer _tagPtr = new UnsafeMutableRawPointer (tagPtr);
                UnsafeMutablePointer<Byte> _plaintextPtr = new UnsafeMutablePointer<Byte>(plaintextPtr);
                UnsafeMutableRawPointer _aadPtr = new UnsafeMutableRawPointer(aadPtr);

                int result = UnsafePointerTests.AppleCryptoNative_ChaCha20Poly1305Decrypt(
                    _keyPtr, key.Length,
                    _noncePtr, nonce.Length,
                    _ciphertextPtr, ciphertext.Length,
                    _tagPtr, tag.Length,
                    _plaintextPtr, plaintext.Length,
                    _aadPtr, aad.Length);

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
