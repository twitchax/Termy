using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Termy
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseContentRoot("wwwroot/dist")
                .UseKestrel(options => 
                {
                    options.Listen(IPAddress.Any, 80);
                    options.Listen(IPAddress.Any, 443, listenOptions =>
                    {
                        listenOptions.UseHttps(new HttpsConnectionAdapterOptions
                        {
                            ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(Helpers.CertFile, Helpers.CertPassword), 
                            SslProtocols = SslProtocols.Tls12
                        });
                    });
                }).Build();
    }
}
