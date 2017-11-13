using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Termy
{
    public static class Helpers
    {
        public static readonly string CertFile = "/etc/secrets/cert.pfx";
        public static readonly string CertPassword = File.ReadAllText("/etc/secrets/certpw");

        public static readonly string KubeConfig = "\"/etc/secrets/kubeconfig\"";
        public static readonly string KubeNamespace = "\"terminals\"";

        public static readonly string AzLoginCommand = File.ReadAllText("/etc/secrets/azlogin");
        public static readonly string AzDnsZone = "box.termy.in";
        public static readonly string AzGroup = "Termy";
    }
}
