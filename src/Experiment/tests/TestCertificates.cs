using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace System.Net.Http.LowLevel.Tests
{
    internal static class TestCertificates
    {
        static readonly X509Certificate2 s_SelfSignedServerCert = CreateSelfSigned13ServerCertificate();

        public static X509Certificate2 GetSelfSigned13ServerCertificate() =>
            new X509Certificate2(s_SelfSignedServerCert);

        private static X509Certificate2 CreateSelfSigned13ServerCertificate()
        {
            using RSA rsa = RSA.Create();

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("localhost");

            var certReq = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            certReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
            certReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            certReq.CertificateExtensions.Add(sanBuilder.Build());

            X509Certificate2 innerCert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow.AddMonths(1));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using (innerCert)
                {
                    return new X509Certificate2(innerCert.Export(X509ContentType.Pfx));
                }
            }
            else
            {
                return innerCert;
            }
        }
    }
}
