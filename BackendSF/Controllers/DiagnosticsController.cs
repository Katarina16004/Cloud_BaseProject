using ClassLibrary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;

namespace BackendSF.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnosticsController : ControllerBase
    {
        private readonly FabricClient _fabricClient =
            new FabricClient();

        [HttpGet("partitions")]
        public async Task<IActionResult> GetPartitions()
        {
            var report = new List<object>();

            var libraryUri =
                new Uri("fabric:/MiniProjekat/LibraryService");

            var libraryPartitions =
                await _fabricClient.QueryManager
                    .GetPartitionListAsync(libraryUri);

            foreach (var partition in libraryPartitions)
            {
                var information =
                    (Int64RangePartitionInformation)
                    partition.PartitionInformation;

                var proxy =
                    ServiceProxy.Create<ILibraryService>(
                        libraryUri,
                        new ServicePartitionKey(
                            information.LowKey));

                var count =
                    await proxy.GetCountAsync();

                report.Add(new
                {
                    Service = "LibraryService",
                    PartitionId =
                        partition.PartitionInformation.Id,
                    LowKey = information.LowKey,
                    RecordCount = count
                });
            }

            var bankUri =
                new Uri("fabric:/MiniProjekat/BankService");

            var bankPartitions =
                await _fabricClient.QueryManager
                    .GetPartitionListAsync(bankUri);

            foreach (var partition in bankPartitions)
            {
                var information =
                    (Int64RangePartitionInformation)
                    partition.PartitionInformation;

                var proxy =
                    ServiceProxy.Create<IBankService>(
                        bankUri,
                        new ServicePartitionKey(
                            information.LowKey));

                var count =
                    await proxy.GetCountAsync();

                report.Add(new
                {
                    Service = "BankService",
                    PartitionId =
                        partition.PartitionInformation.Id,
                    LowKey = information.LowKey,
                    RecordCount = count
                });
            }

            return Ok(report);
        }
    }
}