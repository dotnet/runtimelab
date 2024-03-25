// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import CryptoKit
import Foundation

public func AppleCryptoNative_ChaCha20Poly1305Encrypt(
    keyPtr: UnsafeMutableRawPointer,
    keyLength: Int32,
    noncePtr: UnsafeMutableRawPointer,
    nonceLength: Int32,
    plaintextPtr: UnsafeMutableRawPointer,
    plaintextLength: Int32,
    ciphertextPtr: UnsafeMutablePointer<UInt8>,
    ciphertextPtrLength: Int32,
    tagPtr: UnsafeMutablePointer<UInt8>,
    tagPtrLength: Int32,
    aadPtr: UnsafeMutableRawPointer,
    aadLength: Int32
 ) -> Int32 {
    let nonceData = Data(bytesNoCopy: noncePtr, count: Int(nonceLength), deallocator: Data.Deallocator.none)
    let key = Data(bytesNoCopy: keyPtr, count: Int(keyLength), deallocator: Data.Deallocator.none)
    let plaintext = Data(bytesNoCopy: plaintextPtr, count: Int(plaintextLength), deallocator: Data.Deallocator.none)
    let aad = Data(bytesNoCopy: aadPtr, count: Int(aadLength), deallocator: Data.Deallocator.none)
    let symmetricKey = SymmetricKey(data: key)

    guard let nonce = try? ChaChaPoly.Nonce(data: nonceData) else {
        return 0
    }

    guard let result = try? ChaChaPoly.seal(plaintext, using: symmetricKey, nonce: nonce, authenticating: aad) else {
        return 0
    }

    assert(ciphertextPtrLength >= result.ciphertext.count)
    assert(tagPtrLength >= result.tag.count)

    result.ciphertext.copyBytes(to: ciphertextPtr, count: result.ciphertext.count)
    result.tag.copyBytes(to: tagPtr, count: result.tag.count)
    return 1
 }

public func AppleCryptoNative_ChaCha20Poly1305Decrypt(
    keyPtr: UnsafeMutableRawPointer,
    keyLength: Int32,
    noncePtr: UnsafeMutableRawPointer,
    nonceLength: Int32,
    ciphertextPtr: UnsafeMutableRawPointer,
    ciphertextLength: Int32,
    tagPtr: UnsafeMutableRawPointer,
    tagLength: Int32,
    plaintextPtr: UnsafeMutablePointer<UInt8>,
    plaintextPtrLength: Int32,
    aadPtr: UnsafeMutableRawPointer,
    aadLength: Int32
) -> Int32 {
    let nonceData = Data(bytesNoCopy: noncePtr, count: Int(nonceLength), deallocator: Data.Deallocator.none)
    let key = Data(bytesNoCopy: keyPtr, count: Int(keyLength), deallocator: Data.Deallocator.none)
    let ciphertext = Data(bytesNoCopy: ciphertextPtr, count: Int(ciphertextLength), deallocator: Data.Deallocator.none)
    let aad = Data(bytesNoCopy: aadPtr, count: Int(aadLength), deallocator: Data.Deallocator.none)
    let tag = Data(bytesNoCopy: tagPtr, count: Int(tagLength), deallocator: Data.Deallocator.none)
    let symmetricKey = SymmetricKey(data: key)

    guard let nonce = try? ChaChaPoly.Nonce(data: nonceData) else {
        return 0
    }

    guard let sealedBox = try? ChaChaPoly.SealedBox(nonce: nonce, ciphertext: ciphertext, tag: tag) else {
        return 0
    }

    do {
        let result = try ChaChaPoly.open(sealedBox, using: symmetricKey, authenticating: aad)

        assert(plaintextPtrLength >= result.count)
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
