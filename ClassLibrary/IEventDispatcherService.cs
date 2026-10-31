using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary
{
    public interface IEventDispatcherService : IService
    {
        Task PublishAsync(PurchaseEvent evt);
    }
}
