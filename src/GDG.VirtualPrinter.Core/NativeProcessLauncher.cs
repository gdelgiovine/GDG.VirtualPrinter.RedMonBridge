namespace GDG.VirtualPrinter.Core;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

public static class NativeProcessLauncher
{
    private const uint LOGON_WITH_PROFILE = 0x00000001;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const int STARTF_USESHOWWINDOW = 0x00000001;
    private const short SW_HIDE = 0;

    public static NativeProcessHandle StartHiddenWithLogon(
        string executablePath,
        string arguments,
        string workingDirectory,
        string account,
        string password,
        IReadOnlyDictionary<string, string?> environment)
    {
        AccountValidator.ParseAccount(account, out string? domain, out string user);

        var startup = new STARTUPINFO
        {
            cb = Marshal.SizeOf<STARTUPINFO>(),
            dwFlags = STARTF_USESHOWWINDOW,
            wShowWindow = SW_HIDE
        };

        string commandLine = "\"" + executablePath.Replace("\"", "\\\"") + "\"";
        if (!string.IsNullOrWhiteSpace(arguments))
            commandLine += " " + arguments;

        IntPtr environmentBlock = BuildEnvironmentBlock(environment);

        try
        {
            if (!CreateProcessWithLogonW(
                user,
                domain,
                password,
                LOGON_WITH_PROFILE,
                executablePath,
                commandLine,
                CREATE_UNICODE_ENVIRONMENT | CREATE_NO_WINDOW,
                environmentBlock,
                string.IsNullOrWhiteSpace(workingDirectory) ? null : workingDirectory,
                ref startup,
                out PROCESS_INFORMATION processInfo))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (processInfo.hThread != IntPtr.Zero)
                CloseHandle(processInfo.hThread);

            return new NativeProcessHandle(processInfo.hProcess, processInfo.dwProcessId);
        }
        finally
        {
            if (environmentBlock != IntPtr.Zero)
                Marshal.FreeHGlobal(environmentBlock);
        }
    }

    private static IntPtr BuildEnvironmentBlock(
        IReadOnlyDictionary<string, string?> customEnvironment)
    {
        var environment = new SortedDictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (System.Collections.DictionaryEntry item
                 in Environment.GetEnvironmentVariables())
        {
            if (item.Key is string key && item.Value is string value)
                environment[key] = value;
        }

        foreach (var item in customEnvironment)
            environment[item.Key] = item.Value ?? string.Empty;

        var text = new StringBuilder();

        foreach (var item in environment)
        {
            text.Append(item.Key);
            text.Append('=');
            text.Append(item.Value);
            text.Append('\0');
        }

        text.Append('\0');
        return Marshal.StringToHGlobalUni(text.ToString());
    }

    public sealed class NativeProcessHandle : IDisposable
    {
        private IntPtr _handle;

        internal NativeProcessHandle(IntPtr handle, uint processId)
        {
            _handle = handle;
            ProcessId = processId;
        }

        public uint ProcessId { get; }

        public int WaitForExit()
        {
            if (_handle == IntPtr.Zero)
                return 0;

            const uint INFINITE = 0xFFFFFFFF;
            WaitForSingleObject(_handle, INFINITE);

            if (!GetExitCodeProcess(_handle, out uint code))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            return unchecked((int)code);
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero)
                return;

            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessWithLogonW(
        string lpUsername,
        string? lpDomain,
        string lpPassword,
        uint dwLogonFlags,
        string? lpApplicationName,
        string lpCommandLine,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(
        IntPtr hHandle,
        uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(
        IntPtr hProcess,
        out uint lpExitCode);
}
