using ClassLibrary;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;

namespace ReportService
{
    internal sealed class ReportService :
        StatefulService,
        IReportService
    {
        private const string SalesDictionaryName =
            "sales";

        public ReportService(
            StatefulServiceContext context)
            : base(context)
        {
        }

        private async Task<
            IReliableDictionary<Guid, SaleConfirmed>>
            GetSalesAsync()
        {
            return await StateManager.GetOrAddAsync<
                IReliableDictionary<Guid, SaleConfirmed>>(
                    SalesDictionaryName);
        }

        public async Task RecordSaleAsync(
            SaleConfirmed sale)
        {
            if (sale == null)
            {
                throw new ArgumentNullException(
                    "sale");
            }

            var sales =
                await GetSalesAsync();

            using (var tx =
                StateManager.CreateTransaction())
            {
                var existing =
                    await sales.TryGetValueAsync(
                        tx,
                        sale.PurchaseId,
                        LockMode.Update);

                if (existing.HasValue)
                {
                    return;
                }

                await sales.AddAsync(
                    tx,
                    sale.PurchaseId,
                    sale);

                await tx.CommitAsync();
            }
        }

        public async Task<long> GetSalesCountAsync()
        {
            var sales =
                await GetSalesAsync();

            using (var tx =
                StateManager.CreateTransaction())
            {
                return await sales.GetCountAsync(tx);
            }
        }

        protected override
            IEnumerable<ServiceReplicaListener>
            CreateServiceReplicaListeners()
        {
            return this
                .CreateServiceRemotingReplicaListeners();
        }
    }
}