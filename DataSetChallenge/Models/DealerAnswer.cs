using System.Collections.Generic;

namespace DataSetChallenge.Models
{
    public class DealerAnswer
    {
        public int dealerId { get; set; }
        public string name { get; set; }
        public List<VehicleAnswer> vehicles { get; set; }   //In Swagger this is an Array, however using an array in the model here introduces a lot of complexity and potential performance loss.
    }
}
