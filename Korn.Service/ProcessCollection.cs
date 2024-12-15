using Korn;
using Korn.Utils;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
delegate void ProcessDelegate(string Name, int PID, int ParentPID, int NameHash);

unsafe class ProcessCollection : IDisposable
{
    public ProcessCollection() => InitializeCollections();

    #region Events
    public event ProcessDelegate? ProcessStarted;
    public event ProcessDelegate? ProcessStopped;
    #endregion

    #region Initialize
    const int MAX_PROCESSES = 2048;
    const int ENTRY_SIZE = sizeof(long) * 8;
    const int ENTRY_HALF_SIZE = ENTRY_SIZE / 2;
    const int MAX_ENTRY_STATES = MAX_PROCESSES / ENTRY_SIZE;
    long* entriesStates;
    int* processIDs;
    int* processParentIDs;
    string* processNames;
    int* processHashes;

    int NextFreeIndex;
    int MaxIndexOfProcess;
    readonly object locker = new();

    void DisposeCollections()
    {
        if (entriesStates is not null)
            MemoryUtils.Free(entriesStates);

        if (processIDs is not null)
            MemoryUtils.Free(processIDs);

        if (processIDs is not null)
            MemoryUtils.Free(processParentIDs);

        if (processHashes is not null)
            MemoryUtils.Free(processHashes);

        if (processNames is not null)
            MemoryUtils.Free(processNames);
    }

    void InitializeCollections()
    {
        DisposeCollections();

        entriesStates = MemoryUtils.Alloc<long>(MAX_ENTRY_STATES);
        processIDs = MemoryUtils.Alloc<int>(MAX_PROCESSES);
        processParentIDs = MemoryUtils.Alloc<int>(MAX_PROCESSES);
        processHashes = MemoryUtils.Alloc<int>(MAX_PROCESSES);
        processNames = (string*)MemoryUtils.Alloc<nint>(MAX_PROCESSES);
    }

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
            
            AddProcess(index, process);
        }
    }
    #endregion

    #region AddProcess
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProcess(Process process)
    {
        var id = process.Id;
        var name = process.ProcessName;
        var parentID = process.GetParentProcessID();
        var hash = HashedProcess.GetProcessNameHash(name);

        AddProcess(id, parentID, name, hash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddProcess(int index, Process process)
    {
        var id = process.Id;
        var name = process.ProcessName;
        var parentId = process.GetParentProcessID();
        var hash = HashedProcess.GetProcessNameHash(name);

        AddProcess(index, id, parentId, name, hash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProcess(int id, int parentID, string name, int hash) => AddProcess(FindFreeEntryIndex(), id, parentID, name, hash);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProcess(int id, int parentID, string name) => AddProcess(FindFreeEntryIndex(), id, parentID, name, HashedProcess.GetProcessNameHash(name));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProcess(int id, string name) => AddProcess(FindFreeEntryIndex(), id, ProcessUtils.GetParentProcessID(id), name, HashedProcess.GetProcessNameHash(name));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddProcess(int index, int id, int parentID, string name, int hash)
    {
        SetProcessEntryAtIndex(index, id, parentID, name, hash);

        if (index > MaxIndexOfProcess)
            MaxIndexOfProcess = index;

        ProcessStarted?.Invoke(name, id, parentID, hash);
    }
    #endregion

    #region SetProcessEntryAtIndex
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void SetProcessEntryAtIndex(int index, int id, int parentID, string name, int hash)
    {
        lock (locker)
        {
            entriesStates[index / ENTRY_SIZE] |= 1L << (index % ENTRY_SIZE);
            processIDs[index] = id;
            processParentIDs[index] = parentID;
            processHashes[index] = hash;
            processNames[index] = name;
        }
    }
    #endregion

    #region RemoveProcess
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveProcess(Process process) => RemoveProcess(process.ProcessName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveProcessByID(int id) => RemoveProcess(FindProcessEntryIndexByID(id));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void RemoveProcess(string name) => RemoveProcess(FindProcessEntryIndex(name));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void RemoveProcess(int index)
    {
        lock (locker)
            entriesStates[index / ENTRY_SIZE] &= ~(1L << (index % ENTRY_SIZE));

        if (ProcessStopped is not null)
        {
            var name = processNames[index];
            var id = processIDs[index];
            var parentID = processParentIDs[index];
            var hash = processHashes[index];

            ProcessStopped.Invoke(name, id, parentID, hash);
        }
    }
    #endregion

    #region FindProcessEntryIndex
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int FindProcessEntryIndex(Process process) => FindProcessEntryIndexByID(process.Id);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int FindProcessEntryIndexByID(int id)
    {
        var to = MaxIndexOfProcess + 1;
        for (int index = 0; index < to; index++)
            if (processIDs[index] == id)
                return index;

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int FindProcessEntryIndex(string name) => FindProcessEntryIndexByHash(HashedProcess.GetProcessNameHash(name));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int FindProcessEntryIndexByHash(int hash)
    {
        var to = MaxIndexOfProcess + 1;
        for (int index = 0; index < to; index++)
            if (processHashes[index] == hash)
                return index;

        return -1;
    }

    int FindFreeEntryIndex()
    {
        if (NextFreeIndex != -1)
        {
            var index = NextFreeIndex;
            NextFreeIndex = -1;
            return index;
        }

        for (var entryIndex = 0; entryIndex < MAX_ENTRY_STATES; entryIndex++)
        {
            var entryState = entriesStates[entryIndex];
            if ((ulong)entryState != 0xFFFFFFFFFFFFFFFF)
            {
                var lowerState = (int)(entryState & 0xFFFFFFFF);
                if ((uint)lowerState != 0xFFFFFFFF)
                {
                    for (int bitIndex = 0; bitIndex < 32; bitIndex++)
                    {
                        var state = (lowerState & (1 << bitIndex)) != 0;

                        if (!state)
                            return entryIndex * ENTRY_SIZE + bitIndex;
                    }
                }

                var highState = (int)((entryState >> 32) & 0xFFFFFFFF);
                if ((uint)highState != 0xFFFFFFFF)
                {
                    for (int bitIndex = 0; bitIndex < 32; bitIndex++)
                    {
                        var state = (lowerState & (1 << bitIndex)) != 0;

                        if (!state)
                            return entryIndex * ENTRY_SIZE + ENTRY_HALF_SIZE + bitIndex;
                    }
                }
            }
        }

        throw new KornException("ProcessCollection->FindFreeEntryIndex: Critical error: no free space for a new process entry");
    }
    #endregion

    #region IDisposable
    bool disposed;
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        DisposeCollections();
    }

    ~ProcessCollection() => Dispose();
    #endregion
}
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type