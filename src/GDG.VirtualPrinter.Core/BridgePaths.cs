namespace GDG.VirtualPrinter.Core;

using System;
using System.IO;
using Windows.Storage;

public static class BridgePaths
{
    public const string PublisherCacheFolderName = "printing";
    public const string SourceFileName = "source.oxps";
    public const string MetadataFileName = "metadata.json";
    public const string LockFileName = "job.lock";

    public static StorageFolder GetPublisherCacheFolder()
        => ApplicationData.Current.GetPublisherCacheFolder(PublisherCacheFolderName);

    public static string GetProgramDataRoot()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "GDG", "VirtualPrinter");

    public static string GetDefaultJobsFolder()
        => Path.Combine(GetProgramDataRoot(), "Jobs");

    public static string GetConfigurationFile()
        => Path.Combine(GetProgramDataRoot(), "bridge.json");

    public static void EnsureProgramDataFolders()
    {
        Directory.CreateDirectory(GetProgramDataRoot());
        Directory.CreateDirectory(GetDefaultJobsFolder());
    }
}
