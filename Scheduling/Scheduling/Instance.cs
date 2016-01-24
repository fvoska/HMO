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
            for (int day = 0; day < Days; day++)
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

                        // (3. bullet jakit ogr.)
                        removeByShiftType(potentialWorkers, shift.Value);
                        Console.WriteLine("\t\t\tConsidering " + potentialWorkers.Count + " workers after removing workers based on shift type.");

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

                        // Ako među preostalim korisnicima postoji jedan ili više korisnika za koje bi ako ne dobiju neku smjenu ovog dana bilo prekršeno ograničenje minimalnog broja uzastopnih radnih dana(smjena, 1 smjena == 1 dan) koje moraju odraditi za redom:
                        //      Odbaci sve ostale radnike i nastavi dalje samo s onima koji su u opasnosti da ne zadovolje ograničenje minimalnog broja uzastopnih radnih dana.
                        // (6.bullet jakih)
                        var tmp = checkForMinConsecutiveShifts(potentialWorkers, day);
                        if (tmp.Count > 0)
                        {
                            potentialWorkers = tmp;
                        }

                        if (potentialWorkers.Count > 0)
                        {
                            var workersByRemainingMinutes = potentialWorkers.Values.OrderBy(w => w.RemainingMinutes).ToList();
                            int depthLimit = 50;
                            int depth = 0;
                            float remMin = workersByRemainingMinutes.First().RemainingMinutes;
                            Worker aw = null;
                            foreach (Worker ww in workersByRemainingMinutes)
                            {
                                if (ww.ShiftOffRequests.ContainsKey(day))
                                {
                                    continue;
                                }
                                if (ww.RemainingMinutes != remMin)
                                {
                                    remMin = ww.RemainingMinutes;
                                    depth++;
                                }
                                if (depth > depthLimit)
                                {
                                    break;
                                }
                                if (ww.ShiftOnRequests.ContainsKey(day))
                                {
                                    aw = ww;
                                    break;
                                }
                            }
                            if (aw == null)
                            {
                                aw = workersByRemainingMinutes.First();
                            }
                            Assignment a = new Assignment();
                            a.Day = day;
                            a.Shift = Shifts[shift.Value.ID];
                            a.Worker = aw;
                            aw.Assignments[day] = a;
                            if (isWeekend)
                            {
                                aw.WorkedWeekends++;
                            }
                        }
                    }
                }
            }
            ExpandRight();
            //TryFill();
            //FixSingleDayBefore();
            //FixSingleDayAfter();
        }

        public static void ExpandRight()
        {
            foreach (Worker w in Workers.Values)
            {
                for (int day = 0; day < Days; day++)
                {
                    if (!w.Assignments.ContainsKey(day))
                    {
                        continue;
                    }
                    Shift lastShift = w.Assignments[day].Shift;
                    int numConsecutive = 1;
                    for (int d = day - 1; d >= 0; d--)
                    {
                        if (w.Assignments.ContainsKey(d))
                        {
                            numConsecutive++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    int numFree = 0;
                    while (true)
                    {
                        Console.WriteLine(day + " " + numFree);
                        if (day + numFree + 1 >= Days)
                        {
                            break;
                        }
                        if (w.Assignments.ContainsKey(day + numFree + 1))
                        {
                            break;
                        }
                        else
                        {
                            numFree++;
                        }
                    }
                    for (int d = day + 1; d < day + 1 + numFree - w.MinConsecutiveDaysOff; d++)
                    {
                        if ((d + 1) % 7 == 0 || (d + 2) % 7 == 0)
                        {
                            break;
                        }
                        if (w.DaysOff.Contains(d))
                        {
                            break;
                        }
                        if (numConsecutive >= w.MaxConsecutiveShifts)
                        {
                            break;
                        }
                        if (w.WorkedShiftsByType(lastShift.ID) >= w.MaxShifts[lastShift.ID])
                        {
                            break;
                        }
                        Assignment a = new Assignment();
                        a.Day = d;
                        a.Worker = w;
                        a.Shift = lastShift;
                        w.Assignments[d] = a;
                        numConsecutive++;
                    }
                }
            }
        }

        public static void TryFill()
        {
            foreach (Worker w in Workers.Values)
            {
                int sum = 0;
                int count = 0;
                foreach (string shiftID in w.MaxShifts.Keys)
                {
                    count++;
                    sum += Shifts[shiftID].Length;
                }
                int avgShiftLength = sum / count;
                int moreMinutes = w.MinTotalMinutes - w.WorkedMinutes + (w.MaxTotalMinutes - w.MinTotalMinutes) / 2;
                int moreShifts = moreMinutes / avgShiftLength;

                int moreGiven = 0;
                for (int day = 0; day < Days; day++)
                {
                    if (w.WorkedMinutes >= w.MinTotalMinutes)
                    {
                        break;
                    }
                    if (w.DaysOff.Contains(day))
                    {
                        continue;
                    }
                    if (!w.Assignments.ContainsKey(day))
                    {
                        int holeLeftLength = 0;
                        int dayBack = 1;
                        while (true)
                        {
                            if (w.DaysOff.Contains(day - dayBack))
                            {
                                break;
                            }
                            if (w.Assignments.ContainsKey(day - dayBack) || day - dayBack < 1)
                            {
                                break;
                            }
                            else
                            {
                                holeLeftLength++;
                            }
                            dayBack++;
                        }
                        int holeRightLength = 0;
                        int dayForward = 1;
                        while (true)
                        {
                            if (w.DaysOff.Contains(day + dayForward))
                            {
                                break;
                            }
                            if (w.Assignments.ContainsKey(day + dayForward) || day + dayForward > Days - 1)
                            {
                                break;
                            }
                            else
                            {
                                holeRightLength++;
                            }
                            dayForward++;
                        }

                        int holeLength = holeLeftLength + holeRightLength + 1;

                        int sectionLeftLength = 0;
                        dayBack = holeLeftLength + 1;
                        while (true)
                        {
                            if (w.DaysOff.Contains(day - dayBack))
                            {
                                break;
                            }
                            if (!w.Assignments.ContainsKey(day - dayBack) || day - dayBack < 0)
                            {
                                break;
                            }
                            else
                            {
                                sectionLeftLength++;
                            }
                            dayBack++;
                        }

                        int sectionRightLength = 0;
                        dayForward = holeRightLength + 1;
                        while (true)
                        {
                            if (w.DaysOff.Contains(day + dayForward))
                            {
                                break;
                            }
                            if (!w.Assignments.ContainsKey(day + dayForward) || day + dayForward > Days)
                            {
                                break;
                            }
                            else
                            {
                                sectionRightLength++;
                            }
                            dayForward++;
                        }

                        if (sectionLeftLength + holeLength + sectionRightLength <= w.MaxConsecutiveShifts)
                        {
                            List<Assignment> aas = new List<Assignment>();
                            int mw = 0;
                            int cnt = 0;
                            for (int i = day - holeLeftLength; i < day - holeLeftLength + holeLength; i++)
                            {
                                Assignment a = new Assignment();
                                a.Day = i;
                                a.Worker = w;
                                a.Shift = null;
                                if (w.ShiftOnRequests.ContainsKey(i))
                                {
                                    a.Shift = Shifts[w.ShiftOnRequests[i].ID];
                                }
                                else
                                {
                                    foreach (string shiftID in w.MaxShifts.Keys)
                                    {
                                        if (w.WorkedShiftsByType(shiftID) + 1 >= w.MaxShifts[shiftID])
                                        {
                                            continue;
                                        }
                                        else
                                        {
                                            Shift prevShift = null;
                                            if (aas.Count > 0)
                                            {
                                                prevShift = aas.Last().Shift;
                                            }
                                            else
                                            {
                                                if (w.Assignments.ContainsKey(day - holeLeftLength - 1)) {
                                                    prevShift = w.Assignments[day - holeLeftLength - 1].Shift;
                                                }
                                            }
                                            if (prevShift != null && !Shifts[prevShift.ID].ProhibitsFollowingShifts.Contains(shiftID))
                                            {
                                                if (w.Assignments.ContainsKey(day + holeRightLength + 1) && !w.Assignments[day + holeRightLength + 1].Shift.ProhibitsFollowingShifts.Contains(shiftID))
                                                {
                                                    a.Shift = Shifts[shiftID];
                                                    break;
                                                }
                                                else if (!w.Assignments.ContainsKey(day + holeRightLength + 1))
                                                {
                                                    a.Shift = Shifts[shiftID];
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                if (a.Shift != null)
                                {
                                    cnt++;
                                    aas.Add(a);
                                    bool isWeekend = false;
                                    if ((i + 1) % 7 == 0 || (i + 2) % 7 == 0)
                                    {
                                        mw++;
                                    }
                                    moreGiven++;
                                }
                            }
                            if (w.WorkedWeekends + mw > w.MaxWeekends)
                            {
                                continue;
                            }
                            else
                            {
                                foreach(Assignment a in aas)
                                {
                                    w.Assignments[a.Day] = a;
                                }
                            }
                        }
                    }                    
                }
            }
        }

        public static void FixSingleDayBefore()
        {
            for (int day = 0; day < Days; day++)
            {
                foreach (Worker w in Workers.Values)
                {
                    if (w.MinConsecutiveShifts > 1 && day > 0 && day < Days - 1)
                    {
                        if (w.Assignments.ContainsKey(day) && !w.Assignments.ContainsKey(day - 1) && !w.Assignments.ContainsKey(day + 1))
                        {
                            // Našli smo gdje je radio samo jedan dan.
                            int dayBack = 1;
                            while(true)
                            {
                                if (w.Assignments.ContainsKey(day - dayBack) || day - dayBack < 1)
                                {
                                    break;
                                }
                                dayBack++;
                            }
                            int howManyBack = dayBack - w.MinConsecutiveDaysOff;
                            int howManyNeed = w.MinConsecutiveShifts - 1;
                            for (int numBefore = 1; numBefore <= howManyBack - howManyNeed; numBefore++)
                            {
                                if (w.DaysOff.Contains(day - numBefore))
                                {
                                    break;
                                }
                                Assignment a = new Assignment();
                                a.Day = day - numBefore;
                                a.Worker = w;
                                a.Shift = null;
                                if (w.ShiftOnRequests.ContainsKey(day - numBefore))
                                {
                                    a.Shift = Shifts[w.ShiftOnRequests[day - numBefore].ID];
                                }
                                else
                                {
                                    foreach (string shiftID in Shifts.Keys)
                                    {
                                        if (w.WorkedShiftsByType(shiftID) + 1 >= w.MaxShifts[shiftID])
                                        {
                                            continue;
                                        }
                                        else
                                        {
                                            if (ShiftCanBeBefore(shiftID, w.Assignments[day - numBefore + 1].Shift.ID))
                                            {
                                                a.Shift = Shifts[shiftID];
                                                break;
                                            }
                                            else
                                            {
                                                continue;
                                            }
                                        }
                                    }
                                }
                                if (a.Shift != null)
                                {
                                    w.Assignments[day - numBefore] = a;
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void FixSingleDayAfter()
        {
            for (int day = 0; day < Days; day++)
            {
                foreach (Worker w in Workers.Values)
                {
                    if (w.MinConsecutiveShifts > 1 && day > 0 && day < Days - 1)
                    {
                        if (w.Assignments.ContainsKey(day) && !w.Assignments.ContainsKey(day - 1) && !w.Assignments.ContainsKey(day + 1))
                        {
                            // Našli smo gdje je radio samo jedan dan.
                            int dayForward = 1;
                            while (true)
                            {
                                if (w.Assignments.ContainsKey(day + dayForward) || day + dayForward >= Days)
                                {
                                    break;
                                }
                                dayForward++;
                            }
                            int howManyForward = dayForward - w.MinConsecutiveDaysOff;
                            int howManyNeed = w.MinConsecutiveShifts - 1;
                            for (int numAfter = 1; numAfter <= howManyForward - howManyNeed; numAfter++)
                            {
                                if (w.DaysOff.Contains(day + numAfter))
                                {
                                    break;
                                }
                                Assignment a = new Assignment();
                                a.Day = day + numAfter;
                                a.Worker = w;
                                a.Shift = null;
                                if (w.ShiftOnRequests.ContainsKey(day + numAfter))
                                {
                                    a.Shift = Shifts[w.ShiftOnRequests[day + numAfter].ID];
                                }
                                else
                                {
                                    foreach (string shiftID in Shifts.Keys)
                                    {
                                        if (w.WorkedShiftsByType(shiftID) + 1 >= w.MaxShifts[shiftID])
                                        {
                                            continue;
                                        }
                                        else
                                        {
                                            if (!w.Assignments[day + numAfter - 1].Shift.ProhibitsFollowingShifts.Contains(shiftID))
                                            {
                                                a.Shift = Shifts[shiftID];
                                                break;
                                            }
                                            else
                                            {
                                                continue;
                                            }
                                        }
                                    }
                                }
                                if (a.Shift != null)
                                {
                                    w.Assignments[day + numAfter] = a;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool ShiftCanBeBefore(string idDesired, string idAfter)
        {
            Shift desiredShift = Shifts[idDesired];
            if (desiredShift.ProhibitsFollowingShifts.Contains(idAfter))
            {
                return false;
            }
            else
            {
                return true;
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
                Console.WriteLine("\t\t\tRemoving " + wr);
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
                Console.WriteLine("\t\t\tRemoving " + wr);
                workers.Remove(wr);
            }
        }

        private static void removeByShiftType(Dictionary<string, Worker> workers, Shift shift)
        {
            List<string> toRemove = new List<string>();
            foreach (Worker w in workers.Values)
            {
                if (w.WorkedShiftsByType(shift.ID) >= w.MaxShifts[shift.ID])
                {
                    toRemove.Add(w.ID);
                }
            }
            foreach (string wr in toRemove)
            {
                Console.WriteLine("\t\t\tRemoving " + wr);
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
                Console.WriteLine("\t\t\tRemoving " + wr);
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
                Console.WriteLine("\t\t\tRemoving " + wr);
                workers.Remove(wr);
            }
        }

        private static void removeByMinConsecutiveDaysOff(Dictionary<string, Worker> workers, int day)
        {
            List<string> toRemove = new List<string>();
            foreach (Worker w in workers.Values)
            {
                if (!w.Assignments.ContainsKey(day - 1))
                {
                    // Nije radio je jučer, provjeri je li odmarao minimalni broj dana.

                    var hasNotHadEnoughDaysOff = false;
                    for (var d = 1; d <= w.MinConsecutiveDaysOff; d++)
                    {
                        if (day - d < 0)
                        {
                            break;
                        }
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
            }
            foreach (string wr in toRemove)
            {
                Console.WriteLine("\t\t\tRemoving " + wr);
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
                Console.WriteLine("\t\t\tRemoving " + wr);
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
                Console.WriteLine("\t\t\tRemoving " + wr);
                workers.Remove(wr);
            }
        }

        private static Dictionary<string, Worker> checkForMinConsecutiveShifts(Dictionary<string, Worker> workers, int day)
        {
            Dictionary<string, Worker> workers_out = new Dictionary<string, Worker>();
            bool someoneHasNotWorkedEnough = false;
            foreach (Worker w in workers.Values)
            {
                var hasHadEnoughConsecutiveDays = true;
                if (w.Assignments.ContainsKey(day - 1))
                {
                    for (var d = 1; d <= w.MinConsecutiveShifts; d++)
                    {
                        if (day - d < 0)
                        {
                            break;
                        }
                        if (!w.Assignments.ContainsKey(day - d))
                        {
                            hasHadEnoughConsecutiveDays = false;
                            workers_out[w.ID] = w;
                            break;
                        }
                    }
                    if (hasHadEnoughConsecutiveDays)
                    {
                        someoneHasNotWorkedEnough = true;
                        break;
                    }
                }
            }
            return workers_out;
        }

        
        private static void removeWhoWorkedMinConsecutiveShifts(Dictionary<string, Worker> workers, int day)
        {
            List<string> toRemove = new List<string>();
            foreach (Worker w in workers.Values)
            {
                var hasHadEnoughConsecutiveDays = true;
                if (w.Assignments.ContainsKey(day - 1))
                {
                    // Radio je jučer, provjeri je li radio minimalni broj dana.

                    for (var d = 1; d <= w.MinConsecutiveShifts; d++)
                    {
                        if (day - d < 0)
                        {
                            break;
                        }
                        if (!w.Assignments.ContainsKey(day - d))
                        {
                            hasHadEnoughConsecutiveDays = false;
                            break;
                        }
                    }
                    if (hasHadEnoughConsecutiveDays)
                    {
                        toRemove.Add(w.ID);
                    }
                }
            }
            foreach (string wr in toRemove)
            {
                Console.WriteLine("\t\t\tRemoving " + wr);
                workers.Remove(wr);
            }
        }
    }
}
