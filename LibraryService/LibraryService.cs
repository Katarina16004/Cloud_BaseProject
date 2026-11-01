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

namespace LibraryService
{
    internal sealed class LibraryService :
        StatefulService,
        ILibraryService
    {
        private const string DictionaryName = "books";
        private const string ReservationDictionaryName =
            "reservations";

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

        private async Task AddDataAsync()
        {
            var dict = await GetDict();

            using (var tx = StateManager.CreateTransaction())
            {
                var book1 = await dict.TryGetValueAsync(
                    tx,
                    "Orkanski visovi");

                if (!book1.HasValue)
                {
                    await dict.AddAsync(
                        tx,
                        "Orkanski visovi",
                        new Book(
                            1,
                            "Emili Bronte",
                            "Orkanski visovi",
                            400,
                            1000));
                }

                var book2 = await dict.TryGetValueAsync(
                    tx,
                    "Zlocin i kazna");

                if (!book2.HasValue)
                {
                    await dict.AddAsync(
                        tx,
                        "Zlocin i kazna",
                        new Book(
                            2,
                            "Fjodor Dostojevski",
                            "Zlocin i kazna",
                            150,
                            5));
                }

                await tx.CommitAsync();
            }
        }

        protected override async Task RunAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                await AddDataAsync();

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(
                        1000,
                        cancellationToken);
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
            if (quantity <= 0)
            {
                throw new ArgumentException(
                    "Quantity must be greater than zero.",
                    "quantity");
            }

            var books = await GetDict();
            var reservations = await GetReservations();

            using (var tx = StateManager.CreateTransaction())
            {
                var result = await books.TryGetValueAsync(
                    tx,
                    bookName,
                    LockMode.Update);

                if (!result.HasValue)
                {
                    throw new InvalidOperationException(
                        "Book not found: " + bookName);
                }

                var book = result.Value;

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

        protected override IEnumerable<ServiceReplicaListener>
            CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }
    }
}