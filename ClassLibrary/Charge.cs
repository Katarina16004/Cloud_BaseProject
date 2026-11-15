using System;
using System.Runtime.Serialization;

namespace ClassLibrary
{
    [DataContract]
    public class Charge
    {
        [DataMember]
        public Guid TxId { get; private set; }

        [DataMember]
        public string UserId { get; private set; }

        [DataMember]
        public double Amount { get; private set; }

        [DataMember]
        public string Status { get; private set; }

        private Charge()
        {
        }

        public Charge(
            Guid txId,
            string userId,
            double amount,
            string status)
        {
            TxId = txId;
            UserId = userId;
            Amount = amount;
            Status = status;
        }

        public Charge WithStatus(string status)
        {
            return new Charge(
                TxId,
                UserId,
                Amount,
                status);
        }
    }
}