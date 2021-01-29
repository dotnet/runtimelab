﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace System.Text.Json.SourceGeneration.UnitTests
{
    public class JsonSourceGeneratorDiagnosticsTests
    {
        [Fact]
        public void SuccessfulSourceGeneration()
        {
            // Compile the referenced assembly first.
            Compilation campaignCompilation = CompilationHelper.CreateCampaignSummaryViewModelCompilation();
            Compilation eventCompilation = CompilationHelper.CreateActiveOrUpcomingEventCompilation();

            // Emit the image of the referenced assembly.
            byte[] campaignImage = CompilationHelper.CreateAssemblyImage(campaignCompilation);
            byte[] eventImage = CompilationHelper.CreateAssemblyImage(eventCompilation);

            // Main source for current compilation.
            string source = @"
            using System.Collections.Generic;
            using System.Text.Json.Serialization;
            using ReferencedAssembly;

            [assembly: JsonSerializable(typeof(JsonSourceGenerator.IndexViewModel)]

            namespace JsonSourceGenerator
            {
                public class IndexViewModel
                {
                    public List<ActiveOrUpcomingEvent> ActiveOrUpcomingEvents { get; set; }
                    public CampaignSummaryViewModel FeaturedCampaign { get; set; }
                    public bool IsNewAccount { get; set; }
                    public bool HasFeaturedCampaign => FeaturedCampaign != null;
                }
            }";

            MetadataReference[] additionalReferences = {
                MetadataReference.CreateFromImage(campaignImage),
                MetadataReference.CreateFromImage(eventImage),
            };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);

            JsonSourceGenerator generator = new JsonSourceGenerator();

            CompilationHelper.RunGenerators(compilation, out var generatorDiags, generator);

            // Expected info logs.
            string[] expectedInfoDiagnostics = new string[] {
                "Generated serialization metadata for type List<ReferencedAssembly.ActiveOrUpcomingEvent>",
                "Generated serialization metadata for type Int32",
                "Generated serialization metadata for type String",
                "Generated serialization metadata for type DateTimeOffset",
                "Generated serialization metadata for type Boolean",
                "Generated serialization metadata for type ActiveOrUpcomingEvent",
                "Generated serialization metadata for type CampaignSummaryViewModel",
                "Generated serialization metadata for type IndexViewModel",
            };

            CheckDiagnosticMessages(generatorDiags, DiagnosticSeverity.Info, expectedInfoDiagnostics);
            CheckDiagnosticMessages(generatorDiags, DiagnosticSeverity.Warning, new string[] { });
            CheckDiagnosticMessages(generatorDiags, DiagnosticSeverity.Error, new string[] { });
        }

        [Fact]
        public void UnsuccessfulSourceGeneration()
        {
            // Compile the referenced assembly first.
            Compilation campaignCompilation = CompilationHelper.CreateCampaignSummaryViewModelCompilation();
            Compilation eventCompilation = CompilationHelper.CreateActiveOrUpcomingEventCompilation();

            // Emit the image of the referenced assembly.
            byte[] campaignImage = CompilationHelper.CreateAssemblyImage(campaignCompilation);
            byte[] eventImage = CompilationHelper.CreateAssemblyImage(eventCompilation);

            // Main source for current compilation.
            string source = @"
            using System.Text.Json.Serialization;
            using System.Collections.Generic;
            using ReferencedAssembly;

            [assembly: JsonSerializable(typeof(JsonSourceGenerator.IndexViewModel)]

            namespace JsonSourceGenerator
            {
                public class IndexViewModel
                {
                    public ISet<ActiveOrUpcomingEvent> ActiveOrUpcomingEvents { get; set; }
                    public CampaignSummaryViewModel FeaturedCampaign { get; set; }
                    public bool IsNewAccount { get; set; }
                    public bool HasFeaturedCampaign => FeaturedCampaign != null;
                }
            }";

            MetadataReference[] additionalReferences = {
                MetadataReference.CreateFromImage(campaignImage),
                MetadataReference.CreateFromImage(eventImage),
            };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);

            JsonSourceGenerator generator = new JsonSourceGenerator();

            CompilationHelper.RunGenerators(compilation, out var generatorDiags, generator);

            // Expected success info logs.
            string[] expectedInfoDiagnostics = new string[] {
                "Generated serialization metadata for type IndexViewModel",
                "Generated serialization metadata for type Boolean",
                "Generated serialization metadata for type CampaignSummaryViewModel"
            };

            // Expected warning logs.
            string[] expectedWarningDiagnostics = new string[] { "Did not generate serialization metadata for type ISet<ReferencedAssembly.ActiveOrUpcomingEvent>" };

            CheckDiagnosticMessages(generatorDiags, DiagnosticSeverity.Info, expectedInfoDiagnostics);
            CheckDiagnosticMessages(generatorDiags, DiagnosticSeverity.Warning, expectedWarningDiagnostics);
            CheckDiagnosticMessages(generatorDiags, DiagnosticSeverity.Error, new string[] { });
        }

        [Fact]
        public void NameClashSourceGeneration()
        {
            Compilation compilation = CompilationHelper.CreateRepeatedLocationsCompilation();

            JsonSourceGenerator generator = new JsonSourceGenerator();

            CompilationHelper.RunGenerators(compilation, out var generatorDiags, generator);

            // Expected info logs.
            string[] expectedInfoDiagnostics = new string[] {
                "Generated serialization metadata for type Location",
                "Generated serialization metadata for type Int32",
                "Generated serialization metadata for type String",
                "Generated serialization metadata for type HelloWorld.Location",
            };
            // Expected warning logs.
            string[] expectedWarningDiagnostics = new string[] {
                "Duplicate type name detected. Setting the JsonTypeInfo<T> property for type HelloWorld.Location in assembly TestAssembly to HelloWorldLocation. To use please call JsonContext.Instance.HelloWorldLocation" };

            CheckDiagnosticMessages(generatorDiags, DiagnosticSeverity.Info, expectedInfoDiagnostics);
            CheckDiagnosticMessages(generatorDiags, DiagnosticSeverity.Warning, expectedWarningDiagnostics);
            CheckDiagnosticMessages(generatorDiags, DiagnosticSeverity.Error, new string[] { });
        }

        private void CheckDiagnosticMessages(ImmutableArray<Diagnostic> diagnostics, DiagnosticSeverity level, string[] expectedMessages)
        {
            string[] actualMessages = diagnostics.Where(diagnostic => diagnostic.Severity == level).Select(diagnostic => diagnostic.GetMessage()).ToArray();

            // Can't depending on reflection order when generating type metadata.
            Array.Sort(actualMessages);
            Array.Sort(expectedMessages);

            Assert.Equal(expectedMessages, actualMessages);
        }
    }
}
