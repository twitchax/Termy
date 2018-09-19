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
            var id = Helpers.GetId();
            Helpers.Log(id, $"Starting {nameof(GetTerminals)} ...");

            var (terminals, error) = await Helpers.RunKubeCommand(id, $"get services --namespace={Settings.KubeTerminalNamespace}");

            Helpers.Log(id, $"Done.");
            return Ok(Helpers.TextToJArray(terminals));
        }

        [HttpGet("/api/terminal/{name}")]
        public async Task<IActionResult> GetTerminal(string name)
        {
            var id = Helpers.GetId();
            Helpers.Log(id, $"Starting {nameof(GetTerminal)} ...");

            var (terminals, error) = await Helpers.RunKubeCommand(id, $"get services {name} --namespace={Settings.KubeTerminalNamespace}");

            Helpers.Log(id, $"Done.");
            return Ok(terminals);
        }

        [HttpDelete("/api/terminal")]
        public async Task<IActionResult> DeleteTerminals()
        {
            if(!this.IsSuperUser)
                return Unauthorized();
            
            var id = Helpers.GetId();
            Helpers.Log(id, $"Starting {nameof(DeleteTerminals)} ...");

            await Helpers.RunKubeCommand(id, $"delete deployment,service --namespace={Settings.KubeTerminalNamespace} --all");
            
            // TODO: Delete ingress entry.
            await Helpers.TransformTerminalIngress(id, ingressJson => {
                ingressJson["spec"]["rules"] = new JArray((ingressJson["spec"]["rules"] as JArray).Where(r => {
                    var host = (r as JObject).Value<string>("host");
                    return host == "none.com";
                }));
            });

            Helpers.Log(id, $"Done.");
            return Ok();
        }

        [HttpDelete("/api/terminal/{name}")]
        public async Task<IActionResult> DeleteTerminal(string name)
        {
            if(!this.IsSuperUser)
                return Unauthorized();
            
            var id = Helpers.GetId();
            Helpers.Log(id, $"Starting {nameof(DeleteTerminal)} ...");

            await Helpers.RunKubeCommand(id, $"delete deployment,service {name} --namespace={Settings.KubeTerminalNamespace}");

            await Helpers.TransformTerminalIngress(id, ingressJson => {
                ingressJson["spec"]["rules"] = new JArray((ingressJson["spec"]["rules"] as JArray).Where(r => {
                    var host = (r as JObject).Value<string>("host").Split(".").First();
                    return host != name && host != $"t-{name}";
                }));
            });

            Helpers.Log(id, $"Done.");
            return Ok();
        }

        [HttpPost("/api/terminal")]
        public async Task<IActionResult> CreateTerminal([FromBody]CreateTerminalRequest request)
        {
            if(!this.IsSuperUser)
                return Unauthorized();
            
            var id = Helpers.GetId();

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
            if(request.Name.StartsWith("t-"))
            {
                Helpers.Log(id, $"Failed: bad name: began with `t-`.");
                return this.BadRequest("The name must not begin with `t-`.");
            }
            var existing = (await Helpers.RunKubeCommand(id, $"describe services/{request.Name} --namespace={Settings.KubeTerminalNamespace}")).Standard;
            if(!string.IsNullOrWhiteSpace(existing))
            {
                Helpers.Log(id, $"Failed: terminal name already exists.");
                return this.BadRequest("A terminal with this name already exists.");
            }

            // Create yaml for kube deployment.
            Helpers.Log(id, $"Creating k8s yaml ...");
            var terminalYamlText = Settings.TerminalYamlTemplate
                .Replace("{{name}}", request.Name)
                .Replace("{{namespace}}", Settings.KubeTerminalNamespace)
                .Replace("{{image}}", request.Image)
                .Replace("{{password}}", request.Password)
                .Replace("{{shell}}", request.Shell)
                .Replace("{{command}}", request.Command.Replace("\"", "\\\""))
                .Replace("{{port}}", request.Port.ToString());
            var terminalYamlPath = $"deployments/{request.Name}_{DateTime.Now.Ticks}.yml";
            await System.IO.File.WriteAllTextAsync(terminalYamlPath, terminalYamlText);

            // Apply deployment.
            Helpers.Log(id, $"Applying k8s deployment ...");
            await Helpers.RunKubeCommand(id, $"apply -f {terminalYamlPath}");
            Console.WriteLine("Here 1!");
            await Helpers.RetryUntil(id, "deployment", async () => {
                var deployments = Helpers.TextToJArray((await Helpers.RunKubeCommand(id, $"get deploy/{request.Name} --namespace={Settings.KubeTerminalNamespace}")).Standard);

                return deployments?.FirstOrDefault()?.Value<string>("available");
            }, val => val != "0");
            Console.WriteLine("Here 2!");
            var podName = Helpers.TextToJArray((await Helpers.RunKubeCommand(id, $"get pods -l=terminal-run={request.Name} --namespace={Settings.KubeTerminalNamespace}")).Standard).FirstOrDefault().Value<string>("name");
            Helpers.Log(id, $"Pod name is `{podName}`.");
            Console.WriteLine("Here 3!");

            // Copy terminal host files to pod.
            Helpers.Log(id, $"Copying host files to pod ...");
            await Helpers.RunKubeCommand(id, $"cp {Settings.TerminalHostServerFile} {Settings.KubeTerminalNamespace}/{podName}:/{Settings.TerminalHostServerFile}");
            await Helpers.RunKubeCommand(id, $"cp {Settings.TerminalHostPuttyFile} {Settings.KubeTerminalNamespace}/{podName}:/{Settings.TerminalHostPuttyFile}");
            await Helpers.RunKubeCommand(id, $"cp {Settings.TerminalHostStartScript} {Settings.KubeTerminalNamespace}/{podName}:/{Settings.TerminalHostStartScript}");

            // Exec the server on the pod.
            Helpers.Log(id, $"Starting pty server on pod ...");
            await Helpers.RunKubeCommand(id, $"exec {podName} -i --namespace={Settings.KubeTerminalNamespace} -- bash -c \"echo 'root:{Settings.SuperUserPassword}' | chpasswd\"");
            await Helpers.RunKubeCommand(id, $"exec {podName} -i --namespace={Settings.KubeTerminalNamespace} /{Settings.TerminalHostStartScript}");

            // Add to the ingress configuration.
            await Helpers.TransformTerminalIngress(id, ingressJson => {
                var ingressRuleObjects = new List<JObject>();
                ingressRuleObjects.Add(JObject.Parse(
                    Helpers.IngressTemplate
                        .Replace("{{host}}", $"{request.Name}.{Settings.HostName}")
                        .Replace("{{serviceName}}", request.Name)
                        .Replace("{{servicePort}}", 80.ToString())
                ));
                ingressRuleObjects.Add(JObject.Parse(
                    Helpers.IngressTemplate
                        .Replace("{{host}}", $"t-{request.Name}.{Settings.HostName}")
                        .Replace("{{serviceName}}", request.Name)
                        .Replace("{{servicePort}}", request.Port.ToString())
                ));

                foreach(var obj in ingressRuleObjects)
                    (ingressJson["spec"]["rules"] as JArray).Add(obj);
            });
            
            // Finalize.
            Helpers.Log(id, $"Done.");
            return Ok((await Helpers.RunKubeCommand(id, $"get services/{request.Name} --namespace={Settings.KubeTerminalNamespace}")).Standard + "\n" + $"t-{request.Name}.{Settings.HostName}");
        }

        [HttpGet("/api/node/stats")]
        public async Task<IActionResult> GetNodeStats()
        {
            var id = Helpers.GetId();
            Helpers.Log(id, $"Starting {nameof(GetNodeStats)} ...");

            var nodeStats = Workers.NodeStats;

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
    }

    public class CreateTerminalRequest
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string Image { get; set; }
        
        public int Port { get; set; } = 5080;
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
