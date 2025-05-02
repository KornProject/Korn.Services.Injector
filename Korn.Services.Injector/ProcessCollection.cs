using Korn.Utils;
using Korn.Utils.Algorithms;
using Korn.Utils.Memory;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
unsafe delegate void ProcessDelegate(ProcessEntry* entry);

public unsafe struct ProcessEntry
{
    public ProcessEntry(int id, int parentId, string name, int hash)
    {
        (ID, ParentID, Name, Hash) = (id, parentId, name, hash);
    }

    public int ID;
    public int ParentID;
    public string Name;
    public int Hash;
}

unsafe class ProcessCollection : IDisposable
{
    const int MAX_PROCESSES = 2048;

    public ProcessCollection()
    {
        States = new StateCollection(MAX_PROCESSES);
        Entries = MemoryEx.Alloc<ProcessEntry>(MAX_PROCESSES);
    }

    public event ProcessDelegate? ProcessStarted;
    public event ProcessDelegate? ProcessStopped;

    StateCollection States;
    ProcessEntry* Entries;

    public void InitializeExistedProcessesWithTimeOrder()
        => InitializeExistedProcesses(process => process.Id == 0 ? 0 : (int)new TimeSpan(process.StartTime.Ticks).TotalMilliseconds);

    public void InitializeExistedProcesses(Func<Process, int>? orderFunction = null)
    {
        var processes = Process.GetProcesses();

        if (orderFunction is not null)
            processes = processes.OrderBy(orderFunction).ToArray();

        for (var index = 0; index < processes.Length; index++)
        {
            var process = processes[index];

            if (process.Id is 0 or 4)
                continue;

            if (process.HasExited)
                return;

            if (process.ProcessName
                is "explorer" or "ntoskrnl" or "WerFault" or "backgroundTaskHost" or "backgroundTransferHost" or "winlogon" or "wininit"
                or "csrss" or "lsass" or "smss" or "services" or "taskeng" or "taskhost" or "dwm"  or "sihost" or "Secure System" or "Registry")
                continue;
            
            AddProcess(process);
        }
    }

    void AddProcess(ProcessEntry entry)
    {
        lock (this)
        {
            var index = States.HoldEntry();
            Entries[index] = entry;
            ProcessStarted?.Invoke(Entries + index);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void AddProcess(int id, int parentID, string name, int hash) => AddProcess(new ProcessEntry(id, parentID, name, hash));
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void AddProcess(int id, int parentID, string name) => AddProcess(id, parentID, name, HashedProcess.GetProcessNameHash(name));
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void AddProcess(int id, string name) => AddProcess(id, ProcessUtils.GetParentProcessID(id), name);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void AddProcess(Process process) => AddProcess(process.Id, process.ProcessName);

    void RemoveProcess(int index)
    {
        if (index == -1)
            return;

        lock (this)
        {
            States.FreeEntry(index);
            ProcessStopped?.Invoke(Entries + index);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void RemoveProcess(Process process) => RemoveProcessByID(process.Id);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void RemoveProcessByID(int id) => RemoveProcess(FindProcessEntryIndexByID(id));
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void RemoveProcess(string name) => RemoveProcess(FindProcessEntryIndex(name));

    [MethodImpl(MethodImplOptions.AggressiveInlining)] int FindProcessEntryIndex(Process process) => FindProcessEntryIndexByID(process.Id);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int FindProcessEntryIndexByID(int id)
    {
        var to = States.TopHoldedIndex + 1;
        for (int index = 0; index < to; index++)
            if ((Entries + index)->ID == id)
                return index;

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] int FindProcessEntryIndex(string name) => FindProcessEntryIndexByHash(HashedProcess.GetProcessNameHash(name));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int FindProcessEntryIndexByHash(int hash)
    {
        var to = States.TopHoldedIndex + 1;
        for (int index = 0; index < to; index++)
            if ((Entries + index)->Hash == hash)
                return index;

        return -1;
    }

    #region IDisposable
    bool disposed;
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        States.Dispose();
        MemoryEx.Free(Entries);
    }

    ~ProcessCollection() => Dispose();
    #endregion
}
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type