using ClassLibrary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System;
using System.Threading.Tasks;

namespace BackendSF.Controllers
{
    [Route("api/reports")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private static readonly Uri ReportServiceUri =
            new Uri(
                "fabric:/MiniProjekat/ReportService");

        [HttpGet("sales/count")]
        public async Task<IActionResult> GetSalesCount()
        {
            try
            {
                var proxy =
     ServiceProxy.Create<IReportService>(
         ReportServiceUri,
         new ServicePartitionKey(0));

                var count =
                    await proxy.GetSalesCountAsync();

                return Ok(new
                {
                    SalesCount = count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    ex.Message);
            }
        }
    }
}