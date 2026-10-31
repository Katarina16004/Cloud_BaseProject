using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public string Account {  get; set; }

        public double Balance { get; set; }

        // prosiri mozda jos nekim poljima

        public Customer() { }

        public Customer(int id, string account, double balance) 
        { 
            CustomerId = id;
            Account = account;
            Balance = balance;
        }
    }
}
