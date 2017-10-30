using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Docker.DotNet;
using k8s;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Termy
{
    public static class Helpers
    {
        public static string KubeConfig => "\"/etc/kube/config\"";

        public static string KubeNamespace => "\"terminals\"";
    }
}
