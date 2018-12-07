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
using Microsoft.Rest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Termy.Models;
using Termy.Services;

// Properly do helm chart secrets.
// TODO: Allow assigning min resources and limits.

// TODO: Explore DI for logging?
// TODO: Explore DI for Settings?

namespace Termy.Controllers
{
    public class TermyController : BaseController
    {
        public TermyController(IKubernetesService kube) : base(kube)
        {
        }

        [HttpGet("/api/version")]
        public IActionResult GetVersion()
        {
            return Ok("4.0.0");
        }

        [HttpGet("/api/terminal")]
        public async Task<IActionResult> GetTerminals()
        {
            var id = Helpers.GetId();
            Helpers.Log(id, $"Starting {nameof(GetTerminals)} ...");

            var terminals = (await Kube.GetTerminalDeploymentsAsync(Settings.KubeNamespace)).Select(t => {
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

            var terminal = await Kube.GetTerminalDeploymentsAsync(Settings.KubeNamespace).WithName(name);

            // Bail out if not found.
            if(terminal == null)
            {
                Helpers.Log(id, $"Done.");
                return NotFound();
            }
            
            var response = new TerminalResponse {
                Name = name,
                Replicas = terminal.Status.AvailableReplicas, 
                CnameMaps = Helpers.ResolveCnames(terminal.Metadata.Annotations["cnames"])
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

            // Remove all ingress entries (except for the "termy" one).
            await Kube.TransformIngressAsync(Settings.KubeIngressName, Settings.KubeNamespace, i => {
                i.Spec.Rules = i.Spec.Rules.Where(r => r.Host == Settings.HostName || r.Host == $"dashboard.{Settings.HostName}").ToList();
            });

            // Delete all deployments and services in namespace.
            await Task.WhenAll(
                Kube.DeleteAllTerminalServicesAsync(Settings.KubeNamespace), 
                Kube.DeleteAllTerminalDeploymentsAsync(Settings.KubeNamespace)
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

            // TODO: This needs to be smart if the deployment does not exist.  Right now, it will just fail ungracefully.

            // Get cnames for deployment.
            var cnamesString = (await Kube.GetTerminalDeploymentsAsync(Settings.KubeNamespace).WithName(name))?.Metadata.Annotations["cnames"];

            // Fail fast if there is no deployment, since there will be no other resources to clean up if there is no deployment.
            if(cnamesString == null)
            {
                Helpers.Log(id, $"Done.");
                return NotFound();
            }

            // Remove all hosts and CNAMEs from ingress.
            // TODO: Is ths
            var cnames = Helpers.ResolveCnames(cnamesString).ToList();
            await Kube.TransformIngressAsync(Settings.KubeIngressName, Settings.KubeNamespace, i => {
                i.Spec.Rules = i.Spec.Rules.Where(r => 
                    r.Host !=$"{name}.{Settings.HostName}" && 
                    r.Host != $"{Settings.TerminalPtyDomainNamePrefix}{name}.{Settings.HostName}" && 
                    r.Host != $"{Settings.TerminalSshDomainNamePrefix}{name}.{Settings.HostName}" && 
                    !cnames.Select(c => c.Name).Contains(r.Host)
                ).ToList();
            });

            // Delete deployment and service.
            await Task.WhenAll(
                Kube.DeleteServiceAsync(name, Settings.KubeNamespace), 
                Kube.DeleteDeploymentAsync(name, Settings.KubeNamespace)
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
            
            var (valid, cnames, error) = await this.IsCreateTerminalRequestValid(id, request);
            if(!valid)
                return this.BadRequest(id, error);
                
            // Create yaml for kube deployment.
            Helpers.Log(id, $"Creating k8s yaml ...");

            var terminalServiceYamlText = Settings.TerminalServiceYamlTemplate
                .Replace("{{name}}", request.Name)
                .Replace("{{namespace}}", Settings.KubeNamespace);

            var terminalYamlText = Settings.TerminalYamlTemplate
                .Replace("{{name}}", request.Name)
                .Replace("{{namespace}}", Settings.KubeNamespace)
                .Replace("{{image}}", request.Image)
                .Replace("{{ptyPassword}}", request.Password)
                .Replace("{{ptyShell}}", request.Shell)
                .Replace("{{ptyPort}}", Settings.DefaultTerminalPtyPort.ToString())
                .Replace("{{cnames}}", string.Join(" ", cnames))
                .Replace("{{termyhostname}}", Helpers.TermyClusterHostname);

            var service = Kube.LoadFromString<V1Service>(terminalServiceYamlText);
            var deployment = Kube.LoadFromString<Extensionsv1beta1Deployment>(terminalYamlText);

            // Set the proper startup commands.
            deployment.SetStartup(request.Entrypoint, request.EnvironmentVariables, request.Command);

            // Open all requested ports.
            service.SetPorts(cnames);
            deployment.SetPorts(cnames);
            
            // Apply deployment.
            Helpers.Log(id, $"Applying k8s deployment ...");
            
            try
            {
                // The k8s deployments acts as the source of truth, so create it first.
                // If it fails, then there will be nothing else to clean up, anyway.
                await Kube.CreateDeploymentAsync(Settings.KubeNamespace, deployment);
                await Kube.CreateServiceAsync(Settings.KubeNamespace, service);

                // Add to the ingress configuration.
                await Kube.AddIngressRuleAsync(
                    Settings.KubeIngressName,
                    Settings.KubeNamespace,
                    request.Name,
                    cnames.Select(c => (c.Name, c.Port))
                );
            }
            catch(HttpOperationException e)
            {
                // TODO: Rollback everything?
                return this.StatusCode(500, e.Response.Content);
            }

            // Ready response.
            var response = new TerminalResponse {
                Name = request.Name,
                CnameMaps = cnames
            };
            
            // Finalize.
            Helpers.Log(id, $"Done.");
            return Created($"{Settings.HostName}/api/terminal/{request.Name}", response);
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

        private async Task<(bool Valid, IEnumerable<CnameMap> cnames, string error)> IsCreateTerminalRequestValid(string id, CreateTerminalRequest request)
        {
            var errors = new List<string>();

            if(!this.ModelState.IsValid)
            {
                errors.AddRange(this.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                goto fail;
            }

            if(!new Regex("^[a-z0-9]([-a-z0-9]*[a-z0-9])?$").IsMatch(request.Name))
                errors.Add("The name must match: '^[a-z0-9]([-a-z0-9]*[a-z0-9])?$'.");
            if(request.Name.StartsWith(Settings.TerminalPtyDomainNamePrefix) || request.Name.StartsWith(Settings.TerminalSshDomainNamePrefix))
                errors.Add($"The name must not begin with `{Settings.TerminalPtyDomainNamePrefix}` or `{Settings.TerminalSshDomainNamePrefix}`.");
            if(!Helpers.IsCnamesValid(request.Cnames))
                errors.Add("Provided CNAMEs could not be parsed or are invalid.");
            if(!CreateTerminalRequest.AllowedEntrypoints.Contains(request.Entrypoint))
                errors.Add($"The entrypoint value is invalid; must be: {string.Join(",", CreateTerminalRequest.AllowedEntrypoints)}.");
            if(!Helpers.IsEnvironmentVariablesValid(request.EnvironmentVariables))
                errors.Add("Provided environment variables could not be parsed.");
            var existing = await Kube.GetServicesAsync(Settings.KubeNamespace).WithName(request.Name);
            if(existing != null)
                errors.Add("A terminal with this name already exists.");

            // Clean up CNAMEs and add default ones.
            var cnames = Helpers.ResolveCnames(request.Cnames).ToList();
            // TODO: thhis would be cool, but it would require some tunnelling work.
            cnames.Insert(0, new CnameMap { Name = $"{Settings.TerminalSshDomainNamePrefix}{request.Name}.{Settings.HostName}", Port = Settings.DefaultTerminalSshPort });
            cnames.Insert(0, new CnameMap { Name = $"{Settings.TerminalPtyDomainNamePrefix}{request.Name}.{Settings.HostName}", Port = Settings.DefaultTerminalPtyPort });
            cnames.Insert(0, new CnameMap { Name = $"{request.Name}.{Settings.HostName}", Port = Settings.DefaultTerminalHttpPort });

            // Ensure no host names clash.
            var allHosts = cnames.Select(c => c.Name).ToList().AddRangeWithDaisy(await Kube.GetIngressHostsAsync(Settings.KubeIngressName, Settings.KubeNamespace));
            if(allHosts.AreAnyDuplicates())
                errors.Add("Provided CNAMEs have duplicates or clash with existing host names.");

            if(!errors.Any())
                return (true, cnames, "");

        fail:
            return (false, null, string.Join("\n", errors));
        }
    }
}
