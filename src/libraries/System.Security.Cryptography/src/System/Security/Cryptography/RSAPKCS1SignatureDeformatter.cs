// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Internal.Cryptography;

namespace System.Security.Cryptography
{
    [UnsupportedOSPlatform("browser")]
    public class RSAPKCS1SignatureDeformatter : AsymmetricSignatureDeformatter
    {
        private RSA? _rsaKey;
        private string? _algName;

        public RSAPKCS1SignatureDeformatter() { }
        public RSAPKCS1SignatureDeformatter(AsymmetricAlgorithm key)
        {
            ArgumentNullException.ThrowIfNull(key);

            _rsaKey = (RSA)key;
        }

        public override void SetKey(AsymmetricAlgorithm key)
        {
            ArgumentNullException.ThrowIfNull(key);

            _rsaKey = (RSA)key;
        }

        public override void SetHashAlgorithm(string strName)
        {
            // Verify the name
            if (CryptoConfig.MapNameToOID(strName) != null)
            {
                // Uppercase known names as required for BCrypt
                _algName = HashAlgorithmNames.ToUpper(strName);
            }
            else
            {
                // For .NET Framework compat, exception is deferred until VerifySignature
                _algName = null;
            }
        }

        public override bool VerifySignature(byte[] rgbHash, byte[] rgbSignature)
        {
            ArgumentNullException.ThrowIfNull(rgbHash);
            ArgumentNullException.ThrowIfNull(rgbSignature);

            if (_algName == null)
                throw new CryptographicUnexpectedOperationException(SR.Cryptography_FormatterMissingAlgorithm);
            if (_rsaKey == null)
                throw new CryptographicUnexpectedOperationException(SR.Cryptography_FormatterMissingKey);

            return _rsaKey.VerifyHash(rgbHash, rgbSignature, new HashAlgorithmName(_algName), RSASignaturePadding.Pkcs1);
        }
    }
}
