﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scheduling
{
    public class Worker
    {
        public string ID { get; set; }
        public Dictionary<string, int> MaxShifts { get; set; }
        public int MaxTotalMinutes { get; set; }
        public int WorkedMinutes { get
            {
                return Assignments.Values.Sum(a => a.Shift.Length);
            }
        }
        public float RemainingMinutes { get
            {
                return (float) WorkedMinutes / MinTotalMinutes;
            }
        }
        public int MinTotalMinutes { get; set; }
        public int MaxConsecutiveShifts { get; set; }
        public int MinConsecutiveShifts { get; set; }
        public int MinConsecutiveDaysOff { get; set; }
        public int WorkedWeekends { get; set; }
        public int MaxWeekends { get; set; }
        public List<int> DaysOff { get; set; }
        public Dictionary<int, WeightedRequest> ShiftOnRequests { get; set; }
        public Dictionary<int, WeightedRequest> ShiftOffRequests { get; set; }
        public Dictionary<int, Assignment> Assignments { get; set; }

        public Worker()
        {
            MaxShifts = new Dictionary<string, int>();
            DaysOff = new List<int>();
            ShiftOnRequests = new Dictionary<int, WeightedRequest>();
            ShiftOffRequests = new Dictionary<int, WeightedRequest>();
            Assignments = new Dictionary<int, Assignment>();
        }

        public int WorkedShiftsByType(string id)
        {
            return Assignments.Values.Count(a => a.Shift.ID == id);
        }
    }
}
