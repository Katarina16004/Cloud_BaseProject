using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary
{
    public interface IBankService : IService
    {
        Task<bool> HasEnoughMoney(string userId, double amount);
        Task Withdraw(string userId, double amount);

        Task<List<Customer>> GetAllCustomersAsync();
        //Task AddDataAsync();
        Task<long> GetCountAsync();

        Task AddCustomerAsync(Customer customer);
    }
}
