using ClassLibrary;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;

namespace PurchaseOrchestrator
{
    internal sealed class PurchaseOrchestrator :
        StatefulService,
        IPurchaseOrchestratorService
    {
        private const string PurchasesDictionaryName =
            "purchases";

        private const string CancelRequestsDictionaryName =
            "cancelRequests";

        private static readonly Uri LibraryServiceUri =
            new Uri(
                "fabric:/MiniProjekat/LibraryService");

        private static readonly Uri BankServiceUri =
            new Uri(
                "fabric:/MiniProjekat/BankService");

        public PurchaseOrchestrator(
            StatefulServiceContext context)
            : base(context)
        {
        }

        private async Task<
            IReliableDictionary<Guid, PurchaseSaga>>
            GetPurchasesAsync()
        {
            return await StateManager.GetOrAddAsync<
                IReliableDictionary<Guid, PurchaseSaga>>(
                    PurchasesDictionaryName);
        }

        private async Task<
            IReliableDictionary<Guid, bool>>
            GetCancelRequestsAsync()
        {
            return await StateManager.GetOrAddAsync<
                IReliableDictionary<Guid, bool>>(
                    CancelRequestsDictionaryName);
        }

        private ILibraryService GetLibraryProxy(
            int bookId)
        {
            return ServiceProxy.Create<ILibraryService>(
                LibraryServiceUri,
                new ServicePartitionKey(
                    PartitionKeys.ForBook(bookId)));
        }

        private IBankService GetBankProxy(
            string userId)
        {
            return ServiceProxy.Create<IBankService>(
                BankServiceUri,
                new ServicePartitionKey(
                    PartitionKeys.ForAccount(userId)));
        }

        public async Task<Guid> StartPurchaseAsync(
            Guid purchaseId,
            string userId,
            int bookId,
            int quantity)
        {
            if (purchaseId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Purchase ID is required.",
                    "purchaseId");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "User ID is required.",
                    "userId");
            }

            if (bookId <= 0)
            {
                throw new ArgumentException(
                    "Book ID must be positive.",
                    "bookId");
            }

            if (quantity <= 0)
            {
                throw new ArgumentException(
                    "Quantity must be greater than zero.",
                    "quantity");
            }

            var libraryProxy =
                GetLibraryProxy(bookId);

            var books =
                await libraryProxy.GetAllBooksAsync();

            var book =
                books.Find(
                    item => item.BookId == bookId);

            if (book == null)
            {
                throw new InvalidOperationException(
                    "Book not found: " + bookId);
            }

            var totalPrice =
                book.Price * quantity;

            var purchases =
                await GetPurchasesAsync();

