using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Termy.Controllers
{
    public class TermyController : Controller
    {
        [HttpGet("/api/version")]
        public IActionResult GetVersion()
        {
            return Ok("3.0.0");
        }

        [HttpGet("/api/terminal")]
        public async Task<IActionResult> GetTerminals()
        {
            var id = GetId();
            Console.WriteLine($" [{id}] Starting {nameof(GetTerminals)} ...");

            var (terminals, error) = await RunKubeCommand(id, $"get services --namespace={Helpers.KubeNamespace}");

            Console.WriteLine($" [{id}] Done.");
            return Ok(TextToJArray(terminals));
        }

        [HttpGet("/api/terminal/{name}")]
        public async Task<IActionResult> GetTerminal(string name)
        {
            var id = GetId();
            Console.WriteLine($" [{id}] Starting {nameof(GetTerminal)} ...");

            var (terminals, error) = await RunKubeCommand(id, $"get services {name} --namespace={Helpers.KubeNamespace}");

            Console.WriteLine($" [{id}] Done.");
            return Ok(terminals);
        }

        [HttpDelete("/api/terminal")]
        public async Task<IActionResult> DeleteTerminals()
        {
            var id = GetId();
            Console.WriteLine($" [{id}] Starting {nameof(DeleteTerminals)} ...");

            await RunKubeCommand(id, $"delete deployment,service --namespace={Helpers.KubeNamespace} --all");

            await RunAzCommand(id, Helpers.AzLoginCommand);
            var records = (await RunAzCommand(id, $"network dns record-set list -z {Helpers.AzDnsZone} -g {Helpers.AzGroup} --query [].name -o tsv")).Standard.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s) && s != "@");

            foreach(var record in records)
                await RunAzCommand(id, $"network dns record-set a delete -z {Helpers.AzDnsZone} -g {Helpers.AzGroup} -n {record} -y");

            Console.WriteLine($" [{id}] Done.");
            return Ok();
        }

        [HttpDelete("/api/terminal/{name}")]
        public async Task<IActionResult> DeleteTerminal(string name)
        {
            var id = GetId();
            Console.WriteLine($" [{id}] Starting {nameof(DeleteTerminal)} ...");

            await RunKubeCommand(id, $"delete deployment,service {name} --namespace={Helpers.KubeNamespace}");

            await RunAzCommand(id, Helpers.AzLoginCommand);
            await RunAzCommand(id, $"network dns record-set a delete -z {Helpers.AzDnsZone} -g {Helpers.AzGroup} -n {name} -y");

            Console.WriteLine($" [{id}] Done.");
            return Ok();
        }

        [HttpPost("/api/terminal")]
        public async Task<IActionResult> CreateTerminal([FromBody]CreateTerminalRequest request)
        {
            var id = GetId();

            Console.WriteLine($" [{id}] Starting {nameof(CreateTerminal)} ...");

            Console.WriteLine($"    [{id}] Name:  {request.Name} ...");
            Console.WriteLine($"    [{id}] Image: {request.Image} ...");
            Console.WriteLine($"    [{id}] Shell: {request.Shell} ...");

            Console.WriteLine($" [{id}] Validating ...");

            if(!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return this.BadRequest(errors);
            }
            if(!new Regex("^[a-z0-9]([-a-z0-9]*[a-z0-9])?$").IsMatch(request.Name))
            {
                Console.WriteLine($" [{id}] Failed: bad name.");
                return this.BadRequest("The name must match: '^[a-z0-9]([-a-z0-9]*[a-z0-9])?$'.");
            }
            var existing = (await RunKubeCommand(id, $"describe services/{request.Name} --namespace={Helpers.KubeNamespace}")).Standard;
            if(!string.IsNullOrWhiteSpace(existing))
            {
                Console.WriteLine($" [{id}] Failed: terminal name already exists.");
                return this.BadRequest("A terminal with this name already exists.");
            }
            
            // Ensure namespace exists.
            Console.WriteLine($" [{id}] Ensuring k8s namespace ({Helpers.KubeNamespace}) ...");
            await RunKubeCommand(id, $"create namespace {Helpers.KubeNamespace}");

            // Ensure deployments directory exists.
            Directory.CreateDirectory("deployments");

            // Create yaml for kube deployment.
            Console.WriteLine($" [{id}] Creating k8s yaml ...");
            var terminalYamlText = Helpers.TerminalYamlTemplate.Replace("{{name}}", request.Name).Replace("{{image}}", request.Image).Replace("{{password}}", request.Password).Replace("{{shell}}", request.Shell);
            var terminalYamlPath = $"deployments/{request.Name}_{DateTime.Now.Ticks}.yml";
            await System.IO.File.WriteAllTextAsync(terminalYamlPath, terminalYamlText);

            // Create deployment.
            Console.WriteLine($" [{id}] Creating k8s deployment ...");
            await RunKubeCommand(id, $"create -f {terminalYamlPath}");
            await RetryUntil(id, "deployment", async () => {
                var deployments = TextToJArray((await RunKubeCommand(id, $"get deploy/{request.Name} --namespace={Helpers.KubeNamespace}")).Standard);

                return deployments?.FirstOrDefault()?.Value<string>("available");
            }, val => val != "0");
            var podName = TextToJArray((await RunKubeCommand(id, $"get pods -l=run={request.Name} --namespace={Helpers.KubeNamespace}")).Standard).FirstOrDefault().Value<string>("name");
            Console.WriteLine($" [{id}] Pod name is `{podName}`.");

            // Copy terminal host files to pod.
            Console.WriteLine($" [{id}] Copying host files to pod ...");
            await RunKubeCommand(id, $"cp {Helpers.TerminalHostServerFile} {Helpers.KubeNamespace}/{podName}:/app/{Helpers.TerminalHostServerFile}");
            await RunKubeCommand(id, $"cp {Helpers.TerminalHostPuttyFile} {Helpers.KubeNamespace}/{podName}:/app/{Helpers.TerminalHostPuttyFile}");

            // Exec the server on the pod.
            Console.WriteLine($" [{id}] Starting pty server on pod ...");
            await RunKubeCommand(id, $"exec {podName} --namespace={Helpers.KubeNamespace} /app/{Helpers.TerminalHostServerFile}");

            // Expose a load balancer to get a public IP.
            Console.WriteLine($" [{id}] Exposing k8s service for deployment ...");
            await RunKubeCommand(id, $"expose deployment {request.Name} --type=LoadBalancer --name={request.Name} --namespace={Helpers.KubeNamespace}");
            var ip = await RetryUntil(id, "IP", async () => {
                var (val, _) = await RunKubeCommand(id, $"get services/{request.Name} --namespace={Helpers.KubeNamespace} -o=jsonpath='{{.status.loadBalancer.ingress[].ip}}'");

                return val;
            }, val => val != "''");

            // Provision the DNS record in Azure.
            Console.WriteLine($" [{id}] Provisioning DNS record ...");
            await RunAzCommand(id, Helpers.AzLoginCommand);
            await RunAzCommand(id, $"network dns record-set a add-record -z {Helpers.AzDnsZone} -g {Helpers.AzGroup} -n {request.Name} -a {ip.Replace("\'", "")}");
            
            // Finalize.
            Console.WriteLine($" [{id}] Done.");
            return Ok((await RunKubeCommand(id, $"get services/{request.Name} --namespace={Helpers.KubeNamespace}")).Standard + "\n" + $"{request.Name}.{Helpers.AzDnsZone}");
        }

        [HttpPost("/api/kill")]
        public IActionResult KillTerminal([FromBody]KillRequest request)
        {
            if(request.AdminPassword == Helpers.AdminPassword)
            {
                Environment.FailFast("Kill requested: tearing down pod.");
                return Ok();
            }

            return this.Unauthorized();
        }

        private async Task<T> RetryUntil<T>(string id, string name, Func<Task<T>> func, Func<T, bool> predicate, uint maxRetry = 60)
        {
            T result = default(T);
            uint tryCount = 0;

            do
            {
                Console.WriteLine($" [{id}] Trying to get `{name}`: {result} ...");

                try
                {
                    result = await func();
                } catch(Exception) {}

                await Task.Delay(5000);

                tryCount++;
            } while(!predicate(result) && tryCount < maxRetry);

            if(tryCount >= maxRetry)
            {
                Console.WriteLine($" [{id}] Retry timed out for `{name}`.");
                throw new Exception($"Hit the max number of retries while trying to get `{name}`.");
            }

            Console.WriteLine($" [{id}] Got `{name}`: {result}.");
            return result;
        }

        private string GetId() => new string(Guid.NewGuid().ToString().Take(6).ToArray());

        private Task<(string Standard, string Error)> RunKubeCommand(string id, string args)
        {
            return RunCommand(id, "kubectl", $"{args} --kubeconfig={Helpers.KubeConfig}");
        }

        private Task<(string Standard, string Error)> RunDockerCommand(string id, string args)
        {
            return RunCommand(id, "docker", args);
        }

        private Task<(string Standard, string Error)> RunAzCommand(string id, string args)
        {
            return RunCommand(id, "/root/bin/az", args);
        }

        private Task<(string Standard, string Error)> RunCommand(string id, string command, string args)
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

                //Console.WriteLine($" [{id}] [{command}] {standard}");
                if(!string.IsNullOrEmpty(error))
                    Console.WriteLine($" [{id}] [{command}] [ERROR] {error}");

                return (standard, error);
            });
            t.Start();

            return t;
        }

        private JArray TextToJArray(string text)
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

            var props = lines.FirstOrDefault()?.Select(p => p.Replace("-", "").Replace("(", "").Replace(")", "").Replace(" ", "")).ToList();
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

    public class CreateTerminalRequest
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string Image { get; set; }
        
        public string Password { get; set; } = "null";
        public string Shell { get; set; } = "/bin/bash";
    }

    public class KillRequest
    {
        [Required]
        public string AdminPassword { get; set; }
    }
}
