// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code-gen'd

using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization.Tests;

namespace MyNamespace
{
    public partial class JsonContext : JsonSerializerContext
    {
        private ActiveOrUpcomingEventTypeInfo _activeOrUpcomingEvent;
        public JsonTypeInfo<ActiveOrUpcomingEvent> ActiveOrUpcomingEvent
        {
            get
            {
                if (_activeOrUpcomingEvent == null)
                {
                    _activeOrUpcomingEvent = new ActiveOrUpcomingEventTypeInfo(this);
                }

                return _activeOrUpcomingEvent.TypeInfo;
            }
        }

        private class ActiveOrUpcomingEventTypeInfo
        {
            public JsonTypeInfo<ActiveOrUpcomingEvent> TypeInfo { get; private set; }

            public ActiveOrUpcomingEventTypeInfo(JsonContext context)
            {
                var typeInfo = new JsonObjectInfo<ActiveOrUpcomingEvent>(CreateObjectFunc, context.GetOptions());

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.ActiveOrUpcomingEvent.Id),
                    (obj) => { return ((ActiveOrUpcomingEvent)obj).Id; },
                    (obj, value) => { ((ActiveOrUpcomingEvent)obj).Id = value; },
                    context.Int32);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.ActiveOrUpcomingEvent.ImageUrl),
                    (obj) => { return ((ActiveOrUpcomingEvent)obj).ImageUrl; },
                    (obj, value) => { ((ActiveOrUpcomingEvent)obj).ImageUrl = value; },
                    context.String);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.ActiveOrUpcomingEvent.Name),
                    (obj) => { return ((ActiveOrUpcomingEvent)obj).Name; },
                    (obj, value) => { ((ActiveOrUpcomingEvent)obj).Name = value; },
                    context.String);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.ActiveOrUpcomingEvent.CampaignName),
                    (obj) => { return ((ActiveOrUpcomingEvent)obj).CampaignName; },
                    (obj, value) => { ((ActiveOrUpcomingEvent)obj).CampaignName = value; },
                    context.String);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.ActiveOrUpcomingEvent.CampaignManagedOrganizerName),
                    (obj) => { return ((ActiveOrUpcomingEvent)obj).CampaignManagedOrganizerName; },
                    (obj, value) => { ((ActiveOrUpcomingEvent)obj).CampaignManagedOrganizerName = value; },
                    context.String);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.ActiveOrUpcomingEvent.Description),
                    (obj) => { return ((ActiveOrUpcomingEvent)obj).Description; },
                    (obj, value) => { ((ActiveOrUpcomingEvent)obj).Description = value; },
                    context.String);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.ActiveOrUpcomingEvent.StartDate),
                    (obj) => { return ((ActiveOrUpcomingEvent)obj).StartDate; },
                    (obj, value) => { ((ActiveOrUpcomingEvent)obj).StartDate = value; },
                    context.DateTimeOffset);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.ActiveOrUpcomingEvent.EndDate),
                    (obj) => { return ((ActiveOrUpcomingEvent)obj).EndDate; },
                    (obj, value) => { ((ActiveOrUpcomingEvent)obj).EndDate = value; },
                    context.DateTimeOffset);

                typeInfo.CompleteInitialization(canBeDynamic: false);
                TypeInfo = typeInfo;
            }

            private object CreateObjectFunc()
            {
                return new ActiveOrUpcomingEvent();
            }
        }
    }
}
