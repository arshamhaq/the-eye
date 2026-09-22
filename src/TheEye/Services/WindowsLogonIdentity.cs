using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace TheEye.Services;

/// <summary>Read-only Windows logon identity; no privilege or security changes.</summary>
public static class WindowsLogonIdentity
{
    public static string Read()
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!GetTokenInformation(identity.AccessToken, 10, out var statistics,
                Marshal.SizeOf<TokenStatistics>(), out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return $"{statistics.AuthenticationId.High:X8}:{statistics.AuthenticationId.Low:X8}";
    }

    [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] private struct TokenStatistics
    {
        public Luid TokenId, AuthenticationId;
        public long ExpirationTime;
        public int TokenType, ImpersonationLevel;
        public uint DynamicCharged, DynamicAvailable, GroupCount, PrivilegeCount;
        public Luid ModifiedId;
    }
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass,
        out TokenStatistics statistics, int length, out int returnLength);
}
