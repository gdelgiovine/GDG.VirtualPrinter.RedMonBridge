namespace GDG.VirtualPrinter.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

public static class RdsSessionResolver
{
    private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

    public static void Resolve(PrintJobMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.SubmitterUser))
        {
            metadata.RdsResolutionStatus = "NoSubmitterUser";
            return;
        }

        ParseIdentity(metadata.SubmitterUser, out string wantedDomain, out string wantedUser);

        var sessions = Enumerate()
            .Where(s =>
                string.Equals(s.UserName, wantedUser, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(wantedDomain) ||
                 string.Equals(s.DomainName, wantedDomain, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (sessions.Count == 0)
        {
            // Some spoolers expose only the short user name while WTS exposes a domain.
            sessions = Enumerate()
                .Where(s => string.Equals(
                    s.UserName, wantedUser, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (sessions.Count == 0)
        {
            metadata.RdsResolutionStatus = "NoMatchingSession";
            return;
        }

        SessionSnapshot? selected = null;

        var active = sessions.Where(s => s.State == WTS_CONNECTSTATE_CLASS.WTSActive).ToList();
        if (active.Count == 1)
            selected = active[0];
        else if (sessions.Count == 1)
            selected = sessions[0];

        if (selected is null)
        {
            metadata.RdsResolutionStatus = "Ambiguous";
            return;
        }

        metadata.RdsSessionId = checked((int)selected.SessionId);
        metadata.RdsSessionName = selected.SessionName;
        metadata.RdsClientName = QueryString(selected.SessionId, WTS_INFO_CLASS.WTSClientName);
        metadata.IsRemoteSession = QueryBool(selected.SessionId, WTS_INFO_CLASS.WTSIsRemoteSession);
        metadata.RdsResolutionStatus = "Resolved";
    }

    private static List<SessionSnapshot> Enumerate()
    {
        var result = new List<SessionSnapshot>();

        if (!WTSEnumerateSessions(
            WTS_CURRENT_SERVER_HANDLE,
            0,
            1,
            out IntPtr buffer,
            out int count))
        {
            return result;
        }

        try
        {
            int size = Marshal.SizeOf<WTS_SESSION_INFO>();

            for (int i = 0; i < count; i++)
            {
                IntPtr ptr = IntPtr.Add(buffer, i * size);
                var info = Marshal.PtrToStructure<WTS_SESSION_INFO>(ptr);

                string user = QueryString(info.SessionId, WTS_INFO_CLASS.WTSUserName);
                if (string.IsNullOrWhiteSpace(user))
                    continue;

                result.Add(new SessionSnapshot
                {
                    SessionId = info.SessionId,
                    SessionName = info.pWinStationName == IntPtr.Zero
                        ? string.Empty
                        : Marshal.PtrToStringUni(info.pWinStationName) ?? string.Empty,
                    State = info.State,
                    UserName = user,
                    DomainName = QueryString(info.SessionId, WTS_INFO_CLASS.WTSDomainName)
                });
            }

            return result;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private static string QueryString(uint sessionId, WTS_INFO_CLASS infoClass)
    {
        if (!WTSQuerySessionInformation(
            WTS_CURRENT_SERVER_HANDLE,
            sessionId,
            infoClass,
            out IntPtr buffer,
            out _))
        {
            return string.Empty;
        }

        try
        {
            return buffer == IntPtr.Zero
                ? string.Empty
                : Marshal.PtrToStringUni(buffer) ?? string.Empty;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private static bool? QueryBool(uint sessionId, WTS_INFO_CLASS infoClass)
    {
        if (!WTSQuerySessionInformation(
            WTS_CURRENT_SERVER_HANDLE,
            sessionId,
            infoClass,
            out IntPtr buffer,
            out uint bytes))
        {
            return null;
        }

        try
        {
            if (buffer == IntPtr.Zero || bytes < sizeof(int))
                return null;

            return Marshal.ReadInt32(buffer) != 0;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private static void ParseIdentity(string identity, out string domain, out string user)
    {
        identity = identity.Trim();
        int slash = identity.IndexOf('\\');

        if (slash > 0)
        {
            domain = identity[..slash];
            user = identity[(slash + 1)..];
            return;
        }

        int at = identity.IndexOf('@');
        if (at > 0)
        {
            user = identity[..at];
            domain = identity[(at + 1)..];
            return;
        }

        domain = string.Empty;
        user = identity;
    }

    private sealed class SessionSnapshot
    {
        public uint SessionId { get; init; }
        public string SessionName { get; init; } = string.Empty;
        public WTS_CONNECTSTATE_CLASS State { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string DomainName { get; init; } = string.Empty;
    }

    private enum WTS_CONNECTSTATE_CLASS
    {
        WTSActive,
        WTSConnected,
        WTSConnectQuery,
        WTSShadow,
        WTSDisconnected,
        WTSIdle,
        WTSListen,
        WTSReset,
        WTSDown,
        WTSInit
    }

    private enum WTS_INFO_CLASS
    {
        WTSInitialProgram,
        WTSApplicationName,
        WTSWorkingDirectory,
        WTSOEMId,
        WTSSessionId,
        WTSUserName,
        WTSWinStationName,
        WTSDomainName,
        WTSConnectState,
        WTSClientBuildNumber,
        WTSClientName,
        WTSClientDirectory,
        WTSClientProductId,
        WTSClientHardwareId,
        WTSClientAddress,
        WTSClientDisplay,
        WTSClientProtocolType,
        WTSIdleTime,
        WTSLogonTime,
        WTSIncomingBytes,
        WTSOutgoingBytes,
        WTSIncomingFrames,
        WTSOutgoingFrames,
        WTSClientInfo,
        WTSSessionInfo,
        WTSSessionInfoEx,
        WTSConfigInfo,
        WTSValidationInfo,
        WTSSessionAddressV4,
        WTSIsRemoteSession
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WTS_SESSION_INFO
    {
        public uint SessionId;
        public IntPtr pWinStationName;
        public WTS_CONNECTSTATE_CLASS State;
    }

    [DllImport("Wtsapi32.dll", EntryPoint = "WTSEnumerateSessionsW",
        SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSEnumerateSessions(
        IntPtr hServer,
        int reserved,
        int version,
        out IntPtr ppSessionInfo,
        out int pCount);

    [DllImport("Wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW",
        SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSQuerySessionInformation(
        IntPtr hServer,
        uint sessionId,
        WTS_INFO_CLASS wtsInfoClass,
        out IntPtr ppBuffer,
        out uint pBytesReturned);

    [DllImport("Wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);
}
