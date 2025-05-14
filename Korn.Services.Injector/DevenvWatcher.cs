using Korn.Utils;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
delegate void DevenvProcessDelegate(ProcessEntry Process);

unsafe class DevenvWatcher : IDisposable
{
    public DevenvWatcher(ProcessWatcher processWatcher)
    {
        ProcessWatcher = processWatcher;
        ProcessCollection = processWatcher.ProcessCollection;

        RegisterEvents();
    }

    public readonly ProcessWatcher ProcessWatcher;
    public readonly ProcessCollection ProcessCollection;
    public event DevenvProcessDelegate? ProcessStarted;
    public event DevenvProcessDelegate? ProcessStopped;

    public readonly Dictionary<int, ProcessEntry> ActiveProcesses = [];
    readonly object locker = new();

    static ulong conhostHash = StringHasher.CalculateHash("conhost");
    void OnProcessStarted(ProcessEntry process)
    {
        // Most likely, this is a start of a debug process, not a service, so we will not consider this process as a working one
        if (process.Hash == conhostHash)
            return;

        if (!IsDevenvProcess(process))
            return;

         ActiveProcesses.Add(process.ID, process);

        ProcessStarted?.Invoke(process);
    }

    void OnProcessStopped(ProcessEntry process)
    {
        var id = process.ID;
        if (!ActiveProcesses.ContainsKey(id))
            return;

        ActiveProcesses.Remove(id);

        ProcessStopped?.Invoke(process);
    }

    static ulong devenvProcessNameHash = StringHasher.CalculateHash("devenv");
    bool IsDevenvProcess(ProcessEntry process)
        => process.Hash == devenvProcessNameHash || ActiveProcesses.ContainsKey(process.ParentID);

    void RegisterEvents()
    {
        ProcessCollection.ProcessStarted += OnProcessStarted;
        ProcessCollection.ProcessStopped += OnProcessStopped;
    }

    void UnregisterEvents()
    {
        ProcessCollection.ProcessStarted -= OnProcessStarted;
        ProcessCollection.ProcessStopped -= OnProcessStopped;
    }

    #region IDisposible
    bool disposed;
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        UnregisterEvents();
    }

    ~DevenvWatcher() => Dispose();
    #endregion
}
