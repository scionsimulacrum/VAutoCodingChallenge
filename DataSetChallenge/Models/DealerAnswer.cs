using System;
using System.Collections.Generic;
using System.Text;

namespace DataSetChallenge.Models
{
    public class DealerAnswer
    {
        public int dealerId { get; set; }
        public string name { get; set; }
        public List<VehicleAnswer> vehicles { get; set; }
    }
}
