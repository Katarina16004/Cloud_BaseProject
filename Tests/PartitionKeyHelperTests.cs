using ClassLibrary;
using Xunit;

namespace Tests
{

    public class PartitionKeyHelperTests
    {
        [Fact]
        public void Raspodjela_kljuceva_je_ravnomerna()
        {
            const int n = 10_000, partitionCount = 4;
            var counts = new int[partitionCount];
            var rnd = new Random(42);
            for (int i = 0; i < n; i++)
            {
                string isbn = $"978-{rnd.Next(10, 99)}-{rnd.Next(100, 999)}-{rnd.Next(1000, 9999)}-{rnd.Next(0, 9)}";
                counts[PartitionIndex(PartitionKeyHelper.ForBook(isbn), partitionCount)]++;
            }
            Assert.All(counts, c => Assert.InRange(c, 2250, 2750));
        }

        // Približno mapiranje Int64 ključa na indeks particije (jednaki opsezi kao UniformInt64Partition)
        private static int PartitionIndex(long key, int count)
        {
            ulong offset = unchecked((ulong)(key - long.MinValue));   // 0 .. 2^64 - 1
            return (int)(offset / (ulong.MaxValue / (ulong)count + 1));
        }
    }
}
