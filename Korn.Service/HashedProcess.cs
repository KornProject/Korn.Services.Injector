using System.Diagnostics;
using System.Runtime.CompilerServices;

record HashedProcess(Process Process, string Name, int ID, int ParentID, int NameHash)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetProcessHash(Process process) => process.ProcessName.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetProcessNameHash(string processName) => processName.GetHashCode();
}