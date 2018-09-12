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
    public class BaseController : Controller
    {
        public bool IsSuperUser => this.Request.Headers.TryGetValue("X-Super-User-Password", out var values) && values.Contains(Settings.SuperUserPassword);
    }
}