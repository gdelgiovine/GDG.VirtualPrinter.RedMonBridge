namespace GDG.VirtualPrinter.Core;

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class PrintJobMetadata
{
    // PSA metadata
    public string JobTitle { get; set; } = string.Empty;
    public string WorkflowSessionId { get; set; } = string.Empty;
    public string SourceAppDisplayName { get; set; } = string.Empty;
    public string PrinterName { get; set; } = "GDG Virtual Printer";
    public string PrinterUri { get; set; } = "gdg-virtual-printer:oxps";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    // Win32 spooler metadata (RedMon compatibility)
    public uint? SpoolerJobId { get; set; }
    public string SubmitterUser { get; set; } = string.Empty;
    public string SourceMachine { get; set; } = string.Empty;
    public string SpoolerDocumentName { get; set; } = string.Empty;
    public DateTimeOffset? SpoolerSubmittedUtc { get; set; }

    // RDS / Terminal Services metadata
    public int? RdsSessionId { get; set; }
    public string RdsSessionName { get; set; } = string.Empty;
    public string RdsClientName { get; set; } = string.Empty;
    public bool? IsRemoteSession { get; set; }

    // Diagnostics
    public string SpoolerResolutionStatus { get; set; } = string.Empty;
    public string RdsResolutionStatus { get; set; } = string.Empty;

    // Compatibility alias retained for code that used the old v0.3 name.
    public string SessionId
    {
        get => WorkflowSessionId;
        set => WorkflowSessionId = value ?? string.Empty;
    }

    public static async Task<PrintJobMetadata?> LoadAsync(string path)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<PrintJobMetadata>(stream, JsonOptions);
    }

    public async Task SaveAsync(string path)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, this, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
