﻿using System;
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
        public static Dictionary<string, Worker> Workers = new Dictionary<string, Worker>();
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

            Workers[worker.ID] = worker;
        }

        private void ParseDaysOff(string line)
        {
            var lineSplit = line.Split(',');
            for (int i = 1; i < lineSplit.Length; i++)
            {
                var dayIndex = Convert.ToInt32(lineSplit[i]);
                Workers[lineSplit[0]].DaysOff.Add(dayIndex);
            }
        }

        private void ParseShiftsOn(string line)
        {
            var lineSplit = line.Split(',');
            WeightedRequest request = new WeightedRequest(lineSplit[2], Convert.ToInt32(lineSplit[3]));
            Workers[lineSplit[0]].ShiftOnRequests[Convert.ToInt32(lineSplit[1])] = request;
        }

        private void ParseShiftsOff(string line)
        {
            var lineSplit = line.Split(',');
            WeightedRequest request = new WeightedRequest(lineSplit[2], Convert.ToInt32(lineSplit[3]));
            Workers[lineSplit[0]].ShiftOffRequests[Convert.ToInt32(lineSplit[1])] = request;
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

        public static void Assign()
        {
            for (int day = 0; day < 3; day++)
            {
                bool isWeekend = false;
                if ((day + 1) % 7 == 0 || (day + 2) % 7 == 0)
                {
                    isWeekend = true;
                }
                Console.WriteLine("Day #" + day + " (is weekend: " + isWeekend + ")");
                foreach (KeyValuePair<string, Shift> shift in Shifts)
                {
                    Dictionary<string, Worker> potentialWorkers = Workers.ToDictionary(d => d.Key, d => d.Value);
                    CoverRequirement coverRequirements = DailyRequirements[day].Where(r => r.ShiftID == shift.Value.ID).First();

                    Console.WriteLine("\tShift " + shift.Value.ID + " (cover target: " + coverRequirements.Requirement + ")");

                    // Nikada nećemo u jednom danu uzeti više od preporučenog broja radnika, ali možda nećemo moći odabrati točno taj broj nego neki manji broj jer nam nije preostalo dovoljno radnika (npr. sve smo ih odbacili jakim ograničenjima).
                    // (3.bullet slabih ogr., broj radnika smjene)
                    for (int assignedNumberOfWorkers = 0; assignedNumberOfWorkers < coverRequirements.Requirement; assignedNumberOfWorkers++)
                    {
                        Console.WriteLine("\t\tTrying to assign worker " + (assignedNumberOfWorkers + 1) + "/" + coverRequirements.Requirement);

                        Console.WriteLine("\t\t\tConsidering " + potentialWorkers.Count + " workers.");

                        // Odbaci radnike koji su ovaj dan već dobili smjenu.
                        // (1. bullet jakih org.)
                        removeByShiftAlreadyAssignedToday(potentialWorkers, day);
                        Console.WriteLine("\t\t\tConsidering " + potentialWorkers.Count + " workers after removing workers who already worked today.");

                        // Odbaci radnike koji ne mogu raditi u smjeni s jer su radili u nekoj ranijoj smjeni.
                        // (2.bullet jakih ogr., SECTION_SHIFTS -Shifts which cannot follow this shift)
                        removeByShiftRotation(potentialWorkers, day, shift.Value);
                        Console.WriteLine("\t\t\tConsidering " + potentialWorkers.Count + " workers after removing workers based on shift rotation.");

                        // Odbaci radnike za koje vrijedi:
                        //      Do sada sveukupni broj minuta koje su odravili + vrijeme trajanja smjene s > MaxTotalMinutes.
                        // (4.bullet jakih, SECTION_STAFF - MaxTotalMinutes)
                        removeByMaxTotalMinutes(potentialWorkers, shift.Value);
                        Console.WriteLine("\t\t\tConsidering " + potentialWorkers.Count + " workers after removing workers who reached max minutes.");

                        // Odbaci radnike kojima bi rad u ovoj smjeni prekršio ograničenje maksimalnog broja uzastopnih smjena (takvi moraju na odmor).
                        // (5.bullet jakih, SECTION_STAFF - MaxConsectiveShifts)
                        removeByMaxConsectiveShifts(potentialWorkers, day);
                        Console.WriteLine("\t\t\tConsidering " + potentialWorkers.Count + " workers after removing workers by max consecutive shifts.");

                        // Odbaci radnike koji još nisu bili na odmoru minimalni broj dana(moraju još biti na odmoru).
                        // (7.bullet jakih, SECTION_STAFF - MinConsecutiveDaysOff)
                        removeByMinConsecutiveDaysOff(potentialWorkers, day);
                        Console.WriteLine("\t\t\tConsidering " + potentialWorkers.Count + " workers after removing workers by min days off.");

                        // Ako je dan d subota ili nedjelja:
                        //      Odbaci radnike koji su već na maksimalnom broju radnih vikenda.
                        // (8.bullet jakih)
                        if (isWeekend)
                        {
                            removeByMaxWeekends(potentialWorkers);
                            Console.WriteLine("\t\t\tConsidering " + potentialWorkers.Count + " workers after removing workers who worked max weekends.");
                        }

                        // Odbaci radnike kojima je dan d označen kao neradni.
                        // (9.bullet jakih)
                        removeByDaysOff(potentialWorkers, day);
                        Console.WriteLine("\t\t\tConsidering " + potentialWorkers.Count + " workers after removing workers who have this day off.");

                        var orderedWorkers = potentialWorkers.Values.OrderBy(w => w.RemainingMinutes).ToList();
                    }
                }
            }
        }

        private static void removeByShiftAlreadyAssignedToday(Dictionary<string, Worker> workers, int day)
        {
            List<string> toRemove = new List<string>();
            foreach (Worker w in workers.Values)
            {
                if (w.Assignments.ContainsKey(day))
                {
                    toRemove.Add(w.ID);
                }
            }
            foreach (string wr in toRemove)
            {
                workers.Remove(wr);
            }
        }

        private static void removeByShiftRotation(Dictionary<string, Worker> workers, int day, Shift shift)
        {
            if (day == 0)
            {
                return;
            }
            List<string> toRemove = new List<string>();
            foreach (Worker w in workers.Values)
            {
                if (w.Assignments.ContainsKey(day - 1))
                {
                    Assignment prevDayAssignment  = w.Assignments[day - 1];
                    if (prevDayAssignment.Shift.ProhibitsFollowingShifts.Contains(shift.ID))
                    {
                        toRemove.Add(w.ID);
                    }
                }
            }
            foreach (string wr in toRemove)
            {
                workers.Remove(wr);
            }
        }

        private static void removeByMaxTotalMinutes(Dictionary<string, Worker> workers, Shift shift)
        {
            List<string> toRemove = new List<string>();
            foreach (Worker w in workers.Values)
            {
                if (w.WorkedMinutes + shift.Length > w.MaxTotalMinutes)
                {
                    toRemove.Add(w.ID);
                }
            }
            foreach (string wr in toRemove)
            {
                workers.Remove(wr);
            }
        }

        private static void removeByMaxConsectiveShifts(Dictionary<string, Worker> workers, int day)
        {
            List<string> toRemove = new List<string>();
            foreach (Worker w in workers.Values)
            {
                var hasNotWorkedMaxShifts = false;
                for (var d = 1; d <= w.MaxConsecutiveShifts; d++)
                {
                    if (!w.Assignments.ContainsKey(day - d))
                    {
                        hasNotWorkedMaxShifts = true;
                        break;
                    }
                }
                if (!hasNotWorkedMaxShifts)
                {
                    toRemove.Add(w.ID);
                }
                
            }
            foreach (string wr in toRemove)
            {
                workers.Remove(wr);
            }
        }

        private static void removeByMinConsecutiveDaysOff(Dictionary<string, Worker> workers, int day)
        {
            List<string> toRemove = new List<string>();
            foreach (Worker w in workers.Values)
            {
                var hasNotHadEnoughDaysOff = false;
                for (var d = 1; d <= w.MinConsecutiveDaysOff; d++)
                {
                    if (w.Assignments.ContainsKey(day - d))
                    {
                        hasNotHadEnoughDaysOff = true;
                        break;
                    }
                }
                if (hasNotHadEnoughDaysOff)
                {
                    toRemove.Add(w.ID);
                }
            }
            foreach (string wr in toRemove)
            {
                workers.Remove(wr);
            }
        }

        private static void removeByMaxWeekends(Dictionary<string, Worker> workers)
        {
            List<string> toRemove = new List<string>();
            foreach (Worker w in workers.Values)
            {
                if (w.WorkedWeekends >= w.MaxWeekends)
                {
                    toRemove.Add(w.ID);
                }
            }
            foreach (string wr in toRemove)
            {
                workers.Remove(wr);
            }
        }

        private static void removeByDaysOff(Dictionary<string, Worker> workers, int day)
        {
            List<string> toRemove = new List<string>();
            foreach (Worker w in workers.Values)
            {
                if (w.DaysOff.Contains(day))
                {
                    toRemove.Add(w.ID);
                }
            }
            foreach (string wr in toRemove)
            {
                workers.Remove(wr);
            }
        }
    }
}
