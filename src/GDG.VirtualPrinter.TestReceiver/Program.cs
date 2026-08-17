using System;
using System.IO;
using System.Text;

namespace GDG.VirtualPrinter.TestReceiver
{
    internal static class Program
    {
        private static readonly string[] Variables =
        {
            "REDMON_PORT",
            "REDMON_JOB",
            "REDMON_PRINTER",
            "REDMON_OUTPUTPRINTER",
            "REDMON_MACHINE",
            "REDMON_USER",
            "REDMON_DOCNAME",
            "REDMON_BASENAME",
            "REDMON_FILENAME",
            "REDMON_SESSIONID",
            "TEMP",
            "TMP",
            "GDG_RUNAS_USER",
            "GDG_SOURCE_APP",
            "GDG_WORKFLOW_SESSION_ID",
            "GDG_SPOOLER_JOB_ID",
            "GDG_RDS_SESSION_ID",
            "GDG_RDS_SESSION_NAME",
            "GDG_RDS_CLIENT_NAME",
            "GDG_IS_REMOTE_SESSION",
            "GDG_SPOOLER_RESOLUTION",
            "GDG_RDS_RESOLUTION",
            "GDG_OXPS_FILENAME",
            "GDG_XPS_FILENAME",
            "GDG_PROCESSOR_FILENAME",
            "GDG_OUTPUT_FORMAT",
            "GDG_PRINTER_URI"
        };

        private static int Main()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "GDG", "VirtualPrinter", "Logs");

            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir,
                "receiver_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");

            var sb = new StringBuilder();
            sb.AppendLine("GDG Virtual Printer - RedMon environment test");
            sb.AppendLine("Timestamp: " + DateTime.Now.ToString("O"));
            sb.AppendLine();

            foreach (string name in Variables)
                sb.AppendLine(name + "=" + (Environment.GetEnvironmentVariable(name) ?? ""));

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return 0;
        }
    }
}
