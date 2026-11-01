using System;
using System.Runtime.Serialization;

namespace ClassLibrary
{
    [DataContract]
    public class Reservation
    {
        [DataMember]
        public Guid TxId { get; private set; }

        [DataMember]
        public int BookId { get; private set; }

        [DataMember]
        public int Quantity { get; private set; }

        [DataMember]
        public double Price { get; private set; }

        [DataMember]
        public string Status { get; private set; }

        private Reservation()
        {
        }

        public Reservation(
            Guid txId,
            int bookId,
            int quantity,
            double price,
            string status)
        {
            TxId = txId;
            BookId = bookId;
            Quantity = quantity;
            Price = price;
            Status = status;
        }
    }
}