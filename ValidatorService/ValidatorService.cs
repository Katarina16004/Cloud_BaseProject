using ClassLibrary;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;

namespace ValidatorService
{
    internal sealed class ValidatorService : StatelessService, IValidatorService
    {
        public ValidatorService(StatelessServiceContext context)
            : base(context)
        {
        }

        public async Task<ValidationResultDto> ValidatePurchaseAsync(
            PurchaseRequestDto request)
        {
            var libraryProxy = ServiceProxy.Create<ILibraryService>(
                new Uri("fabric:/MiniProjekat/LibraryService"),
                new ServicePartitionKey(
                    PartitionKeys.ForBook(request.BookId)));

            var bankProxy = ServiceProxy.Create<IBankService>(
                new Uri("fabric:/MiniProjekat/BankService"),
                new ServicePartitionKey(
                    PartitionKeys.ForAccount(request.UserId)));

            Book book = await libraryProxy.BookExists(request.Name.Trim());

            if (book == null)
            {
                return new ValidationResultDto
                {
                    IsValid = false,
                    Message = "Book not found"
                };
            }

            bool hasStock = await libraryProxy.HasStock(
                request.Name,
                request.Quantity);

            if (!hasStock)
            {
                return new ValidationResultDto
                {
                    IsValid = false,
                    Message = "Not enough stock"
                };
            }

            double totalPrice = book.Price * request.Quantity;

            bool hasMoney = await bankProxy.HasEnoughMoney(
                request.UserId,
                totalPrice);

            if (!hasMoney)
            {
                return new ValidationResultDto
                {
                    IsValid = false,
                    Message = "Not enough money. Total required: " + totalPrice
                };
            }

            await bankProxy.Withdraw(request.UserId, totalPrice);
            await libraryProxy.DecreaseStock(request.Name, request.Quantity);

            var dispatcher = ServiceProxy.Create<IEventDispatcherService>(
                new Uri("fabric:/MiniProjekat/EventDispatcherService"),
                new ServicePartitionKey(0));

            request.Price = book.Price;

            await dispatcher.PublishAsync(new PurchaseEvent
            {
                Message = "Uspesna kupovina!",
                Timestamp = DateTime.UtcNow,
                ServiceSource = "ValidatorService",
                PurchaseRequest = request
            });

            return new ValidationResultDto
            {
                IsValid = true,
                Message = "Purchase successful"
            };
        }

        protected override IEnumerable<ServiceInstanceListener>
            CreateServiceInstanceListeners()
        {
            return this.CreateServiceRemotingInstanceListeners();
        }
    }
}