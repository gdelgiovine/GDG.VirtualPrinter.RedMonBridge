namespace GDG.VirtualPrinter.Launcher;

using GDG.VirtualPrinter.Core;
using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using Windows.Storage;

internal static class Program
{
    private static async Task<int> Main()
    {
        StorageFolder cache;
        try
        {
            cache = BridgePaths.GetPublisherCacheFolder();
        }
        catch
        {
            return 10;
        }

        string cacheSource = Path.Combine(cache.Path, BridgePaths.SourceFileName);
        string cacheMetadata = Path.Combine(cache.Path, BridgePaths.MetadataFileName);
        string cacheLock = Path.Combine(cache.Path, BridgePaths.LockFileName);

        if (!File.Exists(cacheSource))
        {
            SafeDelete(cacheLock);
            return 11;
        }

        PrintJobMetadata? metadata = null;
        string? finalOxps = null;

        try
        {
            metadata = await PrintJobMetadata.LoadAsync(cacheMetadata);

            if (metadata is not null &&
                !string.Equals(metadata.SpoolerResolutionStatus, "Resolved", StringComparison.OrdinalIgnoreCase))
            {
                // Full-trust fallback in case the packaged background task could not query the spooler.
                SpoolerJobResolver.Resolve(metadata);
            }

            var settings = BridgeSettings.LoadOrCreate();
            Directory.CreateDirectory(settings.JobsDirectory);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            var baseName = SanitizeBaseName(metadata?.JobTitle);
            finalOxps = Path.Combine(
                settings.JobsDirectory,
                $"{stamp}_{baseName}_{Guid.NewGuid():N}.oxps");

            File.Move(cacheSource, finalOxps);

            SafeDelete(cacheMetadata);
            SafeDelete(cacheLock);

            string? finalXps = null;
            string processorFile = finalOxps;

            if (settings.OutputFormat is ProcessorOutputFormat.Xps or ProcessorOutputFormat.Both)
            {
                finalXps = Path.ChangeExtension(finalOxps, ".xps");
                XpsFormatConverter.ConvertOxpsToXps(finalOxps, finalXps);

                if (settings.OutputFormat == ProcessorOutputFormat.Xps)
                    processorFile = finalXps;
            }

            if (!string.IsNullOrWhiteSpace(settings.ExecutablePath))
            {
                using var process = StartReceiver(
                    settings,
                    metadata,
                    processorFile,
                    finalOxps,
                    finalXps);

                if (process is not null)
                    await process.WaitForExitAsync();
            }

            if (!settings.KeepOxps &&
                settings.OutputFormat == ProcessorOutputFormat.Xps &&
                File.Exists(finalOxps))
            {
                File.Delete(finalOxps);
            }

            return 0;
        }
        catch
        {
            SafeDelete(cacheLock);
            return 12;
        }
    }

private static Process? StartReceiver(
    BridgeSettings settings,
    PrintJobMetadata? metadata,
    string processorFile,
    string oxpsPath,
    string? xpsPath)
{
    if (!File.Exists(settings.ExecutablePath))
        throw new FileNotFoundException(
            "Configured executable was not found.",
            settings.ExecutablePath);

    string workingDirectory =
        string.IsNullOrWhiteSpace(settings.WorkingDirectory)
            ? Path.GetDirectoryName(settings.ExecutablePath) ?? string.Empty
            : settings.WorkingDirectory;

    string runAsUser =
        WindowsIdentity.GetCurrent()?.Name ?? Environment.UserName;

    var psi = new ProcessStartInfo
    {
        FileName = settings.ExecutablePath,
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden,
        WorkingDirectory = workingDirectory,
        LoadUserProfile = true
    };

    if (!string.IsNullOrWhiteSpace(settings.Arguments))
        psi.Arguments = settings.Arguments;

    if (settings.RunAsMode == RunAsMode.SpecificAccount)
    {
        var credential = CredentialManager.Read(settings.CredentialTarget)
            ?? throw new InvalidOperationException(
                $"Credential '{settings.CredentialTarget}' was not found.");

        string account = string.IsNullOrWhiteSpace(settings.RunAsUser)
            ? credential.UserName
            : settings.RunAsUser;

        runAsUser = account;

        PopulateRedMonEnvironment(
            psi,
            settings,
            metadata,
            processorFile,
            oxpsPath,
            xpsPath,
            runAsUser);

        var environment =
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in psi.Environment)
            environment[pair.Key] = pair.Value;

        using var process = NativeProcessLauncher.StartHiddenWithLogon(
            settings.ExecutablePath,
            settings.Arguments,
            workingDirectory,
            account,
            credential.Password,
            environment);

        process.WaitForExit();
        return null;
    }

