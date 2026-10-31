using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary
{
    [DataContract]
    public class PurchaseEvent
    {
        [DataMember] public string Message { get; set; }
        [DataMember] public DateTime Timestamp { get; set; }
        [DataMember] public string ServiceSource { get; set; }
        [DataMember] public PurchaseRequestDto PurchaseRequest { get; set; }
    }

}
