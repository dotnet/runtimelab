// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Web.Routing;
using Xunit;

namespace System.Web.Compatibility.Tests
{
    public class StopRoutingHandlerTests
    {
        [Fact]
        public void StopRoutingHandlerDefault()
        {
            StopRoutingHandler srh = new StopRoutingHandler();

            IRouteHandler rh = srh as IRouteHandler;

            Assert.NotNull(rh);
            Assert.Throws<NotSupportedException>(() => rh.GetHttpHandler(null));
        }
    }
}
