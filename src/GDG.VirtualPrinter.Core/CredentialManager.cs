namespace GDG.VirtualPrinter.Core;

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

public static class CredentialManager
{
    private const uint CRED_TYPE_GENERIC = 1;
    private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

    public static void Save(string target, string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Credential target is required.", nameof(target));

        byte[] blob = Encoding.Unicode.GetBytes(password ?? string.Empty);
        IntPtr blobPtr = IntPtr.Zero;

        try
        {
            if (blob.Length > 0)
            {
                blobPtr = Marshal.AllocCoTaskMem(blob.Length);
                Marshal.Copy(blob, 0, blobPtr, blob.Length);
            }

            var credential = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = target,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPtr,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = userName ?? string.Empty
            };

            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            if (blobPtr != IntPtr.Zero)
            {
                for (int i = 0; i < blob.Length; i++) Marshal.WriteByte(blobPtr, i, 0);
                Marshal.FreeCoTaskMem(blobPtr);
            }
            Array.Clear(blob, 0, blob.Length);
        }
    }

    public static (string UserName, string Password)? Read(string target)
    {
        if (!CredRead(target, CRED_TYPE_GENERIC, 0, out IntPtr credentialPtr))
            return null;

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
            string password = string.Empty;

            if (credential.CredentialBlob != IntPtr.Zero && credential.CredentialBlobSize > 0)
            {
                password = Marshal.PtrToStringUni(
                    credential.CredentialBlob,
                    checked((int)credential.CredentialBlobSize / 2)) ?? string.Empty;
            }

            return (credential.UserName ?? string.Empty, password);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public static void Delete(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return;
        CredDelete(target, CRED_TYPE_GENERIC, 0);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

    [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(
        string target,
        uint type,
        uint reservedFlag,
        out IntPtr credentialPtr);

    [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll")]
    private static extern void CredFree([In] IntPtr cred);
}
