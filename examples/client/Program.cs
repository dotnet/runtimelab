using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3DraftSupport", isEnabled: true);

            using HttpClient client = new HttpClient();
            using HttpRequestMessage message = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://localhost:5557/"),
                Version = new Version(3, 0),
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            using HttpResponseMessage response = await client.SendAsync(message).ConfigureAwait(false);

            Console.WriteLine($"Response received using HTTP/{response.Version.Major}.{response.Version.Minor}");
        }
    }
}
