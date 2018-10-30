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
using Termy.Models;
using Termy.Services;

namespace Termy.Controllers
{
    public class BaseController : Controller
    {
        protected IKubernetesService Kube { get; private set;}
        protected INodeStats NodeStats { get; private set; }

        public bool IsSuperUser => this.Request.Headers.TryGetValue("X-Super-User-Password", out var values) && values.Contains(Settings.SuperUserPassword);

        public BaseController(IKubernetesService kube, INodeStats nodeStats) 
        {
            Kube = kube;
            NodeStats = nodeStats;
        }

        public BadRequestObjectResult BadRequest(string id, string error)
        {
            Helpers.Log(id, error);
            return base.BadRequest(error);
        }
    }
}