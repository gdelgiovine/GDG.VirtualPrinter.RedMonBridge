namespace GDG.VirtualPrinter.VirtualPrinter;

using System.Diagnostics;
using Windows.ApplicationModel.Background;
using Windows.Graphics.Printing.PrintSupport;

public sealed class PsaExtensionTask : IBackgroundTask
{
    public void Run(IBackgroundTaskInstance task) => Guard(() =>
    {
        if (task is null) return;

        var deferral = task.GetDeferral();
        task.Canceled += (_, _) => Guard(deferral.Complete);

        var details = task.TriggerDetails as PrintSupportExtensionTriggerDetails;
        if (details?.Session is null)
        {
            deferral.Complete();
            return;
        }

        details.Session.PrintTicketValidationRequested += (_, e) =>
        {
            Guard(() =>
            {
                using var validationDeferral = e.GetDeferral();
                e.SetPrintTicketValidationStatus(
                    WorkflowPrintTicketValidationStatus.Resolved);
            });
        };

        details.Session.Start();
    });

    private static void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { Trace.WriteLine(ex); }
    }
}
