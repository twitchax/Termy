using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Termy.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Termy.Services
{
    public interface IKubernetesService
    {
        // Create.
        Task<Extensionsv1beta1Deployment> CreateDeploymentAsync(string ns, Extensionsv1beta1Deployment body);
        Task<V1Service> CreateServiceAsync(string ns, V1Service body);
        Task ApplyAsync(string yaml);

        // Read.
        Task<IEnumerable<V1Deployment>> GetDeploymentsAsync(string ns = null);
        Task<IEnumerable<V1Service>> GetServicesAsync(string ns = null);
        Task<IEnumerable<V1beta1Ingress>> GetIngressesAsync(string ns = null);
        Task<IEnumerable<string>> GetIngressHostsAsync(string name, string ns);
        Task<IEnumerable<V1Pod>> GetPodsAsync(string ns = null, string labelSelector = null);
        Task<IEnumerable<V1Node>> GetNodesAsync();
        Task<IEnumerable<NodeStat>> GetNodeStatsAsync();

        // Update.
        Task<V1beta1Ingress> TransformIngressAsync(string name, string ns, Action<V1beta1Ingress> transform);
        Task<V1beta1Ingress> AddIngressRuleAsync(string name, string ns, string service, IEnumerable<(string Host, int Port)> cnames);

        // Delete.
        Task<V1Status> DeleteDeploymentAsync(string name, string ns);
        Task<V1Status> DeleteServiceAsync(string name, string ns);
        Task<IEnumerable<V1Status>> DeleteAllServicesAsync(string ns);
        Task<IEnumerable<V1Status>> DeleteAllDeploymentsAsync(string ns);
    }

    public class KubernetesService : IKubernetesService
    {
        private KubernetesClientConfiguration _kubeConfig;
        private Kubernetes _kubeClient;

        public KubernetesService()
        {
            _kubeConfig = KubernetesClientConfiguration.InClusterConfig();
            _kubeClient = new Kubernetes(_kubeConfig);
        }

        #region Create

        public Task<Extensionsv1beta1Deployment> CreateDeploymentAsync(string ns, Extensionsv1beta1Deployment body)
        {
            return _kubeClient.CreateNamespacedDeployment3Async(body, ns);
        }

        public Task<V1Service> CreateServiceAsync(string ns, V1Service body)
        {
            return _kubeClient.CreateNamespacedServiceAsync(body, ns);
        }

        public Task ApplyAsync(string yaml)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Read

        public async Task<IEnumerable<V1Deployment>> GetDeploymentsAsync(string ns = null)
        {
            if(ns == null)
                return (await _kubeClient.ListDeploymentForAllNamespacesAsync()).Items;

            return (await _kubeClient.ListNamespacedDeploymentAsync(ns)).Items;
        }

        public async Task<IEnumerable<V1Service>> GetServicesAsync(string ns = null)
        {
            if(ns == null)
                return (await _kubeClient.ListServiceForAllNamespacesAsync()).Items;

            return (await _kubeClient.ListNamespacedServiceAsync(ns)).Items;
        }

        public async Task<IEnumerable<V1beta1Ingress>> GetIngressesAsync(string ns = null)
        {
            if(ns == null)
                return (await _kubeClient.ListIngressForAllNamespacesAsync()).Items;

            return (await _kubeClient.ListNamespacedIngressAsync(ns)).Items;
        }

        public async Task<IEnumerable<string>> GetIngressHostsAsync(string name, string ns)
        {
            return (await GetIngressesAsync(ns).WithName(name)).Spec.Rules.Select(r => r.Host);
        }

        public async Task<IEnumerable<V1Pod>> GetPodsAsync(string ns = null, string labelSelector = null)
        {
            if(ns == null)
                return (await _kubeClient.ListPodForAllNamespacesAsync(labelSelector: labelSelector)).Items;

            return (await _kubeClient.ListNamespacedPodAsync(ns, labelSelector: labelSelector)).Items;
        }

        public async Task<IEnumerable<V1Node>> GetNodesAsync()
        {
            return (await _kubeClient.ListNodeAsync()).Items;
        }

        public Task<IEnumerable<NodeStat>> GetNodeStatsAsync()
        {
            // TODO: Add this if node metrics are supported.
            throw new NotImplementedException();
        }

        #endregion

        #region Update

        public async Task<V1beta1Ingress> TransformIngressAsync(string name, string ns, Action<V1beta1Ingress> transform)
        {
            var ingress = (await this.GetIngressesAsync(ns).WithName(name));

            transform(ingress);

            return await _kubeClient.ReplaceNamespacedIngressAsync(ingress, name, ns);
        }

        public Task<V1beta1Ingress> AddIngressRuleAsync(string name, string ns, string service, IEnumerable<(string Host, int Port)> cnames)
        {
            return this.TransformIngressAsync(name, ns, i => {
                foreach (var cname in cnames)
                {
                    i.Spec.Rules.Add(new V1beta1IngressRule {
                        Host = cname.Host,
                        Http = new V1beta1HTTPIngressRuleValue {
                            Paths = new List<V1beta1HTTPIngressPath> {
                                new V1beta1HTTPIngressPath {
                                    Backend = new V1beta1IngressBackend {
                                        ServiceName = service,
                                        ServicePort = cname.Port
                                    }
                                }
                            }
                        }
                    });
                }
            });
        }

        #endregion

        #region Delete

        public Task<V1Status> DeleteDeploymentAsync(string name, string ns)
        {
            return _kubeClient.DeleteNamespacedDeploymentAsync(new V1DeleteOptions(), name, ns);
        }

        public Task<V1Status> DeleteServiceAsync(string name, string ns)
        {
            return _kubeClient.DeleteNamespacedServiceAsync(new V1DeleteOptions(), name, ns);
        }

        public async Task<IEnumerable<V1Status>> DeleteAllServicesAsync(string ns)
        {
            var tasks = (await this.GetServicesAsync(ns)).Select(s => DeleteServiceAsync(s.Metadata.Name, ns));

            return (await Task.WhenAll(tasks)).ToList();
        }

        public async Task<IEnumerable<V1Status>> DeleteAllDeploymentsAsync(string ns)
        {
            var tasks = (await this.GetDeploymentsAsync(ns)).Select(s => DeleteDeploymentAsync(s.Metadata.Name, ns));

            return (await Task.WhenAll(tasks)).ToList();
        }

        #endregion

        #region Helpers

        #endregion
    }

    public static class KubernetesExtensions
    {
        public static async Task<T> WithName<T>(this Task<IEnumerable<T>> apiCallTask, string name) where T : IKubernetesObject
        {
            // Would not have to use dynamic if the k8s API had proper inheritance.
            return (await apiCallTask).FirstOrDefault(o => (o as dynamic).Metadata.Name == name);
        }

        public static void SetPorts(this V1Service service, IEnumerable<CnameMap> cnames)
        {
            service.Spec.Ports = new List<V1ServicePort>();
            foreach(var cname in cnames)
            {
                service.Spec.Ports.Add(new V1ServicePort {
                    Name = cname.Name.Replace(".", "-").ToLower(),
                    Protocol = "TCP",
                    Port = cname.Port,
                    TargetPort = cname.Port
                });
            }
        }

        public static void SetPorts(this Extensionsv1beta1Deployment deployment, IEnumerable<CnameMap> cnames)
        {
            foreach(var container in deployment.Spec.Template.Spec.Containers)
            {
                container.Ports = new List<V1ContainerPort>();
                foreach(var cname in cnames)
                {
                    container.Ports.Add(new V1ContainerPort {
                        ContainerPort = cname.Port,
                    });
                }
            }
        }

        public static void SetStartup(this Extensionsv1beta1Deployment deployment, string entrypoint, string environmentVariables, string command)
        {
            foreach(var container in deployment.Spec.Template.Spec.Containers)
            {
                foreach(var envVar in Helpers.ResolveEnvironmentVariables(environmentVariables))
                    container.Env.Add(new V1EnvVar(envVar.Name, envVar.Value));

                switch(entrypoint)
                {
                    case "default":
                        container.Command = new List<string> { "/bin/bash", "-c", "--" };
                        container.Args = new List<string> { "while true; do sleep 30; done;" };
                        break;
                    case "custom":
                        container.Command = new List<string> { "/bin/bash", "-c", "--" };
                        container.Args = new List<string> { command };
                        break;
                    case "container": 
                        // Do nothing.  This defaults to the container's entrypoint.
                        break;
                    default:
                        throw new Exception("This should never happen based on validation.");
                }
            }
        }

        public static T LoadFromString<T>(this IKubernetesService service, string content) {
            // Hack fix for k8s library bug.
            content = content
                .Replace("namespace: ", "namespaceProperty: ")
                .Replace("readOnly: ", "readOnlyProperty: ");

            var deserializer =
                new DeserializerBuilder()
                .WithNamingConvention(new CamelCaseNamingConvention())
                .Build();
            var obj = deserializer.Deserialize<T>(content);
            return obj;
        }

        public static async Task<IEnumerable<V1Deployment>> GetTerminalDeploymentsAsync(this IKubernetesService service, string ns = null)
        {
            return (await service.GetDeploymentsAsync(ns)).Where(d => d.Metadata.Labels.Any(kvp => kvp.Key == Settings.KubeTerminalRunLabel));
        }

        public static async Task<IEnumerable<V1Service>> GetTerminalServicesAsync(this IKubernetesService service, string ns = null)
        {
            return (await service.GetServicesAsync(ns)).Where(d => d.Metadata.Labels.Any(kvp => kvp.Key == Settings.KubeTerminalRunLabel));
        }

        public static async Task<IEnumerable<V1Status>> DeleteAllTerminalServicesAsync(this IKubernetesService service, string ns)
        {
            var tasks = (await service.GetTerminalServicesAsync(ns)).Select(s => service.DeleteServiceAsync(s.Metadata.Name, ns));

            return (await Task.WhenAll(tasks)).ToList();
        }

        public static async Task<IEnumerable<V1Status>> DeleteAllTerminalDeploymentsAsync(this IKubernetesService service, string ns)
        {
            var tasks = (await service.GetTerminalDeploymentsAsync(ns)).Select(s => service.DeleteDeploymentAsync(s.Metadata.Name, ns));

            return (await Task.WhenAll(tasks)).ToList();
        }
    }
}