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
    internal sealed class ValidatorService :
        StatelessService,
        IValidatorService
    {
        private static readonly Uri LibraryServiceUri =
            new Uri("fabric:/MiniProjekat/LibraryService");

        private static readonly Uri BankServiceUri =
            new Uri("fabric:/MiniProjekat/BankService");

        private static readonly Uri PurchaseOrchestratorUri =
            new Uri("fabric:/MiniProjekat/PurchaseOrchestrator");

        public ValidatorService(
            StatelessServiceContext context)
            : base(context)
        {
        }

        public async Task<ValidationResultDto>
            ValidatePurchaseAsync(
                PurchaseRequestDto request)
        {
            if (request == null)
            {
                return Invalid("Request is required.");
            }

            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return Invalid("UserId is required.");
            }

            if (request.BookId <= 0)
            {
                return Invalid("BookId must be positive.");
            }

            if (request.Quantity <= 0)
            {
                return Invalid(
                    "Quantity must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Invalid("Book name is required.");
            }

            var libraryProxy =
                ServiceProxy.Create<ILibraryService>(
                    LibraryServiceUri,
                    new ServicePartitionKey(
                        PartitionKeys.ForBook(
                            request.BookId)));

            var bankProxy =
                ServiceProxy.Create<IBankService>(
                    BankServiceUri,
                    new ServicePartitionKey(
                        PartitionKeys.ForAccount(
                            request.UserId)));

            var book =
                await libraryProxy.BookExists(
                    request.Name.Trim());

            if (book == null)
            {
                return Invalid("Book not found.");
            }

            if (book.BookId != request.BookId)
            {
                return Invalid(
                    "Book ID does not match book name.");
            }

            var hasStock =
                await libraryProxy.HasStock(
                    request.Name.Trim(),
                    request.Quantity);

            if (!hasStock)
            {
                return Invalid("Not enough stock.");
            }

            var totalPrice =
                book.Price * request.Quantity;

            var hasMoney =
                await bankProxy.HasEnoughMoney(
                    request.UserId,
                    totalPrice);

            if (!hasMoney)
            {
                return Invalid(
                    "Not enough money. Total required: " +
                    totalPrice);
            }

            /*
             * Validator više ne poziva Withdraw, DecreaseStock
             * niti EventDispatcherService.
             *
             * Umesto toga pokreće trajnu sagu.
             */
            var purchaseId = Guid.NewGuid();

            var orchestratorProxy =
                ServiceProxy.Create<
                    IPurchaseOrchestratorService>(
                        PurchaseOrchestratorUri,
                        new ServicePartitionKey(
                            PartitionKeyHelper
                                .ForPurchase(purchaseId)));

            await orchestratorProxy.StartPurchaseAsync(
                purchaseId,
                request.UserId,
                request.BookId,
                request.Quantity);

            request.Price = book.Price;

            return new ValidationResultDto
            {
                IsValid = true,
                Message =
                    "Purchase saga started. PurchaseId: " +
                    purchaseId
            };
        }

        private static ValidationResultDto Invalid(
            string message)
        {
            return new ValidationResultDto
            {
                IsValid = false,
                Message = message
            };
        }

        protected override
            IEnumerable<ServiceInstanceListener>
            CreateServiceInstanceListeners()
        {
            return this
                .CreateServiceRemotingInstanceListeners();
        }
    }
}