using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    var cert = CertificateLoader.LoadFromStoreCert("localhost", StoreName.My.ToString(), StoreLocation.CurrentUser, false);

                    webBuilder
                        .UseKestrel()
                        .UseQuic(options =>
                        {
                            options.Certificate = cert;
                            options.Alpn = "h3-29";
                        })
                        .ConfigureKestrel((context, options) =>
                        {
                            var basePort = 5557;
                            options.EnableAltSvc = true;

                            options.Listen(IPAddress.Any, basePort, listenOptions =>
                            {
                                listenOptions.UseHttps(httpsOptions =>
                                {
                                    httpsOptions.ServerCertificate = cert;
                                });
                                listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                            });
                        })
                        .UseStartup<Startup>();
                });
    }
}
