using System;
using System.IO;
using System.Linq;

namespace Termy
{
    public static class Settings
    {
        #region User Settings

        public static readonly string HostName = File.ReadAllText(ResolvePath(Environment.GetEnvironmentVariable("TERMY_HOSTNAME_PATH"), "/etc/secrets/hostname"));

        #endregion

        #region User Secrets

        public static readonly string KubeConfigPath = ResolvePath(Environment.GetEnvironmentVariable("TERMY_KUBECONFIG_PATH"), "/etc/secrets/kubeconfig");
        public static readonly string SuperUserPassword = File.ReadAllText(ResolvePath(Environment.GetEnvironmentVariable("TERMY_SUPW_PATH"), "/etc/secrets/supw"));

        #endregion

        #region Constants

        #region Name Constants

        public static readonly string TerminalPtyDomainNamePrefix = "t-";
        public static readonly string TerminalSshDomainNamePrefix = "ssh-";
        public static readonly int DefaultTerminalHttpPort = 80;
        public static readonly int DefaultTerminalSshPort = 22;
        public static readonly int DefaultTerminalPtyPort = 8022;

        #endregion

        #region Work Constances

        public static readonly int WorkerUpdateIntervalInSeconds = 10;
        public static readonly int WorkerQueueLength = 2 /* hours */ * 60 /* minutes/hr */ * 60 /* seconds/minute */ / WorkerUpdateIntervalInSeconds;

        #endregion

        #region Kubernetes Constants

        public static readonly string KubeNamespace = "termy";
        public static readonly string KubeTerminalNamespace = "termy-terminals";
        public static readonly string KubeTermyServiceName = "termy-svc";
        public static readonly string KubeTermyIngressName = "termy-in";
        public static readonly string KubeTerminalIngressName = "termy-terminal-in";

        #endregion

        #region YAML Templates

        public static readonly string TermyServiceYamlTemplate = File.ReadAllText("assets/termy-service.yml");
        public static readonly string TermyIngressYamlTemplate = File.ReadAllText("assets/termy-ingress.yml");
        public static readonly string TerminalIngressYamlTemplate = File.ReadAllText("assets/terminal-ingress.yml");
        public static readonly string TerminalServiceYamlTemplate = File.ReadAllText("assets/terminal-service.yml");
        public static readonly string TerminalYamlTemplate = File.ReadAllText("assets/terminal.yml");

        #endregion

        #region Terminal Start Files

        public static readonly string TerminalHostServerFile = "assets/termy-terminal-host";
        public static readonly string TerminalHostPuttyFile = "assets/pty.node";
        public static readonly string TerminalHostStartScriptTemplate = "assets/start-terminal-host-template.sh";
        public static readonly string TerminalHostStartScript = "assets/start-terminal-host.sh";

        #endregion

        #endregion

        #region Helpers

        internal static string ResolvePath(params string[] paths)
        {
            return paths.FirstOrDefault(f => File.Exists(f)) ?? throw new Exception($"Could not resolve setting for paths ({string.Join(", ", paths)}).");
        }

        #endregion
    }
}