
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Termy.Models
{
    #region Helpers

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

    #endregion

    #region REST

    public class CreateTerminalRequest
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string Image { get; set; }
        
        public string Cnames { get; set; } = "";
        public string Password { get; set; } = "null";
        public string Shell { get; set; } = "/bin/bash";
        public string Entrypoint { get; set; } = "default";
        public string EnvironmentVariables { get; set; } = "";
        public string Command { get; set; } = "";

        public static readonly IReadOnlyCollection<string> AllowedEntrypoints = new List<string> { "default", "container", "custom" }.AsReadOnly();
    }

    public class TerminalResponse
    {
        public string Name { get; set; }
        public int? Replicas { get; set; }
        public IEnumerable<CnameMap> CnameMaps { get; set;}
    }

    public class CnameMap
    {
        public string Name { get; set; }
        public int Port { get; set; }

        public override string ToString()
        {
            return $"{this.Name}:{this.Port}";
        }
    }

    public class EnvironmentVariable
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return $"{this.Name}={this.Value}";
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

    public class KillRequest
    {
        [Required]
        public string AdminPassword { get; set; }
    }

    #endregion
}