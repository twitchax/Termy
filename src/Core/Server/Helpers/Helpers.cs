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
using Termy.Models;
using Termy.Services;

namespace Termy
{
    public static class Helpers
    {
        public static void Log(string id, string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($" [{id}] {message}");
            Console.ResetColor();
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

        public static async void EnsureDependencies(IKubernetesService kube)
        {
            var tasks = new List<Task>();

            // Ensure deployments directory exists.
            Directory.CreateDirectory("deployments");

            // Ensure base services and ingresses exist.
            var existingService = await kube.GetServicesAsync(Settings.KubeNamespace).WithName(Settings.KubeTermyServiceName);
            if(existingService == null)
            {
                // Create termy service.
                Helpers.Log("SERVICE", $"Applying service dependency ...");
                var serviceYamlText = Settings.TermyServiceYamlTemplate
                    .Replace("{{namespace}}", Settings.KubeNamespace);
                
                tasks.Add(kube.ApplyAsync(serviceYamlText));
            }

            var existingIngress = await kube.GetIngressesAsync(Settings.KubeNamespace).WithName(Settings.KubeTermyIngressName);
            if(existingIngress == null)
            {
                // Create termy ingress.
                Helpers.Log("INGRESS", $"Applying ingress dependency ...");
                var ingressYamlText = Settings.TermyIngressYamlTemplate
                    .Replace("{{namespace}}", Settings.KubeNamespace)
                    .Replace("{{host}}", Settings.HostName);
                    
                tasks.Add(kube.ApplyAsync(ingressYamlText));
            }

            var existingTerminalIngress = await kube.GetIngressesAsync(Settings.KubeTerminalNamespace).WithName(Settings.KubeTerminalIngressName);
            if(existingTerminalIngress == null)
            {
                // Create termy terminal ingress.
                Helpers.Log("INGRESS", $"Applying terminal ingress dependency ...");
                var terminalIngressYamlText = Settings.TerminalIngressYamlTemplate
                    .Replace("{{namespace}}", Settings.KubeTerminalNamespace);
                    
                tasks.Add(kube.ApplyAsync(terminalIngressYamlText));
            }

            // Create the host script file.
            tasks.Add(File.WriteAllTextAsync(Settings.TerminalHostStartScript, (await File.ReadAllTextAsync(Settings.TerminalHostStartScriptTemplate)).Replace("{{termyhostname}}", TermyClusterHostname)));

            await Task.WhenAll(tasks);
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
    }

    public static class Extensions
    {
        public static List<T> AddRangeWithDaisy<T>(this List<T> list, IEnumerable<T> other)
        {
            list.AddRange(other);
            return list;
        }

        public static bool AreAnyDuplicates<T>(this IEnumerable<T> list)
        {
            var set = new HashSet<T>();
            return list.Any(e => !set.Add(e));
        }
    }
}
