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
        public static string CertFile => "/etc/secrets/cert.pfx";
        public static string CertPassword => File.ReadAllText("/etc/secrets/certpw");

        public static string KubeConfig => "\"/etc/secrets/kubeconfig\"";
        public static string KubeNamespace => "\"terminals\"";
    }
}
