using ClassLibrary;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System.Fabric;

namespace ValidatorService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnosticsController : ControllerBase
    {
        private readonly FabricClient _fabricClient = new FabricClient();

        [HttpGet("partitions")]
        public async Task<IActionResult> GetPartitionsDiagnostics()
        {
            var libraryUri = new Uri("fabric:/MiniProjekat/LibraryService");
            var bankUri = new Uri("fabric:/MiniProjekat/BankService");

            var report = new List<object>();

            // 1. Provera za LibraryService
            var libraryPartitions = await _fabricClient.QueryManager.GetPartitionListAsync(libraryUri);
            foreach (var p in libraryPartitions)
            {
                long lowKey = ((Int64RangePartitionInformation)p.PartitionInformation).LowKey;
                var proxy = ServiceProxy.Create<ILibraryService>(libraryUri, new ServicePartitionKey(lowKey));

                long count = await proxy.GetCountAsync();

                report.Add(new
                {
                    Service = "LibraryService",
                    PartitionId = p.PartitionInformation.Id,
                    LowKey = lowKey,
                    RecordCount = count
                });
            }

            // 2. Provera za BankService
            var bankPartitions = await _fabricClient.QueryManager.GetPartitionListAsync(bankUri);
            foreach (var p in bankPartitions)
            {
                long lowKey = ((Int64RangePartitionInformation)p.PartitionInformation).LowKey;
                var proxy = ServiceProxy.Create<IBankService>(bankUri, new ServicePartitionKey(lowKey));

                long count = await proxy.GetCountAsync();

                report.Add(new
                {
                    Service = "BankService",
                    PartitionId = p.PartitionInformation.Id,
                    LowKey = lowKey,
                    RecordCount = count
                });
            }

            return Ok(report);
        }
    }
}
