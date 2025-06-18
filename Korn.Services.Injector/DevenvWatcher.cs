#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

using Korn.Com.Wmi;

unsafe class DevenvWatcher : IDisposable
{
    public DevenvWatcher(ProcessWatcher processWatcher)
    {
        ProcessWatcher = processWatcher;

        RegisterEvents();
    }

    public readonly ProcessWatcher ProcessWatcher;
    public Action<CreatedProcess>? ProcessStarted;
    public Action<DestructedProcess>? ProcessStopped;

    public readonly Dictionary<int, CreatedProcess> ActiveProcesses = [];

    void OnProcessStarted(CreatedProcess process)
    {
        // Most likely, this is a start of a debug process, not a service, so we will not consider this process as a working one
        if (process.Name == "conhost")
            return;

        if (!IsDevenvProcess(process))
            return;

        ActiveProcesses.Add(process.ID, process);
        ProcessStarted?.Invoke(process);
    }

    void OnProcessStopped(DestructedProcess process)
    {
        var id = process.ID;
        if (!ActiveProcesses.ContainsKey(id))
            return;

        ActiveProcesses.Remove(id);
        ProcessStopped?.Invoke(process);
    }

    bool IsDevenvProcess(CreatedProcess process) => process.Name == "devenv" || ActiveProcesses.ContainsKey(process.ParentID);

    void RegisterEvents()
    {
        ProcessWatcher.ProcessStarted += OnProcessStarted;
        ProcessWatcher.ProcessStopped += OnProcessStopped;
    }

    void UnregisterEvents()
    {
        ProcessWatcher.ProcessStarted -= OnProcessStarted;
        ProcessWatcher.ProcessStopped -= OnProcessStopped;
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
