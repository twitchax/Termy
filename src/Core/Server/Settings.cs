using System.IO;

namespace Termy
{
    public static class Settings
    {
        #region User Settings

        public static readonly string HostName = File.ReadAllText("/etc/secrets/hostname");

        #endregion

        #region User Secrets

        public static readonly string KubeConfigPath = "/etc/secrets/kubeconfig";
        public static readonly string SuperUserPassword = File.ReadAllText("/etc/secrets/supw");

        #endregion

        #region Constants

        #region Name Constants

        public static readonly string TerminalDomainNamePrefix = "t-";
        public static readonly int DefaultTerminalHttpPort = 80;
        public static readonly int DefaultTerminalPtyPort = 22;

        #endregion

        #region Kubernetes Constants

        public static readonly string KubeNamespace = "termy";
        public static readonly string KubeTerminalNamespace = "termy-terminals";
        public static readonly string KubeTermyServiceName = "termy-svc";
        public static readonly string KubeTermyIngressName = "termy-in";
        public static readonly string KubeTermyTerminalIngressName = "termy-terminal-in";

        #endregion

        #region YAML Templates

        public static readonly string TermyServiceYamlTemplate = File.ReadAllText("termy-service.yml");
        public static readonly string TermyIngressYamlTemplate = File.ReadAllText("termy-ingress.yml");
        public static readonly string TermyTerminalIngressYamlTemplate = File.ReadAllText("termy-terminal-ingress.yml");
        public static readonly string TerminalYamlTemplate = File.ReadAllText("termy-terminal-host.yml");

        #endregion

        #region Terminal Start Files

        public static readonly string TerminalHostServerFile = "termy-terminal-host";
        public static readonly string TerminalHostPuttyFile = "pty.node";
        public static readonly string TerminalHostStartScript = "start-terminal-host.sh";

        #endregion

        #endregion
    }
}