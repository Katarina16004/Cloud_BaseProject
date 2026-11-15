using System;
using System.Runtime.Serialization;

namespace ClassLibrary
{
    [DataContract]
    public class SaleConfirmed
    {
        [DataMember]
        public Guid PurchaseId { get; private set; }

        [DataMember]
        public int BookId { get; private set; }

        [DataMember]
        public double Price { get; private set; }

        [DataMember]
        public DateTime OccurredAtUtc { get; private set; }

        private SaleConfirmed()
        {
        }

        public SaleConfirmed(
            Guid purchaseId,
            int bookId,
            double price,
            DateTime occurredAtUtc)
        {
            PurchaseId = purchaseId;
            BookId = bookId;
            Price = price;
            OccurredAtUtc = occurredAtUtc;
        }
    }
}