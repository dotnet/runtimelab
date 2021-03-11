// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Web.Routing;
using Xunit;

namespace System.Web.Compatibility.Tests
{
    public class RouteDataTests
    {
        [Fact]
        public void RouteDataDefaultCtor()
        {
            RouteData rd = new RouteData();
            Assert.Null(rd.Route);
            Assert.Null(rd.RouteHandler);

            Assert.NotNull(rd.Values);
            Assert.NotNull(rd.DataTokens);
        }

        class NoopHandler : IRouteHandler
        {
            public IHttpHandler GetHttpHandler(RequestContext requestContext)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void RouteDataRouteHandlerCtor()
        {
            RouteData rd = new RouteData(null, null);
            Assert.Null(rd.Route);
            Assert.Null(rd.RouteHandler);

            Assert.NotNull(rd.Values);
            Assert.NotNull(rd.DataTokens);

            // can't construct a RouteBase as it will throw PNSE
            RouteBase rb = null;
            IRouteHandler rh = new NoopHandler();
            rd = new RouteData(rb, rh);
            Assert.Same(rh, rd.RouteHandler);

            Assert.NotNull(rd.Values);
            Assert.NotNull(rd.DataTokens);
        }

        [Fact]
        public void RouteDataValuesString()
        {
            RouteData rd = new RouteData();
            string key = nameof(key);
            string value = nameof(value);
            rd.Values.Add(key, value);

            string actual = rd.GetRequiredString(key);
            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(typeof(RouteData))]
        [InlineData(null)]
        [InlineData(42)]
        [InlineData(new [] { 1 })]
        public void RouteDataValuesThrow(object value)
        {
            RouteData rd = new RouteData();
            string key = nameof(key);
            rd.Values.Add(key, value);

            Assert.Throws<InvalidOperationException>(() => rd.GetRequiredString(key));
        }

        [Fact]
        public void RouteDataValuesNoExist()
        {
            RouteData rd = new RouteData();
            string key = nameof(key);

            Assert.Throws<InvalidOperationException>(() => rd.GetRequiredString(key));
        }
    }
}