    PopulateRedMonEnvironment(
        psi,
        settings,
        metadata,
        processorFile,
        oxpsPath,
        xpsPath,
        runAsUser);

    return Process.Start(psi);
}

    private static void PopulateRedMonEnvironment(
        ProcessStartInfo psi,
        BridgeSettings settings,
        PrintJobMetadata? metadata,
        string processorFile,
        string oxpsPath,
        string? xpsPath,
        string runAsUser)
    {
        string docName = metadata?.JobTitle ?? string.Empty;
        string baseName = SanitizeBaseName(docName);
        string printUser = !string.IsNullOrWhiteSpace(metadata?.SubmitterUser)
            ? metadata!.SubmitterUser
            : GetPrintUser();

        string sourceMachine = !string.IsNullOrWhiteSpace(metadata?.SourceMachine)
            ? metadata!.SourceMachine
            : Environment.MachineName;

        string redMonJob = metadata?.SpoolerJobId?.ToString(CultureInfo.InvariantCulture)
            ?? metadata?.WorkflowSessionId
            ?? string.Empty;

        string rdsSession = metadata?.RdsSessionId?.ToString(CultureInfo.InvariantCulture)
            ?? Process.GetCurrentProcess().SessionId.ToString(CultureInfo.InvariantCulture);

        psi.Environment["REDMON_PORT"] = settings.RedMonPort;
        psi.Environment["REDMON_JOB"] = redMonJob;
        psi.Environment["REDMON_PRINTER"] = settings.RedMonPrinter;
        psi.Environment["REDMON_OUTPUTPRINTER"] = settings.RedMonOutputPrinter;
        psi.Environment["REDMON_MACHINE"] = sourceMachine;
        psi.Environment["REDMON_USER"] = printUser;
        psi.Environment["REDMON_DOCNAME"] = docName;
        psi.Environment["REDMON_BASENAME"] = baseName;
        psi.Environment["REDMON_FILENAME"] = processorFile;
        psi.Environment["REDMON_SESSIONID"] = rdsSession;

        psi.Environment["GDG_RUNAS_USER"] = runAsUser;
        psi.Environment["GDG_SOURCE_APP"] = metadata?.SourceAppDisplayName ?? string.Empty;
        psi.Environment["GDG_WORKFLOW_SESSION_ID"] = metadata?.WorkflowSessionId ?? string.Empty;
        psi.Environment["GDG_SPOOLER_JOB_ID"] = metadata?.SpoolerJobId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        psi.Environment["GDG_RDS_SESSION_ID"] = metadata?.RdsSessionId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        psi.Environment["GDG_RDS_SESSION_NAME"] = metadata?.RdsSessionName ?? string.Empty;
        psi.Environment["GDG_RDS_CLIENT_NAME"] = metadata?.RdsClientName ?? string.Empty;
        psi.Environment["GDG_IS_REMOTE_SESSION"] = metadata?.IsRemoteSession?.ToString() ?? string.Empty;
        psi.Environment["GDG_SPOOLER_RESOLUTION"] = metadata?.SpoolerResolutionStatus ?? string.Empty;
        psi.Environment["GDG_RDS_RESOLUTION"] = metadata?.RdsResolutionStatus ?? string.Empty;
        psi.Environment["GDG_OXPS_FILENAME"] = oxpsPath;
        psi.Environment["GDG_XPS_FILENAME"] = xpsPath ?? string.Empty;
        psi.Environment["GDG_PROCESSOR_FILENAME"] = processorFile;
        psi.Environment["GDG_OUTPUT_FORMAT"] = settings.OutputFormat.ToString();
        psi.Environment["GDG_PRINTER_URI"] = metadata?.PrinterUri ?? string.Empty;
    }

    private static string GetPrintUser()
    {
        try
        {
            return WindowsIdentity.GetCurrent()?.Name ?? Environment.UserName;
        }
        catch
        {
            return Environment.UserName;
        }
    }

    private static string SanitizeBaseName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "printjob";

        var name = Path.GetFileNameWithoutExtension(value.Trim());

        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        if (name.Length > 80)
            name = name[..80];

        return string.IsNullOrWhiteSpace(name) ? "printjob" : name;
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}
