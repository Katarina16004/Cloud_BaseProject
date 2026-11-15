namespace ClassLibrary
{
    public static class PartitionKeys
    {
        public static long ForBook(int bookId)
        {
            return PartitionKeyHelper.ForBook(bookId);
        }

        public static long ForAccount(string userId)
        {
            return PartitionKeyHelper.ForAccount(userId);
        }
    }
}