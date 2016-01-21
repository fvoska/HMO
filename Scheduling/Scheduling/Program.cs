using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scheduling
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Importing problem instance...");

            // Static class members hold information about parsed data.
            Instance parser = null;

            // instanca.txt should be copied to build folder (copied automatically if building in VS, not sure about other IDEs/compilers).
            StreamReader file = new StreamReader("instanca.txt");
            string line;
            string currentSection = "";            

            // Parsing.
            while ((line = file.ReadLine()) != null)
            {
                if (line.Trim().Length == 0)
                {
                    // Skip empty lines.
                    continue;
                }
                else if (line.Trim()[0] == '#')
                {
                    // Skip comments.
                    continue;
                }
                else if (line.Contains("SECTION_"))
                {
                    // Set parser for current section.
                    currentSection = line.Split(new string[] { "SECTION_" }, StringSplitOptions.None)[1];
                    Console.WriteLine("Parsing section \"" + currentSection + "\"");
                    parser = new Instance(currentSection);
                }
                else
                {
                    // Parse section part.
                    if (parser != null)
                    {
                        parser.ParseLine(line);
                    }
                }
            }

            file.Close();

            // Some basic statistics.
            Console.WriteLine();
            Console.WriteLine("== SUMMARY ==");
            Console.WriteLine("Number of days to schedule: " + Instance.Days);
            Console.WriteLine("Number of shift types: " + Instance.Shifts.Count);
            foreach (var shift in Instance.Shifts)
            {
                Console.WriteLine("\tShift: " + shift.Value.ID);
                Console.Write("\t\tShifts which can not follow this shift: ");
                if (shift.Value.ProhibitsFollowingShifts.Count == 0)
                {
                    Console.Write("-");
                }
                else
                {
                    Console.Write(string.Join(", ", shift.Value.ProhibitsFollowingShifts));
                }
                Console.WriteLine();
            }
            Console.WriteLine("Number of staff: " + Instance.Workers.Count);

            int totalDaysOff = Instance.Workers.Sum(s => s.Value.DaysOff.Count);
            Console.WriteLine("Average (per worker) number of days off requested: " + Math.Round((double) totalDaysOff / Instance.Workers.Count, 2));
            int totalShiftOn = Instance.Workers.Sum(s => s.Value.ShiftOnRequests.Count);
            Console.WriteLine("Average (per worker) number of shift on requests: " + Math.Round((double) totalShiftOn / Instance.Workers.Count, 2));
            int totalShiftOff = Instance.Workers.Sum(s => s.Value.ShiftOffRequests.Count);
            Console.WriteLine("Average (per worker) number of shift off requests: " + Math.Round((double) totalShiftOff / Instance.Workers.Count, 2));
            int sumDailyRequirement = Instance.DailyRequirements.Sum(s => s.Value.Sum(r => r.Requirement));
            Console.WriteLine("Average (per day) number of workers needed (for all shifts together): " + Math.Round((double) sumDailyRequirement / Instance.Days, 2));

            Instance.Assign();

            Console.ReadLine();
        }
    }
}
