using System;
using System.Text;

namespace ClassLibrary
{
    public static class PartitionKeyHelper
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        public static long ForBook(string bookId) => Fnv1a64("book:" + bookId.Trim().ToUpperInvariant());
        public static long ForAccount(string userId) => Fnv1a64("account:" + userId.Trim());
        public static long ForPurchase(Guid purchaseId) => Fnv1a64(purchaseId.ToString("N"));

        public static long Fnv1a64(string value)
        {
            ulong hash = FnvOffset;
            foreach (byte b in Encoding.UTF8.GetBytes(value))
            {
                hash ^= b;
                hash *= FnvPrime;
            }
            return unchecked((long)Mix(hash));   // cijeli Int64 opseg
        }

        // završno miješanje bitova (murmur3 fmix64)
        private static ulong Mix(ulong h)
        {
            h ^= h >> 33; h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33; h *= 0xc4ceb9fe1a85ec53UL;
            return h ^ (h >> 33);
        }

    }
}
