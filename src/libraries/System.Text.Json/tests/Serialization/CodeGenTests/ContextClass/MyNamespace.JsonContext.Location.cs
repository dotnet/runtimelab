// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code-gen'd

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MyNamespace
{
    public partial class JsonContext : JsonSerializerContext
    {
        private LocationTypeInfo _location;
        public JsonTypeInfo<Location> Location
        {
            get
            {
                if (_location == null)
                {
                    _location = new LocationTypeInfo(this);
                }

                return _location.TypeInfo;
            }
        }

        private class LocationTypeInfo
        {
            public JsonTypeInfo<Location> TypeInfo { get; private set; }

            public LocationTypeInfo(JsonContext context)
            {
                var typeInfo = new JsonObjectInfo<Location>(CreateObjectFunc, context.GetOptions());

                typeInfo.AddProperty(nameof(MyNamespace.Location.Id),
                    (obj) => { return ((Location)obj).Id; },
                    (obj, value) => { ((Location)obj).Id = value; },
                    context.Int32);

                typeInfo.AddProperty(nameof(MyNamespace.Location.Address1),
                    (obj) => { return ((Location)obj).Address1; },
                    (obj, value) => { ((Location)obj).Address1 = value; },
                    context.String);

                typeInfo.AddProperty(nameof(MyNamespace.Location.Address2),
                    (obj) => { return ((Location)obj).Address2; },
                    (obj, value) => { ((Location)obj).Address2 = value; },
                    context.String);

                typeInfo.AddProperty(nameof(MyNamespace.Location.City),
                    (obj) => { return ((Location)obj).City; },
                    (obj, value) => { ((Location)obj).City = value; },
                    context.String);

                typeInfo.AddProperty(nameof(MyNamespace.Location.State),
                    (obj) => { return ((Location)obj).State; },
                    (obj, value) => { ((Location)obj).State = value; },
                    context.String);

                typeInfo.AddProperty(nameof(MyNamespace.Location.PostalCode),
                    (obj) => { return ((Location)obj).PostalCode; },
                    (obj, value) => { ((Location)obj).PostalCode = value; },
                    context.String);

                typeInfo.AddProperty(nameof(MyNamespace.Location.Name),
                    (obj) => { return ((Location)obj).Name; },
                    (obj, value) => { ((Location)obj).Name = value; },
                    context.String);

                typeInfo.AddProperty(nameof(MyNamespace.Location.PhoneNumber),
                    (obj) => { return ((Location)obj).PhoneNumber; },
                    (obj, value) => { ((Location)obj).PhoneNumber = value; },
                    context.String);

                typeInfo.AddProperty(nameof(MyNamespace.Location.Country),
                    (obj) => { return ((Location)obj).Country; },
                    (obj, value) => { ((Location)obj).Country = value; },
                    context.String);

                typeInfo.CompleteInitialization(canBeDynamic: true);
                TypeInfo = typeInfo;
            }

            private object CreateObjectFunc()
            {
                return new Location();
            }
        }
    }
}
