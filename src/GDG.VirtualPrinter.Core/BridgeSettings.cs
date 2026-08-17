namespace GDG.VirtualPrinter.Core;

using System.IO;
using System.Text.Json;

public enum RunAsMode
{
    CurrentUser = 0,
    SpecificAccount = 1
}

public enum ProcessorOutputFormat
{
    Oxps = 0,
    Xps = 1,
    Both = 2
}

public sealed class BridgeSettings
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string JobsDirectory { get; set; } = string.Empty;
    public bool KeepOxps { get; set; } = true;
    public ProcessorOutputFormat OutputFormat { get; set; } = ProcessorOutputFormat.Xps;

    public RunAsMode RunAsMode { get; set; } = RunAsMode.CurrentUser;
    public string RunAsUser { get; set; } = string.Empty;
    public string CredentialTarget { get; set; } = "GDG.VirtualPrinter.RunAs";

    public string RedMonPort { get; set; } = "GDGVP1:";
    public string RedMonPrinter { get; set; } = "GDG Virtual Printer";
    public string RedMonOutputPrinter { get; set; } = string.Empty;

    public static BridgeSettings LoadOrCreate()
    {
        BridgePaths.EnsureProgramDataFolders();
        var path = BridgePaths.GetConfigurationFile();

        if (!File.Exists(path))
        {
            var settings = new BridgeSettings
            {
                JobsDirectory = BridgePaths.GetDefaultJobsFolder()
            };
            settings.Save();
            return settings;
        }

        try
        {
            var json = File.ReadAllText(path);
            var value = JsonSerializer.Deserialize<BridgeSettings>(json, JsonOptions) ?? new BridgeSettings();

            if (string.IsNullOrWhiteSpace(value.JobsDirectory))
                value.JobsDirectory = BridgePaths.GetDefaultJobsFolder();

            if (string.IsNullOrWhiteSpace(value.CredentialTarget))
                value.CredentialTarget = "GDG.VirtualPrinter.RunAs";

            return value;
        }
        catch
        {
            return new BridgeSettings
            {
                JobsDirectory = BridgePaths.GetDefaultJobsFolder()
            };
        }
    }

    public void Save()
    {
        BridgePaths.EnsureProgramDataFolders();

        if (string.IsNullOrWhiteSpace(JobsDirectory))
            JobsDirectory = BridgePaths.GetDefaultJobsFolder();

        if (string.IsNullOrWhiteSpace(CredentialTarget))
            CredentialTarget = "GDG.VirtualPrinter.RunAs";

        Directory.CreateDirectory(JobsDirectory);

        File.WriteAllText(
            BridgePaths.GetConfigurationFile(),
            JsonSerializer.Serialize(this, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
