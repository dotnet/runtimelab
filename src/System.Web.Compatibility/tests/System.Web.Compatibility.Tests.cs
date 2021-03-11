// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Web.Hosting;
using Xunit;

namespace System.Web.Compatibility.Tests
{
    public class SystemWebCompatTests
    {
        [Fact]
        public void HttpContextCurrentDoesNotThrow()
        {
            Assert.Null(HttpContext.Current);
        }

        [Fact]
        public void HttpContextItemsGetDoesNotThrow()
        {
            var context = new HttpContext(new HttpRequest(null, null, null), new HttpResponse(null));

            Assert.Null(context.Items);
        }

        [Fact]
        public void HttpContextUserGetDoesNotThrow()
        {
            var context = new HttpContext(new HttpRequest(null, null, null), new HttpResponse(null));

            Assert.Null(context.User);
        }

        [Fact]
        public void HostingEnvironmentApplicationPathGetDoesNotThrow()
        {
            Assert.Null(HostingEnvironment.ApplicationPath);
        }

        [Fact]
        public void HostingEnvironmentApplicationVirtualPathGetDoesNotThrow()
        {
            Assert.Null(HostingEnvironment.ApplicationVirtualPath);
        }

        [Fact]
        public void HostingEnvironmentIsHostedDoesNotThrow()
        {
            Assert.False(HostingEnvironment.IsHosted);
        }

        [Fact]
        public void HostingEnvironmentSiteNameDoesNotThrow()
        {
            Assert.Null(HostingEnvironment.SiteName);
        }

        [Fact]
        public void HostingEnvironmentMapPathDoesNotThrow()
        {
            Assert.Null(HostingEnvironment.MapPath(null));
        }

        [Fact]
        public void HttpUtilityStillWorks()
        {
            Type httpUtilityType = Type.GetType("System.Web.HttpUtility, System.Web, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            Assert.NotNull(httpUtilityType);
            Assert.Equal(typeof(HttpUtility), httpUtilityType);

            Assert.Equal("hello+world", HttpUtility.UrlEncode("hello world"));
        }

    }
}
