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
        private CampaignSummaryViewModelTypeInfo _typeInfo;
        public JsonTypeInfo<CampaignSummaryViewModel> CampaignSummaryViewModel
        {
            get
            {
                if (_typeInfo == null)
                {
                    _typeInfo = new CampaignSummaryViewModelTypeInfo(this);
                }

                return _typeInfo.TypeInfo;
            }
        }

        private class CampaignSummaryViewModelTypeInfo
        {
            public JsonTypeInfo<CampaignSummaryViewModel> TypeInfo { get; private set; }

            public CampaignSummaryViewModelTypeInfo(JsonContext context)
            {
                var typeInfo = new JsonObjectInfo<CampaignSummaryViewModel>(CreateObjectFunc, context.GetOptions());

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.CampaignSummaryViewModel.Id),
                    (obj) => { return ((CampaignSummaryViewModel)obj).Id; },
                    (obj, value) => { ((CampaignSummaryViewModel)obj).Id = value; },
                    context.Int32);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.CampaignSummaryViewModel.Title),
                    (obj) => { return ((CampaignSummaryViewModel)obj).Title; },
                    (obj, value) => { ((CampaignSummaryViewModel)obj).Title = value; },
                    context.String);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.CampaignSummaryViewModel.Description),
                    (obj) => { return ((CampaignSummaryViewModel)obj).Description; },
                    (obj, value) => { ((CampaignSummaryViewModel)obj).Description = value; },
                    context.String);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.CampaignSummaryViewModel.ImageUrl),
                    (obj) => { return ((CampaignSummaryViewModel)obj).ImageUrl; },
                    (obj, value) => { ((CampaignSummaryViewModel)obj).ImageUrl = value; },
                    context.String);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.CampaignSummaryViewModel.OrganizationName),
                    (obj) => { return ((CampaignSummaryViewModel)obj).OrganizationName; },
                    (obj, value) => { ((CampaignSummaryViewModel)obj).OrganizationName = value; },
                    context.String);

                typeInfo.AddProperty(nameof(System.Text.Json.Serialization.Tests.CampaignSummaryViewModel.Headline),
                    (obj) => { return ((CampaignSummaryViewModel)obj).Headline; },
                    (obj, value) => { ((CampaignSummaryViewModel)obj).Headline = value; },
                    context.String);

                typeInfo.CompleteInitialization(canBeDynamic: false);
                TypeInfo = typeInfo;
            }

            private object CreateObjectFunc()
            {
                return new CampaignSummaryViewModel();
            }
        }
    }
}
