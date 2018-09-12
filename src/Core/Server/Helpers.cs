using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Termy
{
    public static class Helpers
    {
        public static void Log(string id, string message) => Console.WriteLine($" [{id}] {message}");

        public static Task<(string Standard, string Error)> RunKubeCommand(string id, string args)
        {
            return RunCommand(id, "kubectl", $"{args} --kubeconfig={Settings.KubeConfig}");
        }

        public static Task<(string Standard, string Error)> RunDockerCommand(string id, string args)
        {
            return RunCommand(id, "docker", args);
        }

        public static Task<(string Standard, string Error)> RunAzCommand(string id, string args)
        {
            return RunCommand(id, "/root/bin/az", args);
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
    }
}
