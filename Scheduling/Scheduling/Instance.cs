using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scheduling
{
    public class Instance
    {
        private string _section;

        public static int Days { get; private set; }
        public static Dictionary<string, Shift> Shifts = new Dictionary<string, Shift>();
        public static Dictionary<string, Worker> Staff = new Dictionary<string, Worker>();
        public static Dictionary<int, List<CoverRequirement>> DailyRequirements = new Dictionary<int, List<CoverRequirement>>();

        public Instance(string line)
        {
            _section = line;
        }

        public void ParseLine(string line)
        {
            switch (_section)
            {
                case "HORIZON":
                    ParseHorizon(line);
                    break;
                case "SHIFTS":
                    ParseShifts(line);
                    break;
                case "STAFF":
                    ParseStaff(line);
                    break;
                case "DAYS_OFF":
                    ParseDaysOff(line);
                    break;
                case "SHIFT_ON_REQUESTS":
                    ParseShiftsOn(line);
                    break;
                case "SHIFT_OFF_REQUESTS":
                    ParseShiftsOff(line);
                    break;
                case "COVER":
                    ParseCover(line);
                    break;
                default:
                    break;
            }
        }

        private void ParseHorizon(string line)
        {
            Days = Convert.ToInt32(line);
        }

        private void ParseShifts(string line)
        {
            Shift shift = new Shift();
            var lineSplit = line.Split(',');
            shift.ID = lineSplit[0];
            shift.Length = Convert.ToInt32(lineSplit[1]);
            if (string.IsNullOrEmpty(lineSplit[2]))
            {
                shift.ProhibitsFollowingShifts = new List<string>();
            }
            else
            {
                shift.ProhibitsFollowingShifts = lineSplit[2].Split('|').ToList();
            }            
            Shifts[shift.ID] = shift;
        }

        private void ParseStaff(string line)
        {
            Worker worker = new Worker();
            var lineSplit = line.Split(',');
            worker.ID = lineSplit[0];
            foreach (string shiftLimit in lineSplit[1].Split('|'))
            {
                var shiftSplit = shiftLimit.Split('=');
                worker.MaxShifts[shiftSplit[0]] = Convert.ToInt32(shiftSplit[1]);
            }
            worker.MaxTotalMinutes = Convert.ToInt32(lineSplit[2]);
            worker.MinTotalMinutes = Convert.ToInt32(lineSplit[3]);
            worker.MaxConsecutiveShifts = Convert.ToInt32(lineSplit[4]);
            worker.MinConsecutiveShifts = Convert.ToInt32(lineSplit[5]);
            worker.MinConsecutiveDaysOff = Convert.ToInt32(lineSplit[6]);
            worker.MaxWeekends = Convert.ToInt32(lineSplit[7]);

            Staff[worker.ID] = worker;
        }

        private void ParseDaysOff(string line)
        {
            var lineSplit = line.Split(',');
            for (int i = 1; i < lineSplit.Length; i++)
            {
                var dayIndex = Convert.ToInt32(lineSplit[i]);
                Staff[lineSplit[0]].DaysOff.Add(dayIndex);
            }
        }

        private void ParseShiftsOn(string line)
        {
            var lineSplit = line.Split(',');
            WeightedRequest request = new WeightedRequest(lineSplit[2], Convert.ToInt32(lineSplit[3]));
            Staff[lineSplit[0]].ShiftOnRequests[Convert.ToInt32(lineSplit[1])] = request;
        }

        private void ParseShiftsOff(string line)
        {
            var lineSplit = line.Split(',');
            WeightedRequest request = new WeightedRequest(lineSplit[2], Convert.ToInt32(lineSplit[3]));
            Staff[lineSplit[0]].ShiftOffRequests[Convert.ToInt32(lineSplit[1])] = request;
        }

        private void ParseCover(string line)
        {
            var lineSplit = line.Split(',');
            CoverRequirement req = new CoverRequirement(lineSplit[1], Convert.ToInt32(lineSplit[2]), Convert.ToInt32(lineSplit[3]), Convert.ToInt32(lineSplit[4]));
            if (!DailyRequirements.ContainsKey(Convert.ToInt32(lineSplit[0])))
            {
                DailyRequirements[Convert.ToInt32(lineSplit[0])] = new List<CoverRequirement>();
            }
            DailyRequirements[Convert.ToInt32(lineSplit[0])].Add(req);
        }
    }
}
