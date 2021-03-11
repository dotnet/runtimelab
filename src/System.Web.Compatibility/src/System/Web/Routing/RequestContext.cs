// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace System.Web.Routing 
{
    [TypeForwardedFrom("System.Web.Routing, Version=3.5.0.0, Culture=Neutral, PublicKeyToken=31bf3856ad364e35")]
    public class RequestContext 
    {
        public RequestContext() { }

        public RequestContext(HttpContextBase httpContext, RouteData routeData) 
        {
            HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
            RouteData = routeData ?? throw new ArgumentNullException(nameof(routeData));
        }

        public virtual HttpContextBase HttpContext { get; set; }

        public virtual RouteData RouteData { get; set; }
    }
}
