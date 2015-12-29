using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scheduling
{
    public class WeightedRequest
    {
        public string ID;
        public int Weigth;
        
        public WeightedRequest(string id, int weight)
        {
            ID = id;
            Weigth = weight;
        }
    }
}
