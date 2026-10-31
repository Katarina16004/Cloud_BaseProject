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
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Client;

namespace ValidatorService
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance.
    /// </summary>
    internal sealed class ValidatorService : StatelessService, IValidatorService
    {
        public ValidatorService(StatelessServiceContext context)
            : base(context)
        { }

        /*
        public Task<ValidationResultDto> ValidatePurchaseAsync(PurchaseRequestDto request)
        { 
            if (request == null)
                return Task.FromResult(new ValidationResultDto(false));

            if (request.Quantity <= 0 || request.Quantity > 100 ||
                string.IsNullOrWhiteSpace(request.Account) ||
                string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Author) ||
                string.IsNullOrWhiteSpace(request.UserId) ||
                string.IsNullOrWhiteSpace(request.BookId))
            {
                return Task.FromResult(new ValidationResultDto(false));
            }

            return Task.FromResult(new ValidationResultDto(true));
        }
        */

        public async Task<ValidationResultDto> ValidatePurchaseAsync(PurchaseRequestDto request)
        {
            var bankProxy = ServiceProxy.Create<IBankService>(
                new Uri("fabric:/MiniProjekat/BankService"),
                new ServicePartitionKey(0));

            var libraryProxy = ServiceProxy.Create<ILibraryService>(
                new Uri("fabric:/MiniProjekat/LibraryService"),
                new ServicePartitionKey(0));

            // Prvo se proverava dostupnost knjige kako bismo dobili informaciju o ceni
            Book book = await libraryProxy.BookExists(request.Name.Trim());
            if (book == null)
                return new ValidationResultDto { IsValid = false, Message = "Book not found" };

            bool stock = await libraryProxy.HasStock(request.Name, request.Quantity);
            if (!stock)
                return new ValidationResultDto { IsValid = false, Message = "Not enough stock" };

            // Računamo ukupnu cenu (cena * količina) i proveravamo da li korisnik ima dovoljno novca
            double totalPrice = book.Price * request.Quantity;
            bool hasMoney = await bankProxy.HasEnoughMoney(request.UserId, totalPrice);
            if (!hasMoney)
                return new ValidationResultDto { IsValid = false, Message = "Not enough money. Total required: " + totalPrice };

            // Pa ako je sve ok, tek onda se skida novac sa računa (ukupna cena) i smanjuje zaliha knjiga
            await bankProxy.Withdraw(request.UserId, totalPrice);
            await libraryProxy.DecreaseStock(request.Name, request.Quantity);

            // Sad šaljemo događaj dispatcheru
            var dispatcher = ServiceProxy.Create<IEventDispatcherService>(
                new Uri("fabric:/MiniProjekat/EventDispatcherService"),
                new ServicePartitionKey(0));

            request.Price = book.Price;

            var evt = new PurchaseEvent
            {
                Message = "Uspesna kupovina!",
                Timestamp = DateTime.UtcNow,
                ServiceSource = "ValidatorService",
                PurchaseRequest = request,
            };

            await dispatcher.PublishAsync(evt);

            return new ValidationResultDto { IsValid = true, Message = "Purchase successful" };
        }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return this.CreateServiceRemotingInstanceListeners();
        }
    }
}
