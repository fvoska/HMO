using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scheduling
{
    public class CoverRequirement
    {
        public string ShiftID;
        public int Requirement;
        public int WeightUnder;
        public int WeightOver;

        public CoverRequirement(string shiftID, int req, int under, int over)
        {
            ShiftID = shiftID;
            Requirement = req;
            WeightUnder = under;
            WeightOver = over;
        }
    }
}
