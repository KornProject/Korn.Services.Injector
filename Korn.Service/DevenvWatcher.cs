using Korn.Utils;

delegate void DevenvProcessDelegate(HashedProcess Process);

class DevenvWatcher : IDisposable
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

    void OnProcessStarted(string name, int id, int parentID, int nameHash)
    {
        // Most likely, this is a start of a debug process, not a service, so we will not consider this process as a working one
        if (name == "conhost")
            return;

        HashedProcess? hashedProcess = null;    
        lock (locker) 
        {
            if (!IsDevenvProcess(name, id, parentID, nameHash))
                return;

            var process = ProcessUtils.GetProcessByID(id);
            if (process is null)
                return;

            hashedProcess = new(process, name, id, parentID, nameHash);
            ActiveProcesses.Add(id, hashedProcess);
        }

        ProcessStarted?.Invoke(hashedProcess);
    }

    void OnProcessStopped(string name, int id, int parentID, int nameHash)
    {
        HashedProcess? hashedProcess = null;
        lock (locker)
        {
            if (!IsDevenvProcess(name, id, parentID, nameHash))
                return;

            hashedProcess = ActiveProcesses[id];
            ActiveProcesses.Remove(id);
        }

        ProcessStopped?.Invoke(hashedProcess);
    }

    static int devenvProcessNameHash = HashedProcess.GetProcessNameHash("devenv");
    bool IsDevenvProcess(string name, int id, int parentID, int nameHash) 
        => nameHash == devenvProcessNameHash || ActiveProcesses.ContainsKey(parentID);

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