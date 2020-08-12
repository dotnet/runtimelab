// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace System.Text.Json.SourceGeneration.Tests
{
    public class JsonSerializerSourceGeneratorTests
    {

        private readonly ITestOutputHelper _testOutputHelper;

        public JsonSerializerSourceGeneratorTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [JsonSerializable]
        public class SampleInternalTest
        {
            public char PublicCharField;
            private string PrivateStringField;
            public int PublicIntPropertyPublic { get; set; }
            public int PublicIntPropertyPrivateSet { get; private set; }
            public int PublicIntPropertyPrivateGet { private get; set; }

            public SampleInternalTest()
            {
                PublicCharField = 'a';
                PrivateStringField = "privateStringField";
            }

            public SampleInternalTest(char c, string s)
            {
                PublicCharField = c;
                PrivateStringField = s;
            }

            private SampleInternalTest(int i)
            {
                PublicIntPropertyPublic = i;
            }

            private void UseFields()
            {
                string use = PublicCharField.ToString() + PrivateStringField;
            }
        }

        [JsonSerializable(typeof(JsonConverterAttribute))]
        public class SampleExternalTest { }

        [JsonSerializable]
        public class Location
        {
            public int Id { get; set; }
            public string Address1 { get; set; }
            public string Address2 { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string PostalCode { get; set; }
            public string Name { get; set; }
            public string PhoneNumber { get; set; }
            public string Country { get; set; }
        }

        [JsonSerializable]
        public class ActiveOrUpcomingEvent
        {
            public int Id { get; set; }
            public string ImageUrl { get; set; }
            public string Name { get; set; }
            public string CampaignName { get; set; }
            public string CampaignManagedOrganizerName { get; set; }
            public string Description { get; set; }
            public DateTimeOffset StartDate { get; set; }
            public DateTimeOffset EndDate { get; set; }
        }

        [JsonSerializable]
        public class CampaignSummaryViewModel
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string ImageUrl { get; set; }
            public string OrganizationName { get; set; }
            public string Headline { get; set; }
        }

        [JsonSerializable]
        public class IndexViewModel
        {
            public List<ActiveOrUpcomingEvent> ActiveOrUpcomingEvents { get; set; }
            public CampaignSummaryViewModel FeaturedCampaign { get; set; }
            public bool IsNewAccount { get; set; }
            public bool HasFeaturedCampaign => FeaturedCampaign != null;
        }

        [Fact]
        public void TestGeneratedCode()
        {
            _testOutputHelper.WriteLine("Testing");
            var internalTypeTest = new HelloWorldGenerated.SampleInternalTestClassInfo();
            var locationTypeTest = new HelloWorldGenerated.LocationClassInfo();
            var campaignSummaryViewModelTypeTest = new HelloWorldGenerated.CampaignSummaryViewModelClassInfo();
            var activeOrUpcomingEventTypeTest = new HelloWorldGenerated.ActiveOrUpcomingEventClassInfo();
            var indexViewModelTypeTest = new HelloWorldGenerated.IndexViewModelClassInfo();
            _testOutputHelper.WriteLine(campaignSummaryViewModelTypeTest.s);
            _testOutputHelper.WriteLine(activeOrUpcomingEventTypeTest.s);
            _testOutputHelper.WriteLine(indexViewModelTypeTest.s);

            //// Check base class names.
            //Assert.Equal("SampleInternalTestClassInfo", internalTypeTest.GetClassName());
            //Assert.Equal("SampleExternalTestClassInfo", externalTypeTest.GetClassName());

            //// Public and private Ctors are visible.
            //Assert.Equal(3, internalTypeTest.Ctors.Count);
            //Assert.Equal(2, externalTypeTest.Ctors.Count);

            //// Ctor params along with its types are visible.
            //Dictionary<string, string> expectedCtorParamsInternal = new Dictionary<string, string> { { "c", "Char"}, { "s", "String" }, { "i", "Int32" } };
            //Assert.Equal(expectedCtorParamsInternal, internalTypeTest.CtorParams);

            //Dictionary<string, string> expectedCtorParamsExternal = new Dictionary<string, string> { { "converterType", "Type"} };
            //Assert.Equal(expectedCtorParamsExternal, externalTypeTest.CtorParams);

            //// Public and private methods are visible.
            //List<string> expectedMethodsInternal = new List<string> { "get_PublicIntPropertyPrivateGet", "get_PublicIntPropertyPrivateSet", "get_PublicIntPropertyPublic", "set_PublicIntPropertyPrivateGet", "set_PublicIntPropertyPrivateSet", "set_PublicIntPropertyPublic", "UseFields" };
            //Assert.Equal(expectedMethodsInternal, internalTypeTest.Methods.OrderBy(s => s).ToList());

            //List<string> expectedMethodsExternal = new List<string> { "CreateConverter", "get_ConverterType" };
            //Assert.Equal(expectedMethodsExternal, externalTypeTest.Methods.OrderBy(s => s).ToList());

            //// Public and private fields are visible.
            //Dictionary<string, string> expectedFieldsInternal = new Dictionary<string, string> { { "PublicCharField", "Char" }, { "PrivateStringField", "String" } };
            //Assert.Equal(expectedFieldsInternal, internalTypeTest.Fields);

            //Dictionary<string, string> expectedFieldsExternal = new Dictionary<string, string> { };
            //Assert.Equal(expectedFieldsExternal, externalTypeTest.Fields);

            //// Public properties are visible.
            //Dictionary<string, string> expectedPropertiesInternal = new Dictionary<string, string> { { "PublicIntPropertyPublic", "Int32" }, { "PublicIntPropertyPrivateSet", "Int32" }, { "PublicIntPropertyPrivateGet", "Int32" } };
            //Assert.Equal(expectedPropertiesInternal, internalTypeTest.Properties);

            //Dictionary<string, string> expectedPropertiesExternal = new Dictionary<string, string> { { "ConverterType", "Type"} };
            //Assert.Equal(expectedPropertiesExternal, externalTypeTest.Properties);
        }

        [Fact]
        public static void RoundTrip()
        {
            Location expected = Create();

            string json = JsonSerializer.Serialize(expected, JsonContext.Default.Location);
            Location obj = JsonSerializer.Deserialize(json, JsonContext.Default.Location);

            Verify(expected, obj);
        }
    }
}
