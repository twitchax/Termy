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
    public class TermyController : BaseController
    {
        [HttpGet("/api/version")]
        public IActionResult GetVersion()
        {
            return Ok("3.2.0");
        }

        [HttpGet("/api/terminal")]
        public async Task<IActionResult> GetTerminals()
        {
            var id = GetId();
            Helpers.Log(id, $"Starting {nameof(GetTerminals)} ...");

            var (terminals, error) = await Helpers.RunKubeCommand(id, $"get services --namespace={Settings.KubeNamespace}");

            Helpers.Log(id, $"Done.");
            return Ok(Helpers.TextToJArray(terminals));
        }

        [HttpGet("/api/terminal/{name}")]
        public async Task<IActionResult> GetTerminal(string name)
        {
            var id = GetId();
            Helpers.Log(id, $"Starting {nameof(GetTerminal)} ...");

            var (terminals, error) = await Helpers.RunKubeCommand(id, $"get services {name} --namespace={Settings.KubeNamespace}");

            Helpers.Log(id, $"Done.");
            return Ok(terminals);
        }

        [HttpDelete("/api/terminal")]
        public async Task<IActionResult> DeleteTerminals()
        {
            if(!this.IsSuperUser)
                return Unauthorized();
            
            var id = GetId();
            Helpers.Log(id, $"Starting {nameof(DeleteTerminals)} ...");

            await Helpers.RunKubeCommand(id, $"delete deployment,service --namespace={Settings.KubeNamespace} --all");

            await Helpers.RunAzCommand(id, Settings.AzLoginCommand);
            var records = (await Helpers.RunAzCommand(id, $"network dns record-set list -z {Settings.AzDnsZone} -g {Settings.AzGroup} --query [].name -o tsv")).Standard.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s) && s != "@");

            foreach(var record in records)
                await Helpers.RunAzCommand(id, $"network dns record-set a delete -z {Settings.AzDnsZone} -g {Settings.AzGroup} -n {record} -y");

            Helpers.Log(id, $"Done.");
            return Ok();
        }

        [HttpDelete("/api/terminal/{name}")]
        public async Task<IActionResult> DeleteTerminal(string name)
        {
            if(!this.IsSuperUser)
                return Unauthorized();
            
            var id = GetId();
            Helpers.Log(id, $"Starting {nameof(DeleteTerminal)} ...");

            await Helpers.RunKubeCommand(id, $"delete deployment,service {name} --namespace={Settings.KubeNamespace}");

            await Helpers.RunAzCommand(id, Settings.AzLoginCommand);
            await Helpers.RunAzCommand(id, $"network dns record-set a delete -z {Settings.AzDnsZone} -g {Settings.AzGroup} -n {name} -y");

            Helpers.Log(id, $"Done.");
            return Ok();
        }

        [HttpPost("/api/terminal")]
        public async Task<IActionResult> CreateTerminal([FromBody]CreateTerminalRequest request)
        {
            if(!this.IsSuperUser)
                return Unauthorized();
            
            var id = GetId();

            Helpers.Log(id, $"Starting {nameof(CreateTerminal)} ...");

            Helpers.Log(id, $"   Name:  {request.Name} ...");
            Helpers.Log(id, $"   Image: {request.Image} ...");
            Helpers.Log(id, $"   Shell: {request.Shell} ...");

            Helpers.Log(id, $"Validating ...");

            if(!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return this.BadRequest(errors);
            }
            if(!new Regex("^[a-z0-9]([-a-z0-9]*[a-z0-9])?$").IsMatch(request.Name))
            {
                Helpers.Log(id, $"Failed: bad name.");
                return this.BadRequest("The name must match: '^[a-z0-9]([-a-z0-9]*[a-z0-9])?$'.");
            }
            var existing = (await Helpers.RunKubeCommand(id, $"describe services/{request.Name} --namespace={Settings.KubeNamespace}")).Standard;
            if(!string.IsNullOrWhiteSpace(existing))
            {
                Helpers.Log(id, $"Failed: terminal name already exists.");
                return this.BadRequest("A terminal with this name already exists.");
            }
            
            // Ensure namespace exists.
            Helpers.Log(id, $"Ensuring k8s namespace ({Settings.KubeNamespace}) ...");
            await Helpers.RunKubeCommand(id, $"create namespace {Settings.KubeNamespace}");

            // Ensure deployments directory exists.
            Directory.CreateDirectory("deployments");

            // Create yaml for kube deployment.
            Helpers.Log(id, $"Creating k8s yaml ...");
            var terminalYamlText = Settings.TerminalYamlTemplate
                .Replace("{{name}}", request.Name)
                .Replace("{{image}}", request.Image)
                .Replace("{{password}}", request.Password)
                .Replace("{{su}}", Settings.SuperUserPassword)
                .Replace("{{shell}}", request.Shell)
                .Replace("{{command}}", request.Command.Replace("\"", "\\\""))
                .Replace("{{port}}", request.Port.ToString());
            var terminalYamlPath = $"deployments/{request.Name}_{DateTime.Now.Ticks}.yml";
            await System.IO.File.WriteAllTextAsync(terminalYamlPath, terminalYamlText);

            // Create deployment.
            Helpers.Log(id, $"Creating k8s deployment ...");
            await Helpers.RunKubeCommand(id, $"create -f {terminalYamlPath}");
            await RetryUntil(id, "deployment", async () => {
                var deployments = Helpers.TextToJArray((await Helpers.RunKubeCommand(id, $"get deploy/{request.Name} --namespace={Settings.KubeNamespace}")).Standard);

                return deployments?.FirstOrDefault()?.Value<string>("available");
            }, val => val != "0");
            var podName = Helpers.TextToJArray((await Helpers.RunKubeCommand(id, $"get pods -l=run={request.Name} --namespace={Settings.KubeNamespace}")).Standard).FirstOrDefault().Value<string>("name");
            Helpers.Log(id, $"Pod name is `{podName}`.");

            // Copy terminal host files to pod.
            Helpers.Log(id, $"Copying host files to pod ...");
            await Helpers.RunKubeCommand(id, $"cp {Settings.TerminalHostServerFile} {Settings.KubeNamespace}/{podName}:/{Settings.TerminalHostServerFile}");
            await Helpers.RunKubeCommand(id, $"cp {Settings.TerminalHostPuttyFile} {Settings.KubeNamespace}/{podName}:/{Settings.TerminalHostPuttyFile}");
            await Helpers.RunKubeCommand(id, $"cp {Settings.TerminalHostStartScript} {Settings.KubeNamespace}/{podName}:/{Settings.TerminalHostStartScript}");

            // Exec the server on the pod.
            Helpers.Log(id, $"Starting pty server on pod ...");
            await Helpers.RunKubeCommand(id, $"exec {podName} -i --namespace={Settings.KubeNamespace} /start-host.sh");

            // Expose a load balancer to get a public IP.
            Helpers.Log(id, $"Exposing k8s service for deployment ...");
            await Helpers.RunKubeCommand(id, $"expose deployment {request.Name} --type=LoadBalancer --name={request.Name} --namespace={Settings.KubeNamespace}");
            var ip = await RetryUntil(id, "IP", async () => {
                var (val, _) = await Helpers.RunKubeCommand(id, $"get services/{request.Name} --namespace={Settings.KubeNamespace} -o=jsonpath='{{.status.loadBalancer.ingress[].ip}}'");

                return val;
            }, val => val != "''");

            // Provision the DNS record in Azure.
            Helpers.Log(id, $"Provisioning DNS record ...");
            await Helpers.RunAzCommand(id, Settings.AzLoginCommand);
            await Helpers.RunAzCommand(id, $"network dns record-set a add-record -z {Settings.AzDnsZone} -g {Settings.AzGroup} -n {request.Name} -a {ip.Replace("\'", "")}");
            
            // Finalize.
            Helpers.Log(id, $"Done.");
            return Ok((await Helpers.RunKubeCommand(id, $"get services/{request.Name} --namespace={Settings.KubeNamespace}")).Standard + "\n" + $"{request.Name}.{Settings.AzDnsZone}");
        }

        [HttpGet("/api/node/stats")]
        public async Task<IActionResult> GetNodeStats()
        {
            var id = GetId();
            Helpers.Log(id, $"Starting {nameof(GetNodeStats)} ...");

            var nodeStats = ActivityWorker.NodeStats;

            Helpers.Log(id, $"Done.");
            return Ok(nodeStats);
        }

        [HttpPost("/api/kill")]
        public IActionResult KillTerminal([FromBody]KillRequest request)
        {
            if(this.IsSuperUser)
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

        private string GetId() => new string(Guid.NewGuid().ToString().Take(6).ToArray());
    }

    public class CreateTerminalRequest
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string Image { get; set; }
        
        public int Port { get; set; } = 5443;
        public string Password { get; set; } = "null";
        public string Shell { get; set; } = "/bin/bash";
        public string Command { get; set; } = "while true; do sleep 30; done;";
    }

    public class KillRequest
    {
        [Required]
        public string AdminPassword { get; set; }
    }
}
