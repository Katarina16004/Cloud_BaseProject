using ClassLibrary;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;

namespace BankService
{
    internal sealed class BankService : StatefulService, IBankService
    {
        private const string DictionaryName = "accounts";
        private const string ChargesDictionaryName = "charges";

        public BankService(StatefulServiceContext context)
            : base(context)
        {
        }

        private async Task<IReliableDictionary<string, Customer>> GetDictionary()
        {
            return await StateManager.GetOrAddAsync
                <IReliableDictionary<string, Customer>>(DictionaryName);
        }
        private async Task<IReliableDictionary<Guid, Charge>>  GetChargesDictionary()
        {
            return await StateManager.GetOrAddAsync<
                IReliableDictionary<Guid, Charge>>(
                    ChargesDictionaryName);
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(
                        1000,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "BS RUNASYNC ERROR:");

                Console.WriteLine(
                    ex.ToString());

                throw;
            }
        }
        public async Task ChargeAsync(
    Guid txId,
    string userId,
    double amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentException(
                    "Amount must be greater than zero.",
                    "amount");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "User ID is required.",
                    "userId");
            }

            var accounts =
                await GetDictionary();

            var charges =
                await GetChargesDictionary();

            using (var tx =
                StateManager.CreateTransaction())
            {
                var existingCharge =
                    await charges.TryGetValueAsync(
                        tx,
                        txId,
                        LockMode.Update);

                if (existingCharge.HasValue)
                {
                    return;
                }

                var accountResult =
                    await accounts.TryGetValueAsync(
                        tx,
                        userId,
                        LockMode.Update);

                if (!accountResult.HasValue)
                {
                    throw new InvalidOperationException(
                        "Account not found: " + userId);
                }

                var account =
                    accountResult.Value;

                if (account.Balance < amount)
                {
                    throw new InvalidOperationException(
                        "Not enough money.");
                }

                var updatedAccount =
                    new Customer(
                        account.CustomerId,
                        account.Account,
                        account.Balance - amount);

                await accounts.SetAsync(
                    tx,
                    userId,
                    updatedAccount);

                await charges.AddAsync(
                    tx,
                    txId,
                    new Charge(
                        txId,
                        userId,
                        amount,
                        ChargeStatus.Charged));

                await tx.CommitAsync();
            }
        }
        public async Task RefundAsync(
    Guid txId,
    string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "User ID is required.",
                    "userId");
            }

            var accounts =
                await GetDictionary();

            var charges =
                await GetChargesDictionary();

            using (var tx =
                StateManager.CreateTransaction())
            {
                var chargeResult =
                    await charges.TryGetValueAsync(
                        tx,
                        txId,
                        LockMode.Update);

                if (!chargeResult.HasValue)
                {
                    await charges.AddAsync(
                        tx,
                        txId,
                        new Charge(
                            txId,
                            userId,
                            0,
                            ChargeStatus.Refunded));

                    await tx.CommitAsync();
                    return;
                }

                var charge =
                    chargeResult.Value;

                if (charge.Status ==
                    ChargeStatus.Refunded)
                {
                    return;
                }

                var accountResult =
                    await accounts.TryGetValueAsync(
                        tx,
                        userId,
                        LockMode.Update);

                if (!accountResult.HasValue)
                {
                    throw new InvalidOperationException(
                        "Account not found: " + userId);
                }

                var account =
                    accountResult.Value;

                var updatedAccount =
                    new Customer(
                        account.CustomerId,
                        account.Account,
                        account.Balance + charge.Amount);

                await accounts.SetAsync(
                    tx,
                    userId,
                    updatedAccount);

                await charges.SetAsync(
                    tx,
                    txId,
                    charge.WithStatus(
                        ChargeStatus.Refunded));

                await tx.CommitAsync();
            }
        }

        public async Task<bool> HasEnoughMoney(
            string userId,
            double amount)
        {
            var dict = await GetDictionary();

            using (var tx = StateManager.CreateTransaction())
            {
                var result = await dict.TryGetValueAsync(tx, userId);

                return result.HasValue &&
                       result.Value.Balance >= amount;
            }
        }

        public async Task Withdraw(
            string userId,
            double amount)
        {
            var dict = await GetDictionary();

            using (var tx = StateManager.CreateTransaction())
            {
                var result = await dict.TryGetValueAsync(tx, userId);

                if (result.HasValue)
                {
                    var customer = result.Value;
                    customer.Balance -= amount;

                    await dict.SetAsync(tx, userId, customer);
                    await tx.CommitAsync();
                }
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

                while (await enumerator.MoveNextAsync(
                    CancellationToken.None))
                {
                    list.Add(enumerator.Current.Value);
                }
            }

            return list;
        }

        public async Task<long> GetCountAsync()
        {
            var dict = await GetDictionary();

            using (var tx = StateManager.CreateTransaction())
            {
                return await dict.GetCountAsync(tx);
            }
        }

        public async Task AddCustomerAsync(Customer customer)
        {
            if (customer == null ||
                string.IsNullOrWhiteSpace(customer.Account))
            {
                throw new ArgumentException(
                    "Customer account is required.");
            }

            var dict = await GetDictionary();

            using (var tx = StateManager.CreateTransaction())
            {
                var existing = await dict.TryGetValueAsync(
                    tx,
                    customer.Account);

                if (!existing.HasValue)
                {
                    await dict.AddAsync(
                        tx,
                        customer.Account,
                        customer);

                    await tx.CommitAsync();
                }
            }
        }

        protected override IEnumerable<ServiceReplicaListener>
            CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }
    }
}