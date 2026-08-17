namespace GDG.VirtualPrinter.Core;

using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class AccountValidator
{
    private const int LOGON32_LOGON_NETWORK = 3;
    private const int LOGON32_PROVIDER_DEFAULT = 0;

    public static void Validate(string account, string password)
    {
        ParseAccount(account, out string? domain, out string user);

        if (!LogonUser(
            user,
            domain,
            password,
            LOGON32_LOGON_NETWORK,
            LOGON32_PROVIDER_DEFAULT,
            out SafeAccessTokenHandle token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        token.Dispose();
    }

    public static void ParseAccount(string account, out string? domain, out string user)
    {
        if (string.IsNullOrWhiteSpace(account))
            throw new ArgumentException("Account is required.", nameof(account));

        account = account.Trim();
        int slash = account.IndexOf('\\');

        if (slash > 0)
        {
            domain = account[..slash];
            user = account[(slash + 1)..];
        }
        else if (account.Contains('@'))
        {
            domain = null;
            user = account;
        }
        else
        {
            domain = ".";
            user = account;
        }
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(
        string lpszUsername,
        string? lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out SafeAccessTokenHandle phToken);
}
