namespace GDG.VirtualPrinter.Core;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

public static class SpoolerJobResolver
{
    public static void Resolve(PrintJobMetadata metadata)
    {
        if (metadata is null) throw new ArgumentNullException(nameof(metadata));

        try
        {
            var jobs = EnumerateJobs(metadata.PrinterName);
            if (jobs.Count == 0)
            {
                metadata.SpoolerResolutionStatus = "NoJobs";
                return;
            }

            string wanted = NormalizeDocument(metadata.JobTitle);

            var candidates = jobs
                .Select(j => new
                {
                    Job = j,
                    DocumentScore = ScoreDocument(wanted, NormalizeDocument(j.Document)),
                    Age = GetAge(metadata.CreatedUtc, j.SubmittedLocal)
                })
                .Where(x => x.DocumentScore > 0 && x.Age <= TimeSpan.FromMinutes(5))
                .OrderByDescending(x => x.DocumentScore)
                .ThenBy(x => x.Age)
                .ThenByDescending(x => x.Job.JobId)
                .ToList();

            if (candidates.Count == 0)
            {
                metadata.SpoolerResolutionStatus = "NoMatchingJob";
                return;
            }

            var best = candidates[0];
            bool ambiguous = candidates.Count > 1 &&
                             candidates[1].DocumentScore == best.DocumentScore &&
                             Math.Abs((candidates[1].Age - best.Age).TotalSeconds) < 2;

            if (ambiguous)
            {
                metadata.SpoolerResolutionStatus = "Ambiguous";
                return;
            }

            metadata.SpoolerJobId = best.Job.JobId;
            metadata.SubmitterUser = best.Job.UserName;
            metadata.SourceMachine = best.Job.MachineName;
            metadata.SpoolerDocumentName = best.Job.Document;
            metadata.SpoolerSubmittedUtc = best.Job.SubmittedLocal.ToUniversalTime();
            metadata.SpoolerResolutionStatus = "Resolved";

            RdsSessionResolver.Resolve(metadata);
        }
        catch (Exception ex)
        {
            metadata.SpoolerResolutionStatus = "Error:" + ex.GetType().Name;
        }
    }

    private static List<JobSnapshot> EnumerateJobs(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return new List<JobSnapshot>();

        if (!OpenPrinter(printerName, out IntPtr printer, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            EnumJobs(printer, 0, 999, 2, IntPtr.Zero, 0, out uint needed, out _);

            if (needed == 0)
                return new List<JobSnapshot>();

            IntPtr buffer = Marshal.AllocHGlobal(checked((int)needed));
            try
            {
                if (!EnumJobs(printer, 0, 999, 2, buffer, needed, out _, out uint returned))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                int size = Marshal.SizeOf<JOB_INFO_2>();
                var result = new List<JobSnapshot>(checked((int)returned));

                for (int i = 0; i < returned; i++)
                {
                    IntPtr ptr = IntPtr.Add(buffer, i * size);
                    var info = Marshal.PtrToStructure<JOB_INFO_2>(ptr);

                    result.Add(new JobSnapshot
                    {
                        JobId = info.JobId,
                        PrinterName = Ptr(info.pPrinterName),
                        MachineName = Ptr(info.pMachineName),
                        UserName = Ptr(info.pUserName),
                        Document = Ptr(info.pDocument),
                        SubmittedLocal = ToDateTime(info.Submitted)
                    });
                }

                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            ClosePrinter(printer);
        }
    }

    private static string Ptr(IntPtr p)
        => p == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUni(p) ?? string.Empty;

    private static string NormalizeDocument(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string s = value.Trim().ToUpperInvariant();
        const string WordPrefix = "MICROSOFT WORD - ";
        if (s.StartsWith(WordPrefix, StringComparison.Ordinal))
            s = s[WordPrefix.Length..];

        return s;
    }

    private static int ScoreDocument(string wanted, string actual)
    {
        if (string.IsNullOrEmpty(wanted) || string.IsNullOrEmpty(actual))
            return 0;
        if (string.Equals(wanted, actual, StringComparison.OrdinalIgnoreCase))
            return 100;
        if (actual.Contains(wanted, StringComparison.OrdinalIgnoreCase) ||
            wanted.Contains(actual, StringComparison.OrdinalIgnoreCase))
            return 70;

        string wantedFile = System.IO.Path.GetFileName(wanted);
        string actualFile = System.IO.Path.GetFileName(actual);
        if (!string.IsNullOrEmpty(wantedFile) &&
            string.Equals(wantedFile, actualFile, StringComparison.OrdinalIgnoreCase))
            return 80;

        return 0;
    }

    private static TimeSpan GetAge(DateTimeOffset createdUtc, DateTime submittedLocal)
    {
        var submitted = new DateTimeOffset(submittedLocal).ToUniversalTime();
        var delta = createdUtc - submitted;
        return delta < TimeSpan.Zero ? -delta : delta;
    }

    private static DateTime ToDateTime(SYSTEMTIME s)
    {
        try
        {
            return new DateTime(
                s.wYear, s.wMonth, s.wDay,
                s.wHour, s.wMinute, s.wSecond, s.wMilliseconds,
                DateTimeKind.Local);
        }
        catch
        {
            return DateTime.Now;
        }
    }

    private sealed class JobSnapshot
    {
        public uint JobId { get; init; }
        public string PrinterName { get; init; } = string.Empty;
        public string MachineName { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string Document { get; init; } = string.Empty;
        public DateTime SubmittedLocal { get; init; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JOB_INFO_2
    {
        public uint JobId;
        public IntPtr pPrinterName;
        public IntPtr pMachineName;
        public IntPtr pUserName;
        public IntPtr pDocument;
        public IntPtr pNotifyName;
        public IntPtr pDatatype;
        public IntPtr pPrintProcessor;
        public IntPtr pParameters;
        public IntPtr pDriverName;
        public IntPtr pDevMode;
        public IntPtr pStatus;
        public IntPtr pSecurityDescriptor;
        public uint Status;
        public uint Priority;
        public uint Position;
        public uint StartTime;
        public uint UntilTime;
        public uint TotalPages;
        public uint Size;
        public SYSTEMTIME Submitted;
        public uint Time;
        public uint PagesPrinted;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW",
        SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(
        string pPrinterName,
        out IntPtr phPrinter,
        IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EnumJobsW",
        SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumJobs(
        IntPtr hPrinter,
        uint firstJob,
        uint noJobs,
        uint level,
        IntPtr pJob,
        uint cbBuf,
        out uint pcbNeeded,
        out uint pcReturned);
}