            using (var tx =
                StateManager.CreateTransaction())
            {
                var existing =
                    await purchases.TryGetValueAsync(
                        tx,
                        purchaseId,
                        LockMode.Update);

                if (existing.HasValue)
                {
                    var existingPurchase =
                        existing.Value;

                    if (existingPurchase.UserId != userId ||
                        existingPurchase.BookId != bookId ||
                        existingPurchase.Quantity != quantity)
                    {
                        throw new InvalidOperationException(
                            "Purchase ID is already used " +
                            "for different data.");
                    }

                    return purchaseId;
                }

                var saga =
                    new PurchaseSaga(
                        purchaseId,
                        userId,
                        bookId,
                        book.Name,
                        quantity,
                        totalPrice);

                await purchases.AddAsync(
                    tx,
                    purchaseId,
                    saga);

                await tx.CommitAsync();

                return purchaseId;
            }
        }

        public async Task<PurchaseSaga> GetPurchaseAsync(
            Guid purchaseId)
        {
            var purchases =
                await GetPurchasesAsync();

            using (var tx =
                StateManager.CreateTransaction())
            {
                var result =
                    await purchases.TryGetValueAsync(
                        tx,
                        purchaseId);

                if (!result.HasValue)
                {
                    return null;
                }

                return result.Value;
            }
        }

        public async Task CancelPurchaseAsync(
            Guid purchaseId)
        {
            var purchases =
                await GetPurchasesAsync();

            var cancelRequests =
                await GetCancelRequestsAsync();

            using (var tx =
                StateManager.CreateTransaction())
            {
                var purchaseResult =
                    await purchases.TryGetValueAsync(
                        tx,
                        purchaseId);

                if (!purchaseResult.HasValue)
                {
                    throw new InvalidOperationException(
                        "Purchase not found.");
                }

                var purchase =
                    purchaseResult.Value;

                if (purchase.PastPivot)
                {
                    throw new InvalidOperationException(
                        "Purchase cannot be cancelled " +
                        "after the pivot.");
                }

                var existingRequest =
                    await cancelRequests.TryGetValueAsync(
                        tx,
                        purchaseId);

                if (!existingRequest.HasValue)
                {
                    await cancelRequests.AddAsync(
                        tx,
                        purchaseId,
                        true);

                    await tx.CommitAsync();
                }
            }
        }

        private async Task<bool>
            IsCancelRequestedAsync(
                Guid purchaseId)
        {
            var cancelRequests =
                await GetCancelRequestsAsync();

            using (var tx =
                StateManager.CreateTransaction())
            {
                var result =
                    await cancelRequests.TryGetValueAsync(
                        tx,
                        purchaseId);

                return result.HasValue &&
                       result.Value;
            }
        }

        private async Task<List<PurchaseSaga>>
            LoadDueAsync(
                CancellationToken cancellationToken)
        {
            var purchases =
                await GetPurchasesAsync();

            var due =
                new List<PurchaseSaga>();

            using (var tx =
                StateManager.CreateTransaction())
            {
                var enumerable =
                    await purchases.CreateEnumerableAsync(tx);

                var enumerator =
                    enumerable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(
                    cancellationToken))
                {
                    var saga =
                        enumerator.Current.Value;

                    if (!saga.IsTerminal &&
                        saga.NextAttemptUtc <=
                        DateTime.UtcNow)
                    {
                        due.Add(saga);
                    }
                }
            }

            return due;
        }

        private async Task AdvanceAsync(
            PurchaseSaga saga,
            CancellationToken cancellationToken)
        {
            PurchaseSaga nextSaga;

            try
            {
                var cancelRequested =
                    await IsCancelRequestedAsync(
                        saga.PurchaseId);

                if (!saga.PastPivot &&
                    saga.Status !=
                    PurchaseStatus.Compensating &&
                    cancelRequested)
                {
                    nextSaga =
                        saga.StartCompensation(
                            "Purchase cancelled by client.");
                }
                else
                {
                    switch (saga.Status)
                    {
                        case PurchaseStatus.Started:
                            {
                                var library =
                                    GetLibraryProxy(
                                        saga.BookId);

                                var unitPrice =
                                    await library.ReserveAsync(
                                        saga.PurchaseId,
                                        saga.BookId,
                                        saga.BookName,
                                        saga.Quantity);

                                var totalPrice =
                                    unitPrice *
                                    saga.Quantity;

                                nextSaga =
                                    saga.WithStatus(
                                        PurchaseStatus.BookReserved,
                                        totalPrice);

                                break;
                            }

                        case PurchaseStatus.BookReserved:
                            {
                                var bank =
                                    GetBankProxy(
                                        saga.UserId);

                                await bank.ChargeAsync(
                                    saga.PurchaseId,
                                    saga.UserId,
                                    saga.Price);

                                nextSaga =
                                    saga.WithStatus(
                                        PurchaseStatus.AccountCharged,
                                        saga.Price);

                                break;
                            }

                        case PurchaseStatus.AccountCharged:
                            nextSaga =
                                saga.WithStatus(
                                    PurchaseStatus.Confirming,
                                    saga.Price);

                            break;

                        case PurchaseStatus.Confirming:
                            {
                                var library =
                                    GetLibraryProxy(
                                        saga.BookId);

                                await library.ConfirmAsync(
                                    saga.PurchaseId);

                                nextSaga =
                                    saga.WithStatus(
                                        PurchaseStatus.Completed,
                                        saga.Price);

                                break;
                            }

                        case PurchaseStatus.Compensating:
                            {
                                var bank =
                                    GetBankProxy(
                                        saga.UserId);

                                var library =
                                    GetLibraryProxy(
                                        saga.BookId);

                                await bank.RefundAsync(
                                    saga.PurchaseId,
                                    saga.UserId);

                                await library.ReleaseAsync(
                                    saga.PurchaseId);

                                nextSaga =
                                    saga.WithStatus(
                                        PurchaseStatus.Compensated,
                                        saga.Price);

                                break;
                            }

                        default:
                            return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (saga.Status ==
                    PurchaseStatus.Compensating)
                {
                    nextSaga =
                        saga.WithStatus(
                            PurchaseStatus.Failed,
                            saga.Price);
                }
                else if (saga.PastPivot)
                {
                    nextSaga =
                        saga.ScheduleRetry(
                            ex.Message);
                }
                else
                {
                    nextSaga =
                        saga.StartCompensation(
                            ex.Message);
                }
            }

            await SaveSagaAsync(
                saga,
                nextSaga,
                cancellationToken);
        }

        private async Task SaveSagaAsync(
            PurchaseSaga oldSaga,
            PurchaseSaga newSaga,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var purchases =
                await GetPurchasesAsync();

            using (var tx =
                StateManager.CreateTransaction())
            {
                var current =
                    await purchases.TryGetValueAsync(
                        tx,
                        oldSaga.PurchaseId,
                        LockMode.Update);

                if (current.HasValue &&
                    current.Value.Version ==
                    oldSaga.Version)
                {
                    var versionedSaga =
                        newSaga.WithVersion(
                            oldSaga.Version + 1);

                    await purchases.SetAsync(
                        tx,
                        oldSaga.PurchaseId,
                        versionedSaga);

                    await tx.CommitAsync();
                }
            }
        }

        protected override async Task RunAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken
                    .IsCancellationRequested)
                {
                    var dueSagas =
                        await LoadDueAsync(
                            cancellationToken);

                    foreach (var saga in dueSagas)
                    {
                        await AdvanceAsync(
                            saga,
                            cancellationToken);
                    }

                    await Task.Delay(
                        500,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine(
                    "PurchaseOrchestrator is stopping.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "PURCHASE ORCHESTRATOR ERROR:");

                Console.WriteLine(
                    ex.ToString());

                throw;
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