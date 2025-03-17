using Korn.Utils;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
delegate void DevenvProcessDelegate(HashedProcess Process);

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

    public readonly Dictionary<int, HashedProcess> ActiveProcesses = [];
    readonly object locker = new();

    void OnProcessStarted(ProcessEntry* process)
    {
        // Most likely, this is a start of a debug process, not a service, so we will not consider this process as a working one
        if (process->Name == "conhost")
            return;

        HashedProcess? hashedProcess = null;    
        lock (locker) 
        {
            if (!IsDevenvProcess(process))
                return;

            var managedProcess = ProcessUtils.GetProcessByID(process->ID);
            if (managedProcess is null)
                return;

            hashedProcess = new(managedProcess, *process);
            ActiveProcesses.Add(process->ID, hashedProcess);
        }

        ProcessStarted?.Invoke(hashedProcess);
    }

    void OnProcessStopped(ProcessEntry* process)
    {
        HashedProcess? hashedProcess = null;
        lock (locker)
        {
            if (!IsDevenvProcess(process))
                return;

            if (!ActiveProcesses.ContainsKey(process->ID))
                return;

            hashedProcess = ActiveProcesses[process->ID];
            ActiveProcesses.Remove(process->ID);
        }

        ProcessStopped?.Invoke(hashedProcess);
    }

    static int devenvProcessNameHash = HashedProcess.GetProcessNameHash("devenv");
    bool IsDevenvProcess(ProcessEntry* process)
        => process->Hash == devenvProcessNameHash || ActiveProcesses.ContainsKey(process->ParentID);

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

    bool disposed;
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        UnregisterEvents();
    }

    ~DevenvWatcher() => Dispose();
}
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type