using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scheduling
{
    public class Assignment
    {
        public int Day { get; set; }
        public Shift Shift { get; set; }
        public Worker Worker { get; set; }
    }
}
