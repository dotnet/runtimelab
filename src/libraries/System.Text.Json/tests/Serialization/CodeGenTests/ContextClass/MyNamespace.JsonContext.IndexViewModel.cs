// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code-gen'd

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization.Tests;

namespace MyNamespace
{
    public partial class JsonContext : JsonSerializerContext
    {
        private IndexViewModelTypeInfo _indexViewModel;
        public JsonTypeInfo<IndexViewModel> IndexViewModel
        {
            get
            {
                if (_indexViewModel == null)
                {
                    _indexViewModel = new IndexViewModelTypeInfo(this);
                }

                return _indexViewModel.TypeInfo;
            }
        }

        private class IndexViewModelTypeInfo
        {
            public JsonTypeInfo<IndexViewModel> TypeInfo { get; private set; }

            public IndexViewModelTypeInfo(JsonContext context)
            {
                var typeInfo = new JsonObjectInfo<IndexViewModel>(CreateObjectFunc, context.GetOptions());

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.IndexViewModel.ActiveOrUpcomingEvents),
                    (obj) => { return ((IndexViewModel)obj).ActiveOrUpcomingEvents; },
                    (obj, value) => { ((IndexViewModel)obj).ActiveOrUpcomingEvents = value; },
                    KnownCollectionTypeInfos<ActiveOrUpcomingEvent>.GetList(context.ActiveOrUpcomingEvent, context));

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.IndexViewModel.FeaturedCampaign),
                    (obj) => { return ((IndexViewModel)obj).FeaturedCampaign; },
                    (obj, value) => { ((IndexViewModel)obj).FeaturedCampaign = value; },
                    context.CampaignSummaryViewModel);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.IndexViewModel.IsNewAccount),
                    (obj) => { return ((IndexViewModel)obj).IsNewAccount; },
                    (obj, value) => { ((IndexViewModel)obj).IsNewAccount = value; },
                    context.Boolean);

                typeInfo.CompleteInitialization(canBeDynamic: false);
                TypeInfo = typeInfo;
            }

            private object CreateObjectFunc()
            {
                return new IndexViewModel();
            }
        }
    }
}
