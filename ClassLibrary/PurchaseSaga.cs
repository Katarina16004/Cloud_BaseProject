using System;
using System.Runtime.Serialization;

namespace ClassLibrary
{
    [DataContract]
    public class PurchaseSaga
    {
        [DataMember]
        public Guid PurchaseId { get; private set; }

        [DataMember]
        public string UserId { get; private set; }

        [DataMember]
        public int BookId { get; private set; }

        [DataMember]
        public string BookName { get; private set; }

        [DataMember]
        public int Quantity { get; private set; }

        [DataMember]
        public double Price { get; private set; }

        [DataMember]
        public PurchaseStatus Status { get; private set; }

        [DataMember]
        public int Attempt { get; private set; }

        [DataMember]
        public DateTime NextAttemptUtc { get; private set; }

        [DataMember]
        public string LastError { get; private set; }

        [DataMember]
        public long Version { get; private set; }

        private PurchaseSaga()
        {
        }

        public PurchaseSaga(
            Guid purchaseId,
            string userId,
            int bookId,
            string bookName,
            int quantity,
            double price)
            : this(
                purchaseId,
                userId,
                bookId,
                bookName,
                quantity,
                price,
                PurchaseStatus.Started,
                0,
                DateTime.UtcNow,
                null,
                0)
        {
        }

        private PurchaseSaga(
            Guid purchaseId,
            string userId,
            int bookId,
            string bookName,
            int quantity,
            double price,
            PurchaseStatus status,
            int attempt,
            DateTime nextAttemptUtc,
            string lastError,
            long version)
        {
            PurchaseId = purchaseId;
            UserId = userId;
            BookId = bookId;
            BookName = bookName;
            Quantity = quantity;
            Price = price;
            Status = status;
            Attempt = attempt;
            NextAttemptUtc = nextAttemptUtc;
            LastError = lastError;
            Version = version;
        }

        public bool IsTerminal
        {
            get
            {
                return Status == PurchaseStatus.Completed ||
                       Status == PurchaseStatus.Compensated ||
                       Status == PurchaseStatus.Failed;
            }
        }

        public bool PastPivot
        {
            get
            {
                return Status == PurchaseStatus.Confirming ||
                       Status == PurchaseStatus.Completed;
            }
        }

        public PurchaseSaga WithStatus(
            PurchaseStatus status,
            double price)
        {
            return Copy(
                status,
                price,
                Attempt,
                NextAttemptUtc,
                LastError,
                Version);
        }

        public PurchaseSaga ScheduleRetry(
            string error)
        {
            int nextAttempt = Attempt + 1;

            double seconds =
                Math.Min(
                    60,
                    Math.Pow(2, Attempt));

            return Copy(
                Status,
                Price,
                nextAttempt,
                DateTime.UtcNow.AddSeconds(seconds),
                error,
                Version);
        }

        public PurchaseSaga StartCompensation(
            string reason)
        {
            return Copy(
                PurchaseStatus.Compensating,
                Price,
                0,
                DateTime.UtcNow,
                reason,
                Version);
        }

        public PurchaseSaga WithVersion(
            long version)
        {
            return Copy(
                Status,
                Price,
                Attempt,
                NextAttemptUtc,
                LastError,
                version);
        }

        private PurchaseSaga Copy(
            PurchaseStatus status,
            double price,
            int attempt,
            DateTime nextAttemptUtc,
            string lastError,
            long version)
        {
            return new PurchaseSaga(
                PurchaseId,
                UserId,
                BookId,
                BookName,
                Quantity,
                price,
                status,
                attempt,
                nextAttemptUtc,
                lastError,
                version);
        }
    }
}