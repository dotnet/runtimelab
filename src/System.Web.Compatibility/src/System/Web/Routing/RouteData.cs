// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace System.Web.Routing {

    [TypeForwardedFrom("System.Web.Routing, Version=3.5.0.0, Culture=Neutral, PublicKeyToken=31bf3856ad364e35")]
    public class RouteData {
        public RouteData() { }

        public RouteData(RouteBase route, IRouteHandler routeHandler) {
            Route = route;
            RouteHandler = routeHandler;
        }

        public RouteValueDictionary DataTokens { get; } = new RouteValueDictionary();

        public RouteBase Route { get; set; }

        public IRouteHandler RouteHandler { get; set; }

        public RouteValueDictionary Values { get; } = new RouteValueDictionary();

        public string GetRequiredString(string valueName)
        {
            if (Values.TryGetValue(valueName, out object value))
            {
                string valueString = value as string;
                if (!string.IsNullOrEmpty(valueString)) 
                {
                    return valueString;
                }
            }

            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentUICulture,
                    SR.RouteData_RequiredValue,
                    valueName));
        }
    }
}
