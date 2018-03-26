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

namespace Termy.Controllers
{
    public class TermyController : Controller
    {
        [HttpGet("/")]
        public IActionResult GetRoot()
        {
            return Ok();
        }

        [HttpGet("/api/version")]
        public IActionResult GetVersion()
        {
            return Ok("1.3.1");
        }

        [HttpGet("/api/docker/images")]
        public async Task<IActionResult> GetImages()
        {
            var id = GetId();
            Console.WriteLine($" [{id}] Starting {nameof(GetImages)} ...");

            var images = await RunDockerCommand(id, "images");

            Console.WriteLine($" [{id}] Done.");
            return Ok(images.Standard);
        }

        [HttpDelete("/api/docker/images")]
        public async Task<IActionResult> DeleteImages()
        {
            var id = GetId();
            Console.WriteLine($" [{id}] Starting {nameof(DeleteImages)} ...");

            var imageIds = (await RunDockerCommand(id, "images -q")).Standard.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s));

            foreach(var imageId in imageIds)
                await RunDockerCommand(id, $"rmi -f {imageId}");

            Console.WriteLine($" [{id}] Done.");
            return Ok();
        }

        [HttpGet("/api/terminals")]
        public async Task<IActionResult> GetTerminals()
        {
            var id = GetId();
            Console.WriteLine($" [{id}] Starting {nameof(GetTerminals)} ...");

            var (terminals, error) = await RunKubeCommand(id, $"get services --namespace={Helpers.KubeNamespace}");

            Console.WriteLine($" [{id}] Done.");
            return Ok(terminals);
        }

        [HttpGet("/api/terminals/{name}")]
        public async Task<IActionResult> GetTerminal(string name)
        {
            var id = GetId();
            Console.WriteLine($" [{id}] Starting {nameof(GetTerminal)} ...");

            var (terminals, error) = await RunKubeCommand(id, $"get services {name} --namespace={Helpers.KubeNamespace}");

            Console.WriteLine($" [{id}] Done.");
            return Ok(terminals);
        }

        [HttpDelete("/api/terminals")]
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

        [HttpDelete("/api/terminals/{name}")]
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

        [HttpPost("/api/terminals")]
        public async Task<IActionResult> CreateTerminal([FromBody]CreateTerminalRequest request)
        {
            var id = GetId();
            Console.WriteLine($" [{id}] Starting {nameof(CreateTerminal)} ...");

            Console.WriteLine($"    [{id}] Name:  {request.Name} ...");
            Console.WriteLine($"    [{id}] Image: {request.Image} ...");
            Console.WriteLine($"    [{id}] Tag:   {request.Tag} ...");
            Console.WriteLine($"    [{id}] Shell: {request.Shell} ...");

            Console.WriteLine($" [{id}] Validating ...");
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

            Console.WriteLine($" [{id}] Creating image ...");
            var (_, buildError) = await RunDockerCommand(id, $"build -t {request.Tag} --build-arg IMAGE=\"{request.Image}\" --build-arg ROOTPW=\"{request.RootPassword}\" .");
            if(!string.IsNullOrWhiteSpace(buildError))
            {
                Console.WriteLine($" [{id}] Failed: could not build docker image.");
                return this.BadRequest("Could not build docker image..");
            }

            Console.WriteLine($" [{id}] Pushing image to docker ...");
            await RunDockerCommand(id, $"login -u {request.DockerUsername} -p {request.DockerPassword}");
            await RunDockerCommand(id, $"push {request.Tag}");
            await RunDockerCommand(id, "logout");
            
            Console.WriteLine($" [{id}] Pushing k8s deployment ...");
            await RunKubeCommand(id, $"create namespace terminals");
            await RunKubeCommand(id, $"run {request.Name} --image=\"{request.Tag}\" --port=80 --labels=\"name=\" --namespace={Helpers.KubeNamespace} --env=\"DEFAULTSHELL={request.Shell}\"");
            await RunKubeCommand(id, $"expose deployment {request.Name} --type=LoadBalancer --name={request.Name} --namespace={Helpers.KubeNamespace}");

            var ip = "";
            do
            {
                (ip, _) = await RunKubeCommand(id, $"get services/{request.Name} --namespace={Helpers.KubeNamespace} -o=jsonpath='{{.status.loadBalancer.ingress[].ip}}'");
                Console.WriteLine($" [{id}] Waiting for ip: {ip} ...");
                await Task.Delay(5000);
            } while(ip == "''");

            Console.WriteLine($" [{id}] Provisioning DNS record ...");
            await RunAzCommand(id, Helpers.AzLoginCommand);
            await RunAzCommand(id, $"network dns record-set a add-record -z {Helpers.AzDnsZone} -g {Helpers.AzGroup} -n {request.Name} -a {ip.Replace("\'", "")}");
            
            Console.WriteLine($" [{id}] Done.");
            return Ok((await RunKubeCommand(id, $"get services/{request.Name} --namespace={Helpers.KubeNamespace}")).Standard + "\n" + $"{request.Name}.{Helpers.AzDnsZone}");
        }

        [HttpPost("/api/kill")]
        public IActionResult KillTerminal([FromBody]KillRequest request)
        {
            if(request.AdminPassword == Helpers.AdminPassword)
            {
                Environment.Exit(0);
                return Ok();
            }

            return this.Unauthorized();
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
    }

    public class CreateTerminalRequest
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string Image { get; set; }
        [Required]
        public string Tag { get; set; }
        [Required]
        public string RootPassword { get; set; }

        public string Shell { get; set; } = "/bin/bash";

        [Required]
        public string DockerUsername { get; set; }
        [Required]
        public string DockerPassword { get; set; }
    }

    public class KillRequest
    {
        [Required]
        public string AdminPassword { get; set; }
    }
}
