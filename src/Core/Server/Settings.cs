using System.IO;

namespace Termy
{
    public static class Settings
    {
        public static readonly string CertFile = "/etc/secrets/cert.pfx";
        public static readonly string CertPassword = File.ReadAllText("/etc/secrets/certpw");

        public static readonly string KubeConfig = "\"/etc/secrets/kubeconfig\"";
        public static readonly string KubeNamespace = "\"terminals\"";

        public static readonly string AzLoginCommand = File.ReadAllText("/etc/secrets/azlogin");
        public static readonly string AzDnsZone = "box.termy.in";
        public static readonly string AzGroup = "Termy";

        public static readonly string SuperUserPassword = File.ReadAllText("/etc/secrets/supw");

        public static readonly string TerminalYamlTemplate = File.ReadAllText("terminal.yml");
        public static readonly string TerminalHostServerFile = "termy-terminal-host";
        public static readonly string TerminalHostPuttyFile = "pty.node";
        public static readonly string TerminalHostStartScript = "start-host.sh";
    }
}