
namespace System.Net.Http.LowLevel.Tests.Servers
{
    internal record HttpTestRequest(
        string Method,
        string PathAndQuery,
        Version Version,
        TestHeadersSink Headers);

    internal record HttpTestFullRequest(
        string Method,
        string PathAndQuery,
        Version Version,
        TestHeadersSink Headers,
        string Content,
        TestHeadersSink TrailingHeaders);
}
