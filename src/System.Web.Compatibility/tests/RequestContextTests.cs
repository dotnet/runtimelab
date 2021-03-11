// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Web.Routing;
using Xunit;

namespace System.Web.Compatibility.Tests
{
    public class RequestContextTests
    {
        [Fact]
        public void RequestContextDefaultCtor()
        {
            RequestContext rc = new RequestContext();
            Assert.Null(rc.HttpContext);
            Assert.Null(rc.RouteData);
        }

        [Fact]
        public void RequestContextCtor()
        {
            Assert.Throws<ArgumentNullException>("httpContext", () => new RequestContext(null, null));
            Assert.Throws<ArgumentNullException>("httpContext", () => new RequestContext(null, new RouteData()));

            // can't construct a HttpContextBase as it will throw PNSE
        }
    }
}
