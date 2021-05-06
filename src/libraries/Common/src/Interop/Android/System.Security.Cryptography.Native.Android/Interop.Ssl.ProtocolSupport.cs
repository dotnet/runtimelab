// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Security.Authentication;

internal static partial class Interop
{
    internal static partial class AndroidCrypto
    {
        // NativeAOT TODO - this entrypoint doesn't exist outside Android, but we're trying to statically bind to it because it's in CryptoNative
        //[DllImport(Interop.Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLGetSupportedProtocols")]
        internal static SslProtocols SSLGetSupportedProtocols() => throw new System.NotSupportedException();

        // NativeAOT TODO - this entrypoint doesn't exist outside Android, but we're trying to statically bind to it because it's in CryptoNative
        //[DllImport(Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLSupportsApplicationProtocolsConfiguration")]
        //[return:MarshalAs(UnmanagedType.U1)]
        internal static bool SSLSupportsApplicationProtocolsConfiguration() => throw new System.NotSupportedException();
    }
}
