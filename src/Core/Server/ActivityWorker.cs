using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Termy
{
    public static class ActivityWorker
    {
        private static bool _started = false;
        private static int _updateIntervalInSeconds = 10;
        private static int _queueLength = 2 /* hours */ * 60 /* minutes/hr */ * 60 /* seconds/minute */ / _updateIntervalInSeconds;
        
        private static Dictionary<string, FixedSizedQueue<NodeStat>> _nodeStats = new Dictionary<string, FixedSizedQueue<NodeStat>>();
        public static Dictionary<string, FixedSizedQueue<NodeStat>> NodeStats => _nodeStats;

        public static async void Start()
        {
            if(ActivityWorker._started)
                return;
            
            ActivityWorker._started = true;

            while(true)
            {
                try
                {
                    var (nodes, _) = await Helpers.RunKubeCommand("ACTIVITY WORKER", "top node");

                    var nodeStats = Helpers.TextToJArray(nodes).Select(n => new NodeStat {
                        Name = n.Value<string>("name"),
                        CpuCores = int.Parse(n.Value<string>("cpucores").Replace("m", "")),
                        CpuPercent = int.Parse(n.Value<string>("cpu").Replace("%", "")),
                        MemoryBytes = int.Parse(n.Value<string>("memorybytes").Replace("mi", "")),
                        MemoryPercent = int.Parse(n.Value<string>("memory").Replace("%", "")), 
                        Time = DateTime.Now
                    });

                    foreach(var node in nodeStats)
                    {
                        if(!_nodeStats.ContainsKey(node.Name))
                            _nodeStats.Add(node.Name, new FixedSizedQueue<NodeStat>(ActivityWorker._queueLength));

                        _nodeStats[node.Name].Enqueue(node);

                        Console.WriteLine($" [ACTIVITY WORKER] {node.Name}.");
                        Console.WriteLine($"    CpuCores: {node.CpuCores}.");
                        Console.WriteLine($"    CpuPercent: {node.CpuPercent}.");
                        Console.WriteLine($"    MemoryBytes: {node.MemoryBytes}.");
                        Console.WriteLine($"    MemoryPercent: {node.MemoryPercent}.");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(_updateIntervalInSeconds));
                }
                catch(Exception e)
                {
                    Console.WriteLine($" [ACTIVITY WORKER] Failed: {e.Message}.\n\n{e.StackTrace}");
                }
            }
        }
    }

    public class FixedSizedQueue<T> : Queue<T>
    {
        public int Limit { get; set; }

        public FixedSizedQueue(int limit)
        {
            this.Limit = limit;
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            
            if(base.Count > this.Limit)
                base.Dequeue();
        }
    }

    public class NodeStat
    {
        public string Name { get; set; }
        public int CpuCores { get; set; }
        public int CpuPercent { get; set; }
        public int MemoryBytes { get; set; }
        public int MemoryPercent { get; set; }
        
        public DateTime Time { get; set; }
    }
}