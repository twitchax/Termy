using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using k8s;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Termy.Controllers;

namespace Termy
{
    public static class Helpers
    {
        public static void Log(string id, string message) => Console.WriteLine($" [{id}] {message}");

        private static KubernetesClientConfiguration KubeConfig => KubernetesClientConfiguration.BuildConfigFromConfigFile(Settings.KubeConfigPath);
        public static Kubernetes KubeClient => new k8s.Kubernetes(KubeConfig);

        public static Task<(string Standard, string Error)> RunKubeCommand(string id, string args)
        {
            return RunCommand(id, "kubectl", $"{args} --kubeconfig=\"{Settings.KubeConfigPath}\"");
        }

        public static Task<(string Standard, string Error)> RunCertbotCommand(string id, string args)
        {
            return RunCommand(id, "certbot", args);
        }

        public static Task<(string Standard, string Error)> RunCommand(string id, string command, string args)
        {
            var t = new Task<(string Standard, string Error)>(() => 
            {
                Process process = new Process(); 
                process.StartInfo.FileName = command;
                process.StartInfo.Arguments = args;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                var a = process.Start();
                process.WaitForExit();

                var standard = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                //Helpers.Log(id, $"[{command}] {standard}");
                if(!string.IsNullOrEmpty(error))
                    Helpers.Log(id, $"[{command}] [ERROR] {error}");

                return (standard, error);
            });
            t.Start();

            return t;
        }

        public static string TermyClusterHostname => $"{Settings.KubeTermyServiceName}.{Settings.KubeNamespace}";

        public static JArray TextToJArray(string text)
        {
            var lines = text
                .Split('\n')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(line => line
                    .Split("  ")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToLower())
                    .ToList())
                .ToList();

            var props = lines.FirstOrDefault()?.Select(p => p.Replace("-", "").Replace("%", "").Replace("(", "").Replace(")", "").Replace(" ", "")).ToList();
            var valueLines = lines.Count > 1 ? lines.Skip(1).ToList() : new List<List<string>>();

            var list = new JArray();
            foreach(var valueLine in valueLines)
            {
                var obj = new JObject();

                for(int k = 0; k < props.Count; k++)
                {
                    obj.Add(props[k], valueLine[k]);
                }

                list.Add(obj);
            }
            
            return list;
        }

        public static async Task TransformTerminalIngress(string id, Action<JObject> transform)
        {
            Helpers.Log(id, $"Tranforming terminal ingress ...");
            var (ingressText, _) = await Helpers.RunKubeCommand(id, $"get ingress termy-terminal-in --namespace={Settings.KubeTerminalNamespace} -o json");
            var ingressJson = JObject.Parse(ingressText);

            transform(ingressJson);

            var ingressJsonPath = $"deployments/ingress_transform_{DateTime.Now.Ticks}.json";
            await System.IO.File.WriteAllTextAsync(ingressJsonPath, ingressJson.ToString());

            await Helpers.RunKubeCommand(id, $"apply -f {ingressJsonPath}");
        }

