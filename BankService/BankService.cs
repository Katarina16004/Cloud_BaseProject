using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using ClassLibrary;

namespace BankService
{
    internal sealed class BankService : StatefulService, IBankService
    {
        private const string DictionaryName = "accounts";

        public BankService(StatefulServiceContext context)
            : base(context) { }

        private async Task<IReliableDictionary<string, Customer>> GetDictionary()
        {
            return await StateManager.GetOrAddAsync<IReliableDictionary<string, Customer>>(DictionaryName);
        }

        private async Task AddDataAsync()
        {
            var dict = await GetDictionary();

            using (var tx = StateManager.CreateTransaction())
            {
                var cust1 = await dict.TryGetValueAsync(tx, "1");
                if (!cust1.HasValue)
                {
                    await dict.AddAsync(tx, "1", new Customer(1, "veljkovickatarina56@gmail.com", 600));
                }

                var cust2 = await dict.TryGetValueAsync(tx, "2");
                if (!cust2.HasValue)
                {
                    await dict.AddAsync(tx, "2", new Customer(2, "veljkovic.pr33.2022@uns.ac.rs", 500));
                }

                await tx.CommitAsync();
            }
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await AddDataAsync();

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("BS RUNASYNC ERROR:");
                Console.WriteLine(ex.ToString());
                throw;
            }
        }

        public async Task<bool> HasEnoughMoney(string userId, double amount)
        {
            var dict = await GetDictionary();
            using var tx = StateManager.CreateTransaction();
            var result = await dict.TryGetValueAsync(tx, userId);

            return result.HasValue && result.Value.Balance >= amount;
        }

        public async Task Withdraw(string userId, double amount)
        {
            var dict = await GetDictionary();
            using var tx = StateManager.CreateTransaction();
            var result = await dict.TryGetValueAsync(tx, userId);

            if (result.HasValue)
            {
                var customer = result.Value;
                customer.Balance -= amount;
                await dict.SetAsync(tx, userId, customer);
                await tx.CommitAsync();
            }
        }

        public async Task<List<Customer>> GetAllCustomersAsync()
        {
            var dict = await GetDictionary();
            var list = new List<Customer>();

            using (var tx = StateManager.CreateTransaction())
            {
                var enumerable = await dict.CreateEnumerableAsync(tx);
                var enumerator = enumerable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(CancellationToken.None))
                {
                    list.Add(enumerator.Current.Value);
                }
            }

            return list;
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }
    }
}