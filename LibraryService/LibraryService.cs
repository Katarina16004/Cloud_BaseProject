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
using ClassLibrary;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;

namespace LibraryService
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance.
    /// </summary>
    internal sealed class LibraryService : StatefulService, ILibraryService
    {
        private const string DictionaryName = "books";

        public LibraryService(StatefulServiceContext context)
            : base(context) { }

        private async Task<IReliableDictionary<string, Book>> GetDict()
        {
            return await StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>(DictionaryName);
        }

        private async Task AddDataAsync()
        {
            var dict = await GetDict();

            using (var tx = StateManager.CreateTransaction())
            {
                // Proveravamo da li knjiga već postoji pre dodavanja da ne bi došlo do izuzetka
                var book1 = await dict.TryGetValueAsync(tx, "Orkanski visovi");
                if (!book1.HasValue)
                {
                    await dict.AddAsync(tx, "Orkanski visovi", new Book(1, "Emili Bronte", "Orkanski visovi", 400, 2));
                }

                var book2 = await dict.TryGetValueAsync(tx, "Zlocin i kazna");
                if (!book2.HasValue)
                {
                    await dict.AddAsync(tx, "Zlocin i kazna", new Book(2, "Fjodor Dostojevski", "Zlocin i kazna", 150, 5));
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
                Console.WriteLine("LS RUNASYNC ERROR:");
                Console.WriteLine(ex.ToString());

                throw;
            }
        }

        public async Task<Book> BookExists(string bookName)
        {
            var dict = await GetDict();
            using var tx = StateManager.CreateTransaction();

            var result = await dict.TryGetValueAsync(tx, bookName);
            return result.Value;
        }

        public async Task<bool> HasStock(string bookName, int qty)
        {
            var dict = await GetDict();
            using var tx = StateManager.CreateTransaction();

            var result = await dict.TryGetValueAsync(tx, bookName);
            return result.HasValue && result.Value.Quantity >= qty;
        }

        public async Task DecreaseStock(string bookName, int qty)
        {
            var dict = await GetDict();
            using var tx = StateManager.CreateTransaction();

            var result = await dict.TryGetValueAsync(tx, bookName);

            if (result.HasValue)
            {
                var book = result.Value;
                book.Quantity -= qty;
                await dict.SetAsync(tx, bookName, book);
                await tx.CommitAsync();
            }
        }

        public async Task<List<Book>> GetAllBooksAsync()
        {
            var dict = await GetDict();
            var list = new List<Book>();

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

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }
    }
}