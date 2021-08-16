using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.Web.Configuration;
using Microsoft.eShopWeb.Web.Controllers.Api;

namespace Microsoft.eShopWeb.Web.Controllers
{
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly TestSettings _testSettings;

        public TestController(TestSettings testSettings)
        {
            _testSettings = testSettings;
        }

        [Route("app-instance")]
        public IActionResult GetInstanceName()
        {
            return Ok(_testSettings);
        }

        [Route("start-load")]
        public IActionResult RunTask()
        {
            for (var n = 0; n < 10; n++)
            {
                new Thread(() =>
                {
                    double result = 0;
                    for (var i = 0; i < 10000000000; i++)
                    {
                        result += Math.Exp(i);
                    }
                }).Start();
            }

            return Ok($"Started 10 new threads: {DateTime.Now:F}");
        }
    }
}
