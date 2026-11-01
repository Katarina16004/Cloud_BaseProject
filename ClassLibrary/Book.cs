using System.Runtime.Serialization;

namespace ClassLibrary
{
    [DataContract]
    public class Book
    {
        [DataMember]
        public int BookId { get; private set; }

        [DataMember]
        public string Author { get; private set; }

        [DataMember]
        public string Name { get; private set; }

        [DataMember]
        public double Price { get; private set; }

        [DataMember]
        public int Quantity { get; private set; }

        [DataMember]
        public int Reserved { get; private set; }

        [DataMember]
        public int InitialQuantity { get; private set; }

        private Book()
        {
        }

        public Book(
            int bookId,
            string author,
            string name,
            double price,
            int quantity)
            : this(
                bookId,
                author,
                name,
                price,
                quantity,
                0,
                quantity)
        {
        }

        public Book(
            int bookId,
            string author,
            string name,
            double price,
            int quantity,
            int reserved)
            : this(
                bookId,
                author,
                name,
                price,
                quantity,
                reserved,
                quantity + reserved)
        {
        }

        private Book(
            int bookId,
            string author,
            string name,
            double price,
            int quantity,
            int reserved,
            int initialQuantity)
        {
            BookId = bookId;
            Author = author;
            Name = name;
            Price = price;
            Quantity = quantity;
            Reserved = reserved;
            InitialQuantity = initialQuantity;
        }

        public Book WithStockAndReserved(
            int quantity,
            int reserved)
        {
            return new Book(
                BookId,
                Author,
                Name,
                Price,
                quantity,
                reserved,
                InitialQuantity);
        }
    }
}