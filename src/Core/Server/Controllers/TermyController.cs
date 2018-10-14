using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Termy.Models;
using Termy.Services;

// TODO: Add ASP.NET Core logging (https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1).
// TODO: Let user keep their own entrypoint in Termy since using postStart hook (this means allowing them to also specify environment variables).
//   Choose: your entry point, default entrypoint, custom entrypoint.
// TODO: Explore DI for Settings?

namespace Termy.Controllers
{
    public class TermyController : BaseController
    {
        public TermyController(IKubernetesService kube, INodeStats nodeStats) : base(kube, nodeStats)
        {
        }

        [HttpGet("/api/version")]
        public IActionResult GetVersion()
        {
            return Ok("3.4.0");
        }

        [HttpGet("/api/terminal")]
        public async Task<IActionResult> GetTerminals()
        {
            var id = Helpers.GetId();
            Helpers.Log(id, $"Starting {nameof(GetTerminals)} ...");

            var terminals = (await Kube.GetDeploymentsAsync(Settings.KubeTerminalNamespace)).Select(t => {
                var name = t.Metadata.Name;
                var cnames = Helpers.ResolveCnames(t.Metadata.Annotations["cnames"]);

                return new TerminalResponse {
                    Name = name,
                    Replicas = t.Status.AvailableReplicas, 
                    CnameMaps = cnames
                };
            });

            Helpers.Log(id, $"Done.");
            return Ok(terminals);
        }

        [HttpGet("/api/terminal/{name}")]
        public async Task<IActionResult> GetTerminal(string name)
        {
            var id = Helpers.GetId();
            Helpers.Log(id, $"Starting {nameof(GetTerminal)} ...");

            var terminalService = await Kube.GetDeploymentsAsync(Settings.KubeTerminalNamespace).WithName(name);
            
            var response = new TerminalResponse {
                Name = name,
                Replicas = terminalService.Status.AvailableReplicas, 
                CnameMaps = Helpers.ResolveCnames(terminalService.Metadata.Annotations["cnames"])
            };

            Helpers.Log(id, $"Done.");
            return Ok(response);
        }

