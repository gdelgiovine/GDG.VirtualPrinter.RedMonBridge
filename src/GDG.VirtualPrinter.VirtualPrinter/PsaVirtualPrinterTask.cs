namespace GDG.VirtualPrinter.VirtualPrinter;

using GDG.VirtualPrinter.Core;
using System.IO;
using System.Threading;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage;
using Windows.Storage.Streams;

public sealed class PsaVirtualPrinterTask : IBackgroundTask
{
    public void Run(IBackgroundTaskInstance task)
    {
        if (task is null) return;

        var deferral = task.GetDeferral();
        var completed = 0;

        void CompleteDeferral()
        {
            if (Interlocked.Exchange(ref completed, 1) == 0)
                deferral.Complete();
        }

        task.Canceled += (_, _) => CompleteDeferral();

        var details = task.TriggerDetails as PrintWorkflowVirtualPrinterTriggerDetails;
        var session = details?.VirtualPrinterSession;
        if (session is null)
        {
            CompleteDeferral();
            return;
        }

        session.VirtualPrinterDataAvailable += async (_, e) =>
        {
            var succeeded = false;
            try
            {
                succeeded = await HandleJobAsync(e);
            }
            catch
            {
                succeeded = false;
            }
            finally
            {
                e.CompleteJob(succeeded
                    ? PrintWorkflowSubmittedStatus.Succeeded
                    : PrintWorkflowSubmittedStatus.Failed);
                CompleteDeferral();
            }
        };

        session.Start();
    }

    private static async Task<bool> HandleJobAsync(
        PrintWorkflowVirtualPrinterDataAvailableEventArgs e)
    {
        StorageFolder cache = BridgePaths.GetPublisherCacheFolder();
        string lockPath = Path.Combine(cache.Path, BridgePaths.LockFileName);

        using var jobLock = new LockFile(lockPath);

        return await jobLock.ExecuteAsync(async () =>
        {
            StorageFile output = await cache.CreateFileAsync(
                BridgePaths.SourceFileName,
                CreationCollisionOption.ReplaceExisting);

            using IRandomAccessStream target = await output.OpenAsync(FileAccessMode.ReadWrite);
            IInputStream input = e.SourceContent.GetInputStream();

            await RandomAccessStream.CopyAndCloseAsync(
                input,
                target.GetOutputStreamAt(0));

            var metadata = new PrintJobMetadata
            {
                JobTitle = e.Configuration.JobTitle ?? string.Empty,
                WorkflowSessionId = e.Configuration.SessionId ?? string.Empty,
                SourceAppDisplayName = e.Configuration.SourceAppDisplayName ?? string.Empty,
                PrinterName = "GDG Virtual Printer",
                PrinterUri = "gdg-virtual-printer:oxps"
            };

            // Resolve legacy spooler/RDS identity while the print job is still active.
            SpoolerJobResolver.Resolve(metadata);

            await metadata.SaveAsync(
                Path.Combine(cache.Path, BridgePaths.MetadataFileName));

            return true;
        },
        async () =>
        {
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("Launcher");
        });
    }
}
