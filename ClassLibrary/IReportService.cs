using Microsoft.ServiceFabric.Services.Remoting;
using System.Threading.Tasks;

namespace ClassLibrary
{
    public interface IReportService : IService
    {
        Task RecordSaleAsync(
            SaleConfirmed sale);

        Task<long> GetSalesCountAsync();
    }
}