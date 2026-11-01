namespace ClassLibrary
{
    public class PurchaseRequestDto
    {
        public int BookId { get; set; }
        public int Quantity { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Account { get; set; }
        public string UserId { get; set; }
        public double Price { get; set; }

        public PurchaseRequestDto() { }

        public PurchaseRequestDto(
            int bookId,
            int quantity,
            string name,
            string author,
            string account,
            string userId,
            double price)
        {
            BookId = bookId;
            Quantity = quantity;
            Name = name;
            Author = author;
            Account = account;
            UserId = userId;
            Price = price;
        }
    }
}