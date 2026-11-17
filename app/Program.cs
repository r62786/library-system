using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// The exam program. It gets the path of an input file, and prints the report.
// Run it from the exam folder:  python check/run_checks.py run
// Without a path, it reads data/sample.txt.
class Program
{



    static void Main(string[] args)
    {
        // Hebrew is printed as UTF-8, also on Windows
        Console.OutputEncoding = new UTF8Encoding(false);
        string path = args.Length > 0 ? args[0] : FindSample();
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        int reportDay = ReadReportDay(lines);
        List<string[]> rows = ReadRows(lines);
        Console.Error.WriteLine("skeleton ok: read " + Path.GetFileName(path) + " - report day " + reportDay + " - " + rows.Count + " rows");

        // Write your code here. 'reportDay' and 'rows' are ready.
        PrintReport(rows, reportDay);
    }

    static int ReadReportDay(string[] lines)
    {
        return int.Parse(lines[0].Split(':')[1].Trim());
    }

    // The lines after the two first ones, each split into its fields
    static List<string[]> ReadRows(string[] lines)
    {
        List<string[]> rows = new List<string[]>();
        for (int i = 2; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "") continue;
            string[] fields = lines[i].Split(',');
            for (int j = 0; j < fields.Length; j++) fields[j] = fields[j].Trim();
            rows.Add(fields);
        }
        return rows;
    }

    // Without a path, look for data/sample.txt next to the project, also when started from Visual Studio
    static string FindSample()
    {
        DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "data", "sample.txt");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return Path.Combine("data", "sample.txt");
    }

    static void PrintReport(List<string[]> rows, int reportDay)
    {
        Console.WriteLine("FINES");

        double studentTotal = 0;
        List<string[]> flaggedTeachers = new List<string[]>();

        for (int i = 0; i < rows.Count; i++)
        {
            if (!IsValidRow(rows[i]))
                continue;

            string[] row = rows[i];
            double fine = CalculateFine(row, reportDay);
            studentTotal += fine;
            Console.WriteLine(rows[i][0] + "," + rows[i][1] + "," +
                              fine.ToString("0", System.Globalization.CultureInfo.InvariantCulture));

            if (IsFlaggedTeacher(row, reportDay))
                flaggedTeachers.Add(row);
        }

        Console.WriteLine("FLAGGED");
        for (int i = 0; i < flaggedTeachers.Count; i++)
            Console.WriteLine(flaggedTeachers[i][0] + "," + flaggedTeachers[i][1]);

        Console.WriteLine("ERRORS");
        Console.WriteLine("TOTAL," + studentTotal.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
    }

    static bool IsValidRow(string[] row)
    {
        return row != null && row.Length >= 6 && IsSupportedReaderType(row[2]);
    }

    static bool IsSupportedReaderType(string readerType)
    {
        return readerType.Equals("student", StringComparison.OrdinalIgnoreCase) ||
               readerType.Equals("teacher", StringComparison.OrdinalIgnoreCase) ||
               readerType.Equals("guest", StringComparison.OrdinalIgnoreCase);
    }

    static double CalculateFine(string[] row, int reportDay)
    {
        return row[2].ToLowerInvariant() switch
        {
            "student" => CalculateStudentFine(row, reportDay),
            "guest" => CalculateGuestFine(row, reportDay),
            _ => 0
        };
    }

    static double CalculateStudentFine(string[] row, int reportDay)
    {
        const int StudentFreeDays = 14;
        const int StudentGraceDays = 1;
        const int StudentFineLimit = 30;

        int borrowedDays = GetBorrowedDays(row, reportDay);
        int chargeableDays = borrowedDays - StudentFreeDays - StudentGraceDays;

        return Math.Min(StudentFineLimit, Math.Max(0, chargeableDays));
    }

    static double CalculateGuestFine(string[] row, int reportDay)
    {
        const int GuestFreeDays = 7;
        const double GuestDailyFine = 2.5;

        int borrowedDays = GetBorrowedDays(row, reportDay);
        int chargeableDays = Math.Max(0, borrowedDays - GuestFreeDays);

        return Math.Ceiling(chargeableDays * GuestDailyFine);
    }

    static int GetBorrowedDays(string[] row, int reportDay)
    {
        int loanDay = int.Parse(row[4]);
        int endDay = string.IsNullOrWhiteSpace(row[5]) ? reportDay : int.Parse(row[5]);
        return endDay - loanDay;
    }

    static bool IsFlaggedTeacher(string[] row, int reportDay)
    {
        const int TeacherFlaggedAfterDays = 60;

        return row[2].Equals("teacher", StringComparison.OrdinalIgnoreCase) &&
               string.IsNullOrWhiteSpace(row[5]) &&
               GetBorrowedDays(row, reportDay) > TeacherFlaggedAfterDays;
    }
}