        public static async void EnsureDependencies()
        {
            // Ensure deployments directory exists.
            Directory.CreateDirectory("deployments");

            // Ensure base services and ingresses exist.
            var existingService = (await Helpers.RunKubeCommand("SERVICE", $"describe service/{Settings.KubeTermyServiceName} --namespace={Settings.KubeNamespace}")).Standard;
            if(string.IsNullOrWhiteSpace(existingService))
            {
                // Create termy service.
                Helpers.Log("SERVICE", $"Applying service dependency ...");
                var serviceYamlText = Settings.TermyServiceYamlTemplate
                    .Replace("{{namespace}}", Settings.KubeNamespace);
                var serviceYamlPath = $"service_{DateTime.Now.Ticks}.yml";
                await System.IO.File.WriteAllTextAsync(serviceYamlPath, serviceYamlText);
                await Helpers.RunKubeCommand("STARTUP", $"apply -f {serviceYamlPath}");
            }

            var existingIngress = (await Helpers.RunKubeCommand("INGRESS", $"describe ingress/{Settings.KubeTermyIngressName} --namespace={Settings.KubeNamespace}")).Standard;
            if(string.IsNullOrWhiteSpace(existingIngress))
            {
                // Create termy ingress.
                Helpers.Log("INGRESS", $"Applying ingress dependency ...");
                var ingressYamlText = Settings.TermyIngressYamlTemplate
                    .Replace("{{namespace}}", Settings.KubeNamespace)
                    .Replace("{{host}}", Settings.HostName);
                var ingressYamlPath = $"ingress_{DateTime.Now.Ticks}.yml";
                await System.IO.File.WriteAllTextAsync(ingressYamlPath, ingressYamlText);
                await Helpers.RunKubeCommand("STARTUP", $"apply -f {ingressYamlPath}");
            }

            var existingTerminalIngress = (await Helpers.RunKubeCommand("INGRESS", $"describe ingress/{Settings.KubeTermyTerminalIngressName} --namespace={Settings.KubeTerminalNamespace}")).Standard;
            if(string.IsNullOrWhiteSpace(existingTerminalIngress))
            {
                // Create termy terminal ingress.
                Helpers.Log("INGRESS", $"Applying terminal ingress dependency ...");
                var terminalIngressYamlText = Settings.TermyTerminalIngressYamlTemplate
                    .Replace("{{namespace}}", Settings.KubeTerminalNamespace);
                var terminalIngressYamlPath = $"terminal_ingress_{DateTime.Now.Ticks}.yml";
                await System.IO.File.WriteAllTextAsync(terminalIngressYamlPath, terminalIngressYamlText);
                await Helpers.RunKubeCommand("STARTUP", $"apply -f {terminalIngressYamlPath}");
            }

            // Create the host script file.
            await File.WriteAllTextAsync(Settings.TerminalHostStartScript, (await File.ReadAllTextAsync(Settings.TerminalHostStartScriptTemplate)).Replace("{{termyhostname}}", TermyClusterHostname));
        }

        public static async Task<T> RetryUntil<T>(string id, string name, Func<Task<T>> func, Func<T, bool> predicate, uint maxRetry = 60)
        {
            T result = default(T);
            uint tryCount = 0;

            do
            {
                Helpers.Log(id, $"Trying to get `{name}`: {result} ...");

                try
                {
                    result = await func();
                } catch(Exception) {}

                await Task.Delay(5000);

                tryCount++;
            } while(!predicate(result) && tryCount < maxRetry);

            if(tryCount >= maxRetry)
            {
                Helpers.Log(id, $"Retry timed out for `{name}`.");
                throw new Exception($"Hit the max number of retries while trying to get `{name}`.");
            }

            Helpers.Log(id, $"Got `{name}`: {result}.");
            return result;
        }

        public static bool IsCnamesValid(string cnamesString)
        {
            try
            {
                return ResolveCnames(cnamesString).All(t => Uri.CheckHostName(t.Name) != UriHostNameType.Unknown);
            }
            catch(Exception)
            {
                return false;
            }
        }

        public static IEnumerable<CnameMap> ResolveCnames(string cnamesString)
        {
            try
            {
                if(string.IsNullOrWhiteSpace(cnamesString))
                    return new List<CnameMap>();
                else
                    return cnamesString.Trim().Replace("\"", "").Replace("'", "").Split(' ', ',').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => {
                        var splits = s.Trim().ToLower().Split(':');

                        return new CnameMap {
                            Name = splits[0], 
                            Port = int.Parse(splits[1])
                        };
                    });
            }
            catch(Exception)
            {
                throw new Exception("CNAMEs should be validated upon creation request: this should never happen.");
            }
        }

        public static string GetId() => new string(Guid.NewGuid().ToString().Take(6).ToArray());
        
        public static string IngressTemplate => @"
            {
                ""host"": ""{{host}}"",
                ""http"": {
                    ""paths"": [
                        {
                            ""backend"": {
                                ""serviceName"": ""{{serviceName}}"",
                                ""servicePort"": {{servicePort}}
                            }
                        }
                    ]
                }
            }
        ";
    }
}
