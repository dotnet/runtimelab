// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import CryptoKit
import Foundation

public func AppleCryptoNative_ChaCha20Poly1305Encrypt(
    keyPtr: UnsafeRawBufferPointer,
    noncePtr: UnsafeRawBufferPointer,
    plaintextPtr: UnsafeRawBufferPointer,
    ciphertextPtr: UnsafeMutableBufferPointer<UInt8>,
    tagPtr: UnsafeMutableBufferPointer<UInt8>,
    aadPtr: UnsafeRawBufferPointer
 ) -> Int32 {
    let nonce = try! ChaChaPoly.Nonce(data: noncePtr)
    let symmetricKey = SymmetricKey(data: keyPtr)

    guard let result = try? ChaChaPoly.seal(plaintextPtr, using: symmetricKey, nonce: nonce, authenticating: aadPtr) else {
        return 0
    }

    assert(ciphertextPtr.count >= result.ciphertext.count)
    assert(tagPtr.count >= result.tag.count)

    result.ciphertext.copyBytes(to: ciphertextPtr, count: result.ciphertext.count)
    result.tag.copyBytes(to: tagPtr, count: result.tag.count)
    return 1
 }

public func AppleCryptoNative_ChaCha20Poly1305Decrypt(
    keyPtr: UnsafeRawBufferPointer,
    noncePtr: UnsafeRawBufferPointer,
    ciphertextPtr: UnsafeRawBufferPointer,
    tagPtr: UnsafeRawBufferPointer,
    plaintextPtr: UnsafeMutableBufferPointer<UInt8>,
    aadPtr: UnsafeRawBufferPointer
) -> Int32 {
    let nonce = try! ChaChaPoly.Nonce(data: noncePtr)
    let symmetricKey = SymmetricKey(data: keyPtr)

    guard let sealedBox = try? ChaChaPoly.SealedBox(nonce: nonce, ciphertext: ciphertextPtr, tag: tagPtr) else {
        return 0
    }

    do {
        let result = try ChaChaPoly.open(sealedBox, using: symmetricKey, authenticating: aadPtr)

        assert(plaintextPtr.count >= result.count)
        result.copyBytes(to: plaintextPtr, count: result.count)
        return 1
    }
    catch CryptoKitError.authenticationFailure {
        return -1
    }
    catch {
        return 0
    }
}
