using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary
{
    public class PurchaseRequestDto
    {
        public int Quantity { get; set; }
        public string Name { get; set; } // book name
        public string Author { get; set; } // book author
        public string Account { get; set; } // customer mail
        public string UserId { get; set; }
        public double Price { get; set; }

        public PurchaseRequestDto() { }

        public PurchaseRequestDto(int quantity, string name, string author, string account, 
            string userId, double price)
        {
            Quantity = quantity;
            Name = name;
            Author = author;
            Account = account;
            UserId = userId;
            Price = price;
        }
    }
}
