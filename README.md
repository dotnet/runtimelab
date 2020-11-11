# MsQuic for .NET 5

This branch contains sources for the System.Net.Experimental.MsQuic package which lights up HTTP/3 support in .NET 5.

## Usage

> **Note**: HTTP/3 is experimental in .NET 5 and is not supported in production environments.

There are a few prerequisites that need to be done before starting to use HTTP/3.

1. Download the latest 5.0 build of .NET from <https://dotnet.microsoft.com/download/dotnet/5.0>.
2. Latest [Windows Insider Builds](https://insider.windows.com/en-us/), Insiders Fast build. This is required for Schannel support for QUIC.
    To confirm you have a new enough build, run winver on command line and confirm you version is greater than Version 2004 (OS Build 20145.1000).
    Support for Linux will come in .NET 6.
3. Enabling TLS 1.3. Add the following registry keys to enable TLS 1.3.

    ```
    reg add "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.3\Server" /v DisabledByDefault /t REG_DWORD /d 0 /f
    reg add "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.3\Server" /v Enabled /t REG_DWORD /d 1 /f
    reg add "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.3\Client" /v DisabledByDefault /t REG_DWORD /d 0 /f
    reg add "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.3\Client" /v Enabled /t REG_DWORD /d 1 /f
    ```
4. Add the package System.Net.Experimental.MsQuic to your project.
    ```
    dotnet nuget add source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json --name dotnet-experimental
    dotnet add package System.Net.Experimental.MsQuic --version 5.0.0-alpha*
    ```

### Usage for HttpClient

> See an [example app for HttpClient](examples/client/).

1. Set the app context switch to enable draft HTTP/3.
    ```c#
    AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3DraftSupport", isEnabled: true);
    ```
2. Set version of HTTP/3 for your request.
    ```c#
    new HttpRequestMessage
    {
        Version = new Version(3, 0),
        VersionPolicy = HttpVersionPolicy.RequestVersionExact
    }
    ```

### Usage for ASP.NET Core

> See an [example app for ASP.NET Core](examples/server/).

1. Add the package Microsoft.AspNetCore.Server.Kestrel.Transport.Experimental.Quic to your project.
    ```
    dotnet nuget add source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5/nuget/v3/index.json --name dotnet5
    dotnet add package Microsoft.AspNetCore.Server.Kestrel.Transport.Experimental.Quic --version 5.0.0
    ```
2. You will probably need to regenerate the ASP.NET Core Dev Cert as it needs to be able to support TLS 1.3. You can do this by running:
    ```
    dotnet dev-certs https --clean
    dotnet dev-certs https
    dotnet dev-certs https --trust
    ```
3. Use Kestrel and configure it for QUIC and HTTP/3.
    ```c#
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

                        options.Listen(IPAddress.IPv6Loopback, basePort, listenOptions =>
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
    ```
4. Send a request with Edge or Chrome canary:
    1. Download Edge Dev or Canary <https://www.microsoftedgeinsider.com/en-us/download.>
    2. Either launch edge on the command line with
        ```text
        "C:\Users\<user>\AppData\Local\Microsoft\Edge SxS\Application\msedge.exe" --enable-quic --quic-version=h3-29 --origin-to-force-quic-on=localhost:5557
        ```
       or adding the flags to the Microsoft Edge Dev Properties Target.
3. Hit localhost:5557 from the browser, check the network tab, the request should be HTTP/3 with the spec h3-29.

## Building

- clone the repo recursively or run `git submodule update --init --recursive` to get all the submodules.
- run build.cmd

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