        [HttpDelete("/api/terminal")]
        public async Task<IActionResult> DeleteTerminals()
        {
            if(!this.IsSuperUser)
                return Unauthorized();
            
            var id = Helpers.GetId();
            Helpers.Log(id, $"Starting {nameof(DeleteTerminals)} ...");

            // Remove all ingress entries (except for the "dummy" one).
            await Kube.TransformIngressAsync(Settings.KubeTerminalIngressName, Settings.KubeTerminalNamespace, i => {
                i.Spec.Rules = i.Spec.Rules.Where(r => r.Host == "none.com").ToList();
            });

            // Delete all deployments and services in namespace.
            await Task.WhenAll(
                Kube.DeleteAllServicesAsync(Settings.KubeTerminalNamespace), 
                Kube.DeleteAllDeploymentsAsync(Settings.KubeTerminalNamespace)
            );

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

            // Get cnames for deployment.
            var cnamesString = (await Kube.GetDeploymentsAsync(Settings.KubeTerminalNamespace).WithName(name)).Metadata.Annotations["cnames"];
            var cnames = Helpers.ResolveCnames(cnamesString).ToList();

            // Remove all hosts and CNAMEs from ingress.
            await Kube.TransformIngressAsync(Settings.KubeTerminalIngressName, Settings.KubeTerminalNamespace, i => {
                i.Spec.Rules = i.Spec.Rules.Where(r => 
                    r.Host != name && 
                    r.Host != $"{Settings.TerminalDomainNamePrefix}{name}" && 
                    !cnames.Select(c => c.Name).Contains(r.Host)
                ).ToList();
            });

            // Delete deployment and service.
            await Task.WhenAll(
                Kube.DeleteServiceAsync(name, Settings.KubeTerminalNamespace), 
                Kube.DeleteDeploymentAsync(name, Settings.KubeTerminalNamespace)
            );
            
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
            Helpers.Log(id, $"   CNAMEs: {request.Cnames}");

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
            if(request.Name.StartsWith(Settings.TerminalDomainNamePrefix))
            {
                Helpers.Log(id, $"Failed: bad name: began with `{Settings.TerminalDomainNamePrefix}`.");
                return this.BadRequest($"The name must not begin with `{Settings.TerminalDomainNamePrefix}`.");
            }
            var existing = await Kube.GetServicesAsync(Settings.KubeTerminalNamespace).WithName(request.Name);
            if(existing != null)
            {
                Helpers.Log(id, $"Failed: terminal name already exists.");
                return this.BadRequest("A terminal with this name already exists.");
            }
            if(!Helpers.IsCnamesValid(request.Cnames))
            {
                Helpers.Log(id, $"Failed: provided CNAMEs could not be parsed or are invalid.");
                return this.BadRequest("Provided CNAMEs could not be parsed or are invalid.");
            }

            // Clean up CNAMEs and add default ones.
            var cnames = Helpers.ResolveCnames(request.Cnames).ToList();
            cnames.Insert(0, new CnameMap { Name = $"{Settings.TerminalDomainNamePrefix}{request.Name}.{Settings.HostName}", Port = Settings.DefaultTerminalPtyPort });
            cnames.Insert(0, new CnameMap { Name = $"{request.Name}.{Settings.HostName}", Port = Settings.DefaultTerminalHttpPort });

            // TODO: Check that there are no duplicate CNAMEs in this list, and check that there are no duplicate CNAMEs in the ingress.

            // Create yaml for kube deployment.
            Helpers.Log(id, $"Creating k8s yaml ...");

            var terminalServiceYamlText = Settings.TerminalServiceYamlTemplate
                .Replace("{{name}}", request.Name)
                .Replace("{{namespace}}", Settings.KubeTerminalNamespace)
                .Replace("{{port}}", Settings.DefaultTerminalPtyPort.ToString());

            var terminalYamlText = Settings.TerminalYamlTemplate
                .Replace("{{name}}", request.Name)
                .Replace("{{namespace}}", Settings.KubeTerminalNamespace)
                .Replace("{{image}}", request.Image)
                .Replace("{{password}}", request.Password)
                .Replace("{{shell}}", request.Shell)
                .Replace("{{command}}", request.Command.Replace("\"", "\\\""))
                .Replace("{{port}}", Settings.DefaultTerminalPtyPort.ToString())
                .Replace("{{cnames}}", string.Join(" ", cnames))
                .Replace("{{termyhostname}}", Helpers.TermyClusterHostname);

            var service = Kube.LoadFromString<V1Service>(terminalServiceYamlText);
            var deployment = Kube.LoadFromString<Extensionsv1beta1Deployment>(terminalYamlText);

            // TODO: Open all of the ports found in the CNAMEs (similar to the way ingress rules are applied).
            
            // Apply deployment.
            Helpers.Log(id, $"Applying k8s deployment ...");
            await Kube.CreateServiceAsync(Settings.KubeTerminalNamespace, service);
            await Kube.CreateDeploymentAsync(Settings.KubeTerminalNamespace, deployment);

            // Add to the ingress configuration.
            await Kube.AddIngressRuleAsync(
                Settings.KubeTerminalIngressName, 
                Settings.KubeTerminalNamespace, 
                request.Name,
                cnames.Select(c => (c.Name, c.Port))
            );
            
            // Finalize.
            Helpers.Log(id, $"Done.");
            return Ok($"{Settings.TerminalDomainNamePrefix}{request.Name}.{Settings.HostName}");
        }

        [HttpGet("/api/node/stats")]
        public IActionResult GetNodeStats()
        {
            var id = Helpers.GetId();
            Helpers.Log(id, $"Starting {nameof(GetNodeStats)} ...");

            var nodeStats = this.NodeStats;

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
}
