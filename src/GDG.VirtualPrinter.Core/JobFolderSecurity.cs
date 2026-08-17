namespace GDG.VirtualPrinter.Core;

using System;
using System.Diagnostics;
using System.IO;

public static class JobFolderSecurity
{
    public static void GrantModify(string folder, string account)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("Folder is required.", nameof(folder));

        if (string.IsNullOrWhiteSpace(account))
            throw new ArgumentException("Account is required.", nameof(account));

        Directory.CreateDirectory(folder);

        var psi = new ProcessStartInfo
        {
            FileName = "icacls.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add(folder);
        psi.ArgumentList.Add("/grant");
        psi.ArgumentList.Add(account + ":(OI)(CI)M");
        psi.ArgumentList.Add("/T");
        psi.ArgumentList.Add("/C");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Unable to start icacls.exe.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string error = process.StandardError.ReadToEnd();
            if (string.IsNullOrWhiteSpace(error))
                error = process.StandardOutput.ReadToEnd();

            throw new InvalidOperationException(
                $"Unable to grant Modify rights to '{account}' on '{folder}'. {error}".Trim());
        }
    }
}
