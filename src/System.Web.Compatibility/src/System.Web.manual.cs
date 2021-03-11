// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Principal;

[assembly: TypeForwardedTo(typeof(System.Web.HttpUtility))]

namespace System.Web
{
    public partial class HttpContext
    {
        public static HttpContext Current
        {
            get => null;
            set => throw new PlatformNotSupportedException(SR.PlatformNotSupportedSystemWeb);
        }

        public IDictionary Items
        {
            get => null;
        }

        public IPrincipal User
        {
            get => null;
            set => throw new PlatformNotSupportedException(SR.PlatformNotSupportedSystemWeb);
        }

        public HttpContext(HttpRequest request, HttpResponse response) { }
    }

    public sealed partial class HttpRequest
    {
        public HttpRequest(string filename, string url, string queryString) { }
    }

    public sealed partial class HttpResponse
    {
        public HttpResponse(TextWriter writer) { }
    }
}

namespace System.Web.Hosting
{
    public partial class HostingEnvironment
    {
        public static string ApplicationPath => null;
        public static string ApplicationVirtualPath => null;
        public static bool IsHosted => false;
        public static string SiteName => null;

        public static string MapPath(string virtualPath) => null;
    }
}

namespace System.Web.UI
{
    // There appears to be a bug in GenAPI where it emits internal abstract members but doesn't emit the internal implementations in derived types
    public sealed partial class PageParser
    {
        internal override string UnknownOutputCacheAttributeError => throw new PlatformNotSupportedException(SR.PlatformNotSupportedSystemWeb);
    }
    public partial class StaticPartialCachingControl
    {
        internal override Control CreateCachedControl() => throw new PlatformNotSupportedException(SR.PlatformNotSupportedSystemWeb);
    }
    public partial class PartialCachingControl
    {
        internal override Control CreateCachedControl() => throw new PlatformNotSupportedException(SR.PlatformNotSupportedSystemWeb);
    }
}
