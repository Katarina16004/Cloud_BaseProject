using System;
using System.Text;

namespace ClassLibrary
{
    public static class PartitionKeyHelper
    {
        private const ulong FnvOffset =
            14695981039346656037UL;

        private const ulong FnvPrime =
            1099511628211UL;

        public static long ForBook(int bookId)
        {
            return ForBook(
                bookId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        public static long ForBook(string bookId)
        {
            if (string.IsNullOrWhiteSpace(bookId))
            {
                throw new ArgumentException(
                    "Book ID is required.",
                    nameof(bookId));
            }

            string normalizedBookId =
                bookId.Trim().ToUpperInvariant();

            return Fnv1a64(
                "book:" + normalizedBookId);
        }

        public static long ForAccount(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "User ID is required.",
                    nameof(userId));
            }

            string normalizedUserId =
                userId.Trim();

            return Fnv1a64(
                "account:" + normalizedUserId);
        }

        public static long ForPurchase(Guid purchaseId)
        {
            return Fnv1a64(
                purchaseId.ToString("N"));
        }

        public static long Fnv1a64(string value)
        {
            ulong hash = FnvOffset;

            foreach (byte currentByte
                in Encoding.UTF8.GetBytes(value))
            {
                hash ^= currentByte;
                hash *= FnvPrime;
            }

            return unchecked((long)Mix(hash));
        }

        private static ulong Mix(ulong hash)
        {
            hash ^= hash >> 33;
            hash *= 0xff51afd7ed558ccdUL;

            hash ^= hash >> 33;
            hash *= 0xc4ceb9fe1a85ec53UL;

            return hash ^ (hash >> 33);
        }
    }
}