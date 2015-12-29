using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scheduling
{
    public class Shift
    {
        public string ID { get; set; }
        public int Length { get; set; }
        public List<string> ProhibitsFollowingShifts { get; set; }

        public Shift()
        {
            ProhibitsFollowingShifts = new List<string>();
        }
    }
}
