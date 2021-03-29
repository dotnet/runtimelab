using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Xunit;

namespace System.Net.Http.LowLevel.Tests
{
    public sealed class TestHeadersSink : Dictionary<string, List<string>>, IHttpHeadersSink
    {
        private IEnumerable<(string headerName, string headerValue, int index)> Flattened => this
            .SelectMany(kvp => kvp.Value.Select((value, index) => (value, index)), (kvp, value) => (headerName: kvp.Key, headerValue: value.value, headerIndex: value.index));

        public TestHeadersSink() : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public void OnHeader(object? state, ReadOnlySpan<byte> headerName, ReadOnlySpan<byte> headerValue)
        {
            string nameAscii = Encoding.ASCII.GetString(headerName);
            string valueAscii = Encoding.ASCII.GetString(headerValue);
            Add(nameAscii, valueAscii);
        }

        public string GetSingleValue(string headerName)
        {
            bool hasValues = TryGetSingleValue(headerName, out string? value);
            Assert.True(hasValues);
            return value!;
        }

        public bool TryGetSingleValue(string headerName, [NotNullWhen(true)] out string? headerValue)
        {
            bool hasValues = TryGetValue(headerName, out List<string>? values);
            if (hasValues)
            {
                headerValue = Assert.Single(values!);
                return true;
            }
            else
            {
                headerValue = null;
                return false;
            }
        }

        public void Add(string headerName, string headerValue)
        {
            if (!TryGetValue(headerName, out List<string>? values))
            {
                values = new List<string>();
                Add(headerName, values);
            }

            values.Add(headerValue);
        }

        public bool Contains(TestHeadersSink headers) =>
            headers.Flattened.Except(Flattened).Any() == false;
    }
}
