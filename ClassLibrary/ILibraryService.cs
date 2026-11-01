using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassLibrary
{
    public interface ILibraryService : IService
    {
        Task<Book> BookExists(string bookName);
        Task<bool> HasStock(string bookName, int qty);
        Task DecreaseStock(string bookName, int qty);
        Task<List<Book>> GetAllBooksAsync();
        Task<long> GetCountAsync();

        Task AddBookAsync(Book book);
    }
}
