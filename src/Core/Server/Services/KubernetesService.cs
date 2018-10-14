

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
        private string _configPath;
        private KubernetesClientConfiguration _kubeConfig;
        private Kubernetes _kubeClient;

        public KubernetesService(string configPath)
        {
            _configPath = configPath;
            _kubeConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile(_configPath);
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

        public async Task ApplyAsync(string yaml)
        {
            var fileName = $"deployment_{Guid.NewGuid()}.yml";

            try
            {
                await File.WriteAllTextAsync(fileName, yaml);
                await this.RunKubeCommand("STARTUP", $"apply -f {fileName}");
            }
            finally
            {
                File.Delete(fileName);
            }
            
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

        public async Task<IEnumerable<NodeStat>> GetNodeStatsAsync()
        {
            var (nodes, _) = await this.RunKubeCommand("NODE ACTIVITY WORKER", "top node");

            var nodeStats = Helpers.TextToJArray(nodes).Select(n => new NodeStat {
                Name = n.Value<string>("name"),
                CpuCores = int.Parse(n.Value<string>("cpucores").Replace("m", "")),
                CpuPercent = int.Parse(n.Value<string>("cpu").Replace("%", "")),
                MemoryBytes = int.Parse(n.Value<string>("memorybytes").Replace("mi", "")),
                MemoryPercent = int.Parse(n.Value<string>("memory").Replace("%", "")), 
                Time = DateTime.Now
            }).ToList();

            return nodeStats;
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

        private Task<(string Standard, string Error)> RunKubeCommand(string id, string args)
        {
            return Helpers.RunCommand("KUBERNETES", "kubectl", $"{args} --kubeconfig=\"{_configPath}\"");
        }

        #endregion
    }

    public static class KubernetesExtensions
    {
        public static async Task<T> WithName<T>(this Task<IEnumerable<T>> apiCallTask, string name) where T : IKubernetesObject
        {
            // Would not have to use dynamic if the k8s API had proper inheritance.
            return (await apiCallTask).FirstOrDefault(o => (o as dynamic).Metadata.Name == name);
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
    }
}