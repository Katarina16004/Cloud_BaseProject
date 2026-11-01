using System;
using System.Security.Cryptography;
using System.Text;

namespace ClassLibrary
{
    public static class PartitionKeys
    {
        public static long ForBook(int bookId)
        {
            return bookId;
        }

        public static long ForAccount(string userId)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(userId ?? string.Empty));

                return BitConverter.ToInt64(bytes, 0);
            }
        }
    }
}