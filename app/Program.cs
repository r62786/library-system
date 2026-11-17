using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
        PrintReport(rows, ReadRawRows(lines), reportDay);
    }

    static int ReadReportDay(string[] lines)
    {
        return int.Parse(lines[0].Split(':')[1].Trim());
    }

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

    static List<string> ReadRawRows(string[] lines)
    {
        List<string> rawRows = new List<string>();
        for (int i = 2; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                rawRows.Add(lines[i]);
        }
        return rawRows;
    }

    static void PrintReport(List<string[]> rows, List<string> rawRows, int reportDay)
    {
        Dictionary<string, ReaderSummary> readerFines = new Dictionary<string, ReaderSummary>();
        List<string> readerOrder = new List<string>();
        Dictionary<string, int> bookCounts = new Dictionary<string, int>();
        List<string> flaggedReaderIds = new List<string>();
        List<string> errors = new List<string>();

        for (int i = 0; i < rows.Count; i++)
        {
            string[] row = rows[i];
            ReaderType readerType;

            if (!TryValidateRow(row, out readerType))
            {
                errors.Add(rawRows[i]);
                continue;
            }

            string readerId = row[0];
            if (!readerFines.ContainsKey(readerId))
            {
                readerFines[readerId] = new ReaderSummary(readerId, row[1]);
                readerOrder.Add(readerId);
            }

            double fine = CalculateFine(row, reportDay);
            readerFines[readerId].AddLoan(fine);
            bookCounts[row[3]] = bookCounts.ContainsKey(row[3]) ? bookCounts[row[3]] + 1 : 1;

            if (IsFlaggedTeacher(row, readerType, reportDay) && !flaggedReaderIds.Contains(readerId))
                flaggedReaderIds.Add(readerId);
        }

        Console.WriteLine("FINES");
        foreach (string readerId in readerOrder)
        {
            ReaderSummary reader = readerFines[readerId];
            Console.WriteLine(reader.ReaderId + "," + reader.Name + "," + FormatNumber(reader.TotalFine));
        }

        Console.WriteLine("FLAGGED");
        foreach (string readerId in flaggedReaderIds)
        {
            ReaderSummary reader = readerFines[readerId];
            Console.WriteLine(reader.ReaderId + "," + reader.Name);
        }

        Console.WriteLine("ERRORS");
        foreach (string error in errors)
            Console.WriteLine(error);

        double total = readerFines.Values.Sum(reader => reader.TotalFine);
        Console.WriteLine("TOTAL," + FormatNumber(total));

        Console.WriteLine("TOP_BOOKS");
        Console.WriteLine("book,count");
        foreach (KeyValuePair<string, int> book in bookCounts
                     .OrderByDescending(book => book.Value)
                     .ThenBy(book => book.Key)
                     .Take(3))
            Console.WriteLine(book.Key + "," + book.Value);

        Console.WriteLine("AT_RISK");
        foreach (string readerId in readerOrder)
        {
            ReaderSummary reader = readerFines[readerId];
            if (reader.LoansWithFine > 2)
                Console.WriteLine(reader.ReaderId + "," + reader.Name);
        }
    }

    static string FormatNumber(double number)
    {
        return number.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
    }

    static bool TryValidateRow(string[] row, out ReaderType readerType)
    {
        readerType = ReaderType.Unknown;

        if (row == null || row.Length != 6)
            return false;

        if (string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]) ||
            string.IsNullOrWhiteSpace(row[2]) || string.IsNullOrWhiteSpace(row[3]) ||
            string.IsNullOrWhiteSpace(row[4]))
            return false;

        readerType = ParseReaderType(row[2]);
        if (readerType == ReaderType.Unknown)
            return false;

        if (!int.TryParse(row[0], out _))
            return false;

        if (!int.TryParse(row[4], out int loanDay))
            return false;

        if (!string.IsNullOrWhiteSpace(row[5]) &&
            (!int.TryParse(row[5], out int returnDay) || returnDay < loanDay))
            return false;

        return true;
    }

    static ReaderType ParseReaderType(string typeString)
    {
        return typeString?.ToLowerInvariant() switch
        {
            "student" => ReaderType.Student,
            "teacher" => ReaderType.Teacher,
            "guest" => ReaderType.Guest,
            "alumna" => ReaderType.Alumna,
            _ => ReaderType.Unknown
        };
    }

    static double CalculateFine(string[] row, int reportDay)
    {
        ReaderType type = ParseReaderType(row[2]);
        return type switch
        {
            ReaderType.Student => CalculateStudentFine(row, reportDay),
            ReaderType.Guest => CalculateGuestFine(row, reportDay),
            ReaderType.Alumna => CalculateAlumnaFine(row, reportDay),
            _ => 0
        };
    }

    static double CalculateStudentFine(string[] row, int reportDay)
    {
        const int FreeDays = 14;
        const int GraceDays = 1;
        const int MaxFine = 40;

        int borrowedDays = GetBorrowedDays(row, reportDay);
        int chargeableDays = borrowedDays - FreeDays - GraceDays;

        return Math.Min(MaxFine, Math.Max(0, chargeableDays));
    }

    static double CalculateGuestFine(string[] row, int reportDay)
    {
        const int FreeDays = 7;
        const double DailyFine = 2.5;
        const int MaxFine = 0;

        int borrowedDays = GetBorrowedDays(row, reportDay);
        int chargeableDays = Math.Max(0, borrowedDays - FreeDays);

        return Math.Ceiling(chargeableDays * DailyFine);
    }

    static double CalculateAlumnaFine(string[] row, int reportDay)
    {
        const int FreeDays = 10;
        const double DailyFine = 1.5;
        const int MaxFine = 25;

        int borrowedDays = GetBorrowedDays(row, reportDay);
        int chargeableDays = Math.Max(0, borrowedDays - FreeDays);
        double fine = Math.Ceiling(chargeableDays * DailyFine);

        return Math.Min(MaxFine, fine);
    }

    static int GetBorrowedDays(string[] row, int reportDay)
    {
        int loanDay = int.Parse(row[4]);
        int returnDay = string.IsNullOrWhiteSpace(row[5]) ? reportDay : int.Parse(row[5]);
        return returnDay - loanDay;
    }

    static bool IsFlaggedTeacher(string[] row, ReaderType readerType, int reportDay)
    {
        const int TeacherThresholdDays = 60;

        return readerType == ReaderType.Teacher &&
               string.IsNullOrWhiteSpace(row[5]) &&
               GetBorrowedDays(row, reportDay) > TeacherThresholdDays;
    }
}

enum ReaderType
{
    Unknown,
    Student,
    Teacher,
    Guest,
    Alumna
}

class ReaderSummary
{
    public string ReaderId { get; }
    public string Name { get; }
    public double TotalFine { get; set; }
    public int LoansWithFine { get; set; }

    public ReaderSummary(string readerId, string name)
    {
        ReaderId = readerId;
        Name = name;
    }

    public void AddLoan(double fine)
    {
        TotalFine += fine;
        if (fine > 0)
            LoansWithFine++;
    }
}
