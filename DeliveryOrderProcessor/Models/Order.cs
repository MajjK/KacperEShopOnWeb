using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KacperFunctionApp.Models
{
    public class Order
    {
        public string id { get; set; }
        public Address ShippingAddress { get; set; }
        public List<OrderItem> ListOfItems { get; set;}
        public double FinalPrice { get; set; }
    }
}
