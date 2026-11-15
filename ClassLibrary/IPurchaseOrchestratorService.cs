using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Threading.Tasks;

namespace ClassLibrary
{
    public interface IPurchaseOrchestratorService : IService
    {
        // pokrece novu kupovinu sa idempotentnim id-om purchase id
        Task<Guid> StartPurchaseAsync(
    Guid purchaseId,
    string userId,
    int bookId,
    int quantity);

        Task<PurchaseSaga> GetPurchaseAsync(
            Guid purchaseId);

        //otkazivanje kupovine
        Task CancelPurchaseAsync(
            Guid purchaseId);
    }
}