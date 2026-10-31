using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary
{
    public class Book
    {
        public int BookId { get; set; }
        public string Author { get; set; }

        public string Name { get; set; } 

        public double Price { get; set; }

        public int Quantity { get; set; }

        public Book() { }

        public Book(int id, string author, string name, double price, int quantity)
        {
            BookId = id;
            Author = author;
            Name = name;
            Price = price;
            Quantity = quantity;
        }
    }
}
