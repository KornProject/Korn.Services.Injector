using System.Management;

class ProcessWatcher : IDisposable
{
    public ProcessWatcher() : this(new ProcessCollection()) { }

    public ProcessWatcher(ProcessCollection processCollection)
    {
        ProcessCollection = processCollection;

        processStartWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
        processStartWatcher.EventArrived += OnProcessStarted;

        processStopWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
        processStopWatcher.EventArrived += OnProcessStopped;

        StartWatchers();
    }

    ManagementEventWatcher processStartWatcher;
    ManagementEventWatcher processStopWatcher;

    public readonly ProcessCollection ProcessCollection;

    void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        var properties = e.NewEvent.Properties;
        var name = (string)properties["ProcessName"].Value;
        var id = (int)(uint)properties["ProcessID"].Value;
        var parentID = (int)(uint)properties["ParentProcessID"].Value;

        name = Path.GetFileNameWithoutExtension(name);

        ProcessCollection.AddProcess(id, parentID, name);
    }

    void OnProcessStopped(object sender, EventArrivedEventArgs e)
    {
        var properties = e.NewEvent.Properties;
        var name = (string)properties["ProcessName"].Value;
        var id = (int)(uint)properties["ProcessID"].Value;
        var parentID = (int)(uint)properties["ParentProcessID"].Value;

        name = Path.GetFileNameWithoutExtension(name);

        ProcessCollection.RemoveProcessByID(id);
    }

    void StartWatchers()
    {
        processStartWatcher.Start();
        processStopWatcher.Start();
    }

    void StopWatchers()
    {
        processStartWatcher.Stop();
        processStopWatcher.Stop();
    }

    bool disposed;
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        StopWatchers();

        processStartWatcher.Dispose();
        processStopWatcher.Dispose();
    }

    ~ProcessWatcher() => Dispose();
}