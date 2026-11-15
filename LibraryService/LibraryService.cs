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

namespace LibraryService
{
    internal sealed class LibraryService :
        StatefulService,
        ILibraryService
    {
        private const string DictionaryName = "books";
        private const string ReservationDictionaryName =
            "reservations";
        private const string OutboxQueueName =
    "salesOutbox";
        private static readonly Uri ReportServiceUri =
    new Uri(
        "fabric:/MiniProjekat/ReportService");

        public LibraryService(StatefulServiceContext context)
            : base(context)
        {
        }

        private async Task<
            IReliableDictionary<string, Book>>
            GetDict()
        {
            return await StateManager.GetOrAddAsync<
                IReliableDictionary<string, Book>>(
                    DictionaryName);
        }

        private async Task<
            IReliableDictionary<Guid, Reservation>>
            GetReservations()
        {
            return await StateManager.GetOrAddAsync<
                IReliableDictionary<Guid, Reservation>>(
                    ReservationDictionaryName);
        }
        private async Task<
    IReliableQueue<SaleConfirmed>>
    GetOutboxAsync()
        {
            return await StateManager.GetOrAddAsync<
                IReliableQueue<SaleConfirmed>>(
                    OutboxQueueName);
        }

        protected override async Task RunAsync(
    CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken
                    .IsCancellationRequested)
                {
                    try
                    {
                        await ProcessOutboxAsync(
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            "OUTBOX PROCESSING ERROR:");

                        Console.WriteLine(
                            ex.ToString());

                        await Task.Delay(
                            TimeSpan.FromSeconds(5),
                            cancellationToken);
                    }

                    await Task.Delay(
                        TimeSpan.FromMilliseconds(500),
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine(
                    "LibraryService is stopping.");
            }
        }

        public async Task<Book> BookExists(string bookName)
        {
            var dict = await GetDict();

            using (var tx = StateManager.CreateTransaction())
            {
                var result = await dict.TryGetValueAsync(
                    tx,
                    bookName);

                return result.HasValue
                    ? result.Value
                    : null;
            }
        }

        public async Task<bool> HasStock(
            string bookName,
            int quantity)
        {
            var dict = await GetDict();

            using (var tx = StateManager.CreateTransaction())
            {
                var result = await dict.TryGetValueAsync(
                    tx,
                    bookName);

                return result.HasValue &&
                       result.Value.Quantity >= quantity;
            }
        }

        public async Task DecreaseStock(
            string bookName,
            int quantity)
        {
            var dict = await GetDict();

            using (var tx = StateManager.CreateTransaction())
            {
                var result = await dict.TryGetValueAsync(
                    tx,
                    bookName);

                if (result.HasValue)
                {
                    var book = result.Value;

                    var updatedBook =
                        book.WithStockAndReserved(
                            book.Quantity - quantity,
                            book.Reserved);

                    await dict.SetAsync(
                        tx,
                        bookName,
                        updatedBook);

                    await tx.CommitAsync();
                }
            }
        }

        public async Task<List<Book>> GetAllBooksAsync()
        {
            var dict = await GetDict();
            var list = new List<Book>();

            using (var tx = StateManager.CreateTransaction())
            {
                var enumerable =
                    await dict.CreateEnumerableAsync(tx);

                var enumerator =
                    enumerable.GetAsyncEnumerator();

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
            var dict = await GetDict();

            using (var tx = StateManager.CreateTransaction())
            {
                return await dict.GetCountAsync(tx);
            }
        }

        public async Task AddBookAsync(Book book)
        {
            var dict = await GetDict();

            using (var tx = StateManager.CreateTransaction())
            {
                var existing = await dict.TryGetValueAsync(
                    tx,
                    book.Name);

                if (!existing.HasValue)
                {
                    await dict.AddAsync(
                        tx,
                        book.Name,
                        book);

                    await tx.CommitAsync();
                }
            }
        }

        public async Task<double> ReserveAsync(
    Guid txId,
    int bookId,
    string bookName,
    int quantity)
        {
            if (txId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Transaction ID is required.",
                    "txId");
            }

            if (quantity <= 0)
            {
                throw new ArgumentException(
                    "Quantity must be greater than zero.",
                    "quantity");
            }

            var books =
                await GetDict();

            var reservations =
                await GetReservations();

            using (var tx =
                StateManager.CreateTransaction())
            {
                var existingReservation =
                    await reservations.TryGetValueAsync(
                        tx,
                        txId,
                        LockMode.Update);

                if (existingReservation.HasValue)
                {
                    var existing =
                        existingReservation.Value;

                    if (existing.BookId != bookId)
                    {
                        throw new InvalidOperationException(
                            "Transaction ID is already used " +
                            "for another book.");
                    }

                    return existing.Price;
                }

                var result =
                    await books.TryGetValueAsync(
                        tx,
                        bookName,
                        LockMode.Update);

                if (!result.HasValue)
                {
                    throw new InvalidOperationException(
                        "Book not found: " + bookName);
                }

                var book =
                    result.Value;

                if (book.BookId != bookId)
                {
                    throw new InvalidOperationException(
                        "Book ID does not match book name.");
                }

                if (book.Quantity < quantity)
                {
                    throw new InvalidOperationException(
                        "Not enough stock.");
                }

                var updatedBook =
                    book.WithStockAndReserved(
                        book.Quantity - quantity,
                        book.Reserved + quantity);

                await books.SetAsync(
                    tx,
                    bookName,
                    updatedBook);

                await reservations.AddAsync(
                    tx,
                    txId,
                    new Reservation(
                        txId,
                        bookId,
                        quantity,
                        book.Price,
                        ReservationStatus.Reserved));

                await tx.CommitAsync();

                return book.Price;
            }
        }
        public async Task ConfirmAsync(
    Guid txId)
        {
            var books =
                await GetDict();

            var reservations =
                await GetReservations();

            var outbox =
                await GetOutboxAsync();

            using (var tx =
                StateManager.CreateTransaction())
            {
                var reservationResult =
                    await reservations.TryGetValueAsync(
                        tx,
                        txId,
                        LockMode.Update);

                if (!reservationResult.HasValue)
                {
                    return;
                }

                var reservation =
                    reservationResult.Value;

                if (reservation.Status !=
                    ReservationStatus.Reserved)
                {
                    return;
                }

                string bookName = null;
                Book book = null;

                var enumerable =
                    await books.CreateEnumerableAsync(tx);

                var enumerator =
                    enumerable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(
                    CancellationToken.None))
                {
                    var currentBook =
                        enumerator.Current.Value;

                    if (currentBook.BookId ==
                        reservation.BookId)
                    {
                        bookName =
                            enumerator.Current.Key;

                        book =
                            currentBook;

                        break;
                    }
                }

                if (book == null)
                {
                    throw new InvalidOperationException(
                        "Book not found for reservation.");
                }

                var updatedBook =
                    book.WithStockAndReserved(
                        book.Quantity,
                        book.Reserved -
                        reservation.Quantity);

                await books.SetAsync(
                    tx,
                    bookName,
                    updatedBook);

                await reservations.SetAsync(
                    tx,
                    txId,
                    reservation.WithStatus(
                        ReservationStatus.Confirmed));

                await outbox.EnqueueAsync(
                    tx,
                    new SaleConfirmed(
                        txId,
                        reservation.BookId,
                        reservation.Price,
                        DateTime.UtcNow));

                await tx.CommitAsync();
            }
        }
        public async Task ReleaseAsync(
    Guid txId)
        {
            var books = await GetDict();
            var reservations = await GetReservations();

            using (var tx = StateManager.CreateTransaction())
            {
                var reservationResult =
                    await reservations.TryGetValueAsync(
                        tx,
                        txId,
                        LockMode.Update);

                if (!reservationResult.HasValue)
                {
                    return;
                }

                var reservation =
                    reservationResult.Value;

                if (reservation.Status !=
                    ReservationStatus.Reserved)
                {
                    return;
                }

                string bookName = null;
                Book book = null;

                var enumerable =
                    await books.CreateEnumerableAsync(tx);

                var enumerator =
                    enumerable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(
                    CancellationToken.None))
                {
                    var currentBook =
                        enumerator.Current.Value;

                    if (currentBook.BookId ==
                        reservation.BookId)
                    {
                        bookName =
                            enumerator.Current.Key;

                        book = currentBook;
                        break;
                    }
                }

                if (book == null)
                {
                    throw new InvalidOperationException(
                        "Book not found for reservation.");
                }

                var updatedBook =
                    book.WithStockAndReserved(
                        book.Quantity +
                        reservation.Quantity,
                        book.Reserved -
                        reservation.Quantity);

                await books.SetAsync(
                    tx,
                    bookName,
                    updatedBook);

                await reservations.SetAsync(
                    tx,
                    txId,
                    reservation.WithStatus(
                        ReservationStatus.Released));

                await tx.CommitAsync();
            }
        }

        public async Task<ConsistencyResult>
            GetConsistencyAsync(int bookId)
        {
            var books = await GetDict();
            var reservations = await GetReservations();

            using (var tx = StateManager.CreateTransaction())
            {
                Book selectedBook = null;
                string selectedBookName = null;

                var bookEnumerable =
                    await books.CreateEnumerableAsync(tx);

                var bookEnumerator =
                    bookEnumerable.GetAsyncEnumerator();

                while (await bookEnumerator.MoveNextAsync(
                    CancellationToken.None))
                {
                    var currentBook =
                        bookEnumerator.Current.Value;

                    if (currentBook.BookId == bookId)
                    {
                        selectedBook = currentBook;
                        selectedBookName =
                            bookEnumerator.Current.Key;
                        break;
                    }
                }

                if (selectedBook == null)
                {
                    throw new InvalidOperationException(
                        "Book not found: " + bookId);
                }

                int reservationCount = 0;
                int reservedQuantitySum = 0;

                var reservationEnumerable =
                    await reservations.CreateEnumerableAsync(tx);

                var reservationEnumerator =
                    reservationEnumerable.GetAsyncEnumerator();

                while (await reservationEnumerator.MoveNextAsync(
                    CancellationToken.None))
                {
                    var reservation =
                        reservationEnumerator.Current.Value;

                    if (reservation.BookId == bookId &&
                        reservation.Status ==
                        ReservationStatus.Reserved)
                    {
                        reservationCount++;
                        reservedQuantitySum +=
                            reservation.Quantity;
                    }
                }

                bool isConsistent =
                    selectedBook.Quantity +
                    selectedBook.Reserved ==
                    selectedBook.InitialQuantity
                    &&
                    selectedBook.Reserved ==
                    reservedQuantitySum;

                return new ConsistencyResult
                {
                    BookId = selectedBook.BookId,
                    BookName = selectedBookName,
                    Stock = selectedBook.Quantity,
                    Reserved = selectedBook.Reserved,
                    ReservationCount = reservationCount,
                    ReservedQuantitySum = reservedQuantitySum,
                    InitialStock =
                        selectedBook.InitialQuantity,
                    IsConsistent = isConsistent
                };
            }
        }
        private async Task ProcessOutboxAsync(
    CancellationToken cancellationToken)
        {
            SaleConfirmed sale;

            var outbox =
                await GetOutboxAsync();

            using (var readTx =
                StateManager.CreateTransaction())
            {
                var next =
                    await outbox.TryPeekAsync(readTx);

                if (!next.HasValue)
                {
                    return;
                }

                sale = next.Value;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var reportProxy =
    ServiceProxy.Create<IReportService>(
        ReportServiceUri,
        new ServicePartitionKey(0));

            await reportProxy.RecordSaleAsync(
                sale);

            using (var removeTx =
                StateManager.CreateTransaction())
            {
                var removed =
                    await outbox.TryDequeueAsync(removeTx);

                if (!removed.HasValue)
                {
                    return;
                }

                if (removed.Value.PurchaseId !=
                    sale.PurchaseId)
                {
                    throw new InvalidOperationException(
                        "Outbox message changed unexpectedly.");
                }

                await removeTx.CommitAsync();
            }
        }

        protected override IEnumerable<ServiceReplicaListener>
            CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }
    }
}