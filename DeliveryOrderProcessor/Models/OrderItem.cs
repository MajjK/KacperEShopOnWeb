﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KacperFunctionApp.Models
{
    public class OrderItem
    {
        public CatalogItemOrdered ItemOrdered { get; set; }
        public decimal UnitPrice { get; set; }
        public int Units { get; set; }
    }
}
