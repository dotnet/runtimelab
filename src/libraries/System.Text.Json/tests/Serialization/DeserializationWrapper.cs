// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using Xunit;
#if GENERATE_JSON_METADATA
using System.Text.Json.Tests.JsonSourceGeneration;
#endif

namespace System.Text.Json.Serialization.Tests
{
    /// <summary>
    /// Base class for wrapping serialization calls which allows tests to run under different configurations.
    /// </summary>
    public abstract class DeserializationWrapper
    {
#if GENERATE_JSON_METADATA
        private static JsonSerializerOptions _sourceGenOptions = new();
#endif

        private static readonly JsonSerializerOptions _optionsWithSmallBuffer = new JsonSerializerOptions { DefaultBufferSize = 1 };

        public static DeserializationWrapper StringDeserializer => new StringDeserializerWrapper();
        public static DeserializationWrapper StreamDeserializer => new StreamDeserializerWrapper();
        public static DeserializationWrapper CharSpanDeserializer => new CharSpanDeserializerWrapper();
        public static DeserializationWrapper StringMetadataDeserialzer => new StringMetadataDeserializerWrapper();

        protected internal abstract Task<T> DeserializeWrapper<T>(string json, JsonSerializerOptions options = null);

        protected internal abstract Task<T> DeserializeWrapper<T>(byte[] utf8Json, JsonSerializerOptions options = null);

        protected internal abstract Task<object> DeserializeWrapper(string json, Type type, JsonSerializerOptions options = null);

        private class StringDeserializerWrapper : DeserializationWrapper
        {
            protected internal override Task<T> DeserializeWrapper<T>(string json, JsonSerializerOptions options = null)
            {
                return Task.FromResult(JsonSerializer.Deserialize<T>(json, options));
            }

            protected internal override Task<T> DeserializeWrapper<T>(byte[] utf8Json, JsonSerializerOptions options = null)
            {
                return Task.FromResult(JsonSerializer.Deserialize<T>(utf8Json, options));
            }

            protected internal override Task<object> DeserializeWrapper(string json, Type type, JsonSerializerOptions options = null)
            {
                return Task.FromResult(JsonSerializer.Deserialize(json, type, options));
            }
        }

        private class StreamDeserializerWrapper : DeserializationWrapper
        {
            protected internal override async Task<T> DeserializeWrapper<T>(string json, JsonSerializerOptions options = null)
            {
                if (options == null)
                {
                    options = _optionsWithSmallBuffer;
                }

                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return await JsonSerializer.DeserializeAsync<T>(stream, options);
                }
            }

            protected internal override Task<T> DeserializeWrapper<T>(byte[] utf8Json, JsonSerializerOptions options = null) =>
                throw new NotImplementedException();

            protected internal override async Task<object> DeserializeWrapper(string json, Type type, JsonSerializerOptions options = null)
            {
                if (options == null)
                {
                    options = _optionsWithSmallBuffer;
                }

                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return await JsonSerializer.DeserializeAsync(stream, type, options);
                }
            }
        }

        private class CharSpanDeserializerWrapper : DeserializationWrapper
        {
            protected internal override Task<T> DeserializeWrapper<T>(string json, JsonSerializerOptions options = null)
            {
                return Task.FromResult(JsonSerializer.Deserialize<T>(json.AsSpan(), options));
            }

            protected internal override Task<T> DeserializeWrapper<T>(byte[] utf8Json, JsonSerializerOptions options = null) =>
                throw new NotImplementedException();

            protected internal override Task<object> DeserializeWrapper(string json, Type type, JsonSerializerOptions options = null)
            {
                return Task.FromResult(JsonSerializer.Deserialize(json.AsSpan(), type, options));
            }
        }

        private class StringMetadataDeserializerWrapper : DeserializationWrapper
        {
#if GENERATE_JSON_METADATA
            protected internal override Task<T> DeserializeWrapper<T>(string json, JsonSerializerOptions options = null) =>
                Task.FromResult(JsonSerializer.Deserialize<T>(json, new JsonContext(options ?? _sourceGenOptions)));

            // TODO: update this to use new ROS<byte> + metadata based overload.
            protected internal override Task<T> DeserializeWrapper<T>(byte[] utf8Json, JsonSerializerOptions options = null) =>
                Task.FromResult(JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(utf8Json), new JsonContext(options ?? _sourceGenOptions)));

            protected internal override Task<object> DeserializeWrapper(string json, Type type, JsonSerializerOptions options = null) =>
                Task.FromResult(JsonSerializer.Deserialize(json, type, new JsonContext(options ?? _sourceGenOptions)));
#else
            protected internal override Task<T> DeserializeWrapper<T>(string json, JsonSerializerOptions options = null)
            {
                Assert.True(false, "This overload is not supported without JSON metadata generation.");
                throw new NotSupportedException();
            }

            protected internal override Task<T> DeserializeWrapper<T>(byte[] utf8Json, JsonSerializerOptions options = null)
            {
                Assert.True(false, "This overload is not supported without JSON metadata generation.");
                throw new NotSupportedException();
            }

            protected internal override Task<object> DeserializeWrapper(string json, Type type, JsonSerializerOptions options = null)
            {
                Assert.True(false, "This overload is not supported without JSON metadata generation.");
                throw new NotSupportedException();
            }
#endif
        }

        private class StreamMetadataDeserializerWrapper : DeserializationWrapper
        {
#if GENERATE_JSON_METADATA
            protected internal override async Task<T> DeserializeWrapper<T>(string json, JsonSerializerOptions options = null)
            {
                if (options == null)
                {
                    options = _optionsWithSmallBuffer;
                }

                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return await JsonSerializer.DeserializeAsync<T>(stream, new JsonContext(options ?? _sourceGenOptions));
                }
            }

            protected internal override Task<T> DeserializeWrapper<T>(byte[] utf8Json, JsonSerializerOptions options = null) =>
                throw new NotImplementedException();

            protected internal override async Task<object> DeserializeWrapper(string json, Type type, JsonSerializerOptions options = null)
            {
                if (options == null)
                {
                    options = _optionsWithSmallBuffer;
                }

                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return await JsonSerializer.DeserializeAsync(stream, type, new JsonContext(options ?? _sourceGenOptions));
                }
            }
#else
            protected internal override Task<T> DeserializeWrapper<T>(string json, JsonSerializerOptions options = null)
            {
                Assert.True(false, "This overload is not supported without JSON metadata generation.");
                throw new NotSupportedException();
            }

            protected internal override Task<T> DeserializeWrapper<T>(byte[] utf8Json, JsonSerializerOptions options = null)
            {
                Assert.True(false, "This overload is not supported without JSON metadata generation.");
                throw new NotSupportedException();
            }

            protected internal override Task<object> DeserializeWrapper(string json, Type type, JsonSerializerOptions options = null)
            {
                Assert.True(false, "This overload is not supported without JSON metadata generation.");
                throw new NotSupportedException();
            }
#endif
        }
    }
}
