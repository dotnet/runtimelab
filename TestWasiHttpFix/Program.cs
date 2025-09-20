using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Testing WASI HttpClient fix...");

        // Test the scenario from the issue
        string url = "https://management.azure.com/metadata/endpoints?api-version=2020-01-01";

        using var client = new HttpClient();
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd("application/json");

        try
        {
            Console.WriteLine($"GET {url}");
            var resp = await client.SendAsync(req);
            Console.WriteLine($"Status: {(int)resp.StatusCode} {resp.StatusCode}");
            Console.WriteLine("SUCCESS: No FormatException thrown!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex}");
            return 1;
        }
    }
}
