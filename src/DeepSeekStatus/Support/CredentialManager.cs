using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DeepSeekStatus.Support;

public static class CredentialManager
{
    public const string DefaultTarget = "DeepSeekStatus/deepseek-api-key";
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;

    public static string? Load(string target = DefaultTarget)
    {
        if (!CredRead(target, CredTypeGeneric, 0, out var handle))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(handle);
            if (credential.CredentialBlobSize <= 0 || credential.CredentialBlob == IntPtr.Zero)
            {
                return null;
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            var encoding = bytes.Length >= 2 && bytes[1] == 0 ? Encoding.Unicode : Encoding.UTF8;
            var key = encoding.GetString(bytes).Trim('\0', ' ', '\r', '\n', '\t');
            return key.Length > 0 ? key : null;
        }
        finally
        {
            CredFree(handle);
        }
    }

    public static string? Save(string key, string target = DefaultTarget)
    {
        var trimmed = key.Trim();
        if (trimmed.Length == 0)
        {
            return Strings.Get("balance.key.empty");
        }

        Delete(target);

        var bytes = Encoding.UTF8.GetBytes(trimmed);
        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        var targetPtr = Marshal.StringToCoTaskMemUni(target);
        var user = Marshal.StringToCoTaskMemUni(Environment.UserName);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = targetPtr,
                CredentialBlob = blob,
                CredentialBlobSize = bytes.Length,
                Persist = CredPersistLocalMachine,
                UserName = user,
            };

            if (CredWrite(ref credential, 0))
            {
                return null;
            }

            return new Win32Exception(Marshal.GetLastWin32Error()).Message;
        }
        finally
        {
            Marshal.FreeCoTaskMem(blob);
            Marshal.FreeCoTaskMem(targetPtr);
            Marshal.FreeCoTaskMem(user);
        }
    }

    public static void Delete(string target = DefaultTarget)
    {
        CredDelete(target, CredTypeGeneric, 0);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
