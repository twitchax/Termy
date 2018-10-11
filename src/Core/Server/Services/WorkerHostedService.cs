

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Termy.Models;

namespace Termy.Services
{
    public class WorkerHostedService : IHostedService, IDisposable
    {
        private IKubernetesService _kube;
        private INodeStats _nodeStats;
        private Timer _timer;

        public WorkerHostedService(IKubernetesService kube, INodeStats nodeStats)
        {
            _kube = kube;
            _nodeStats = nodeStats;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(Settings.WorkerUpdateIntervalInSeconds));

            return Task.CompletedTask;
        }

        private async void DoWork(object state)
        {
            try
            {
                var nodeStats = await _kube.GetNodeStatsAsync();

                foreach(var node in nodeStats)
                {
                    if(!_nodeStats.ContainsKey(node.Name))
                        _nodeStats.Add(node.Name, new FixedSizedQueue<NodeStat>(Settings.WorkerQueueLength));

                    _nodeStats[node.Name].Enqueue(node);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine($" [NODE ACTIVITY WORKER] Failed: {e.Message}.\n\n{e.StackTrace}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}