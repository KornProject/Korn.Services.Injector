using Korn.Utils;
using System.Management;

unsafe class ProcessWatcher : IDisposable
{
    public ProcessWatcher() : this(new ProcessCollection()) { }

    public ProcessWatcher(ProcessCollection processCollection)
    {
        ProcessCollection = processCollection;

        processCreation = new ManagementEventWatcher("SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'");
        processCreation.EventArrived += OnProcessCreation;

        processStopWatcher = new ManagementEventWatcher("SELECT * FROM Win32_ProcessStopTrace");
        processStopWatcher.EventArrived += OnProcessStopped;

        StartWatchers();
    }

    ManagementEventWatcher processCreation;
    ManagementEventWatcher processStopWatcher;

    public readonly ProcessCollection ProcessCollection;

    void OnProcessCreation(object sender, EventArrivedEventArgs e)
    {
        Console.WriteLine($"c: {Kernel32.GetSystemTime() / 10000}");

        var eventProperties = e.NewEvent.Properties;

        var instance = (ManagementBaseObject)eventProperties["TargetInstance"].Value;
        var instanceProperties = instance.Properties;

        var name = (string)instanceProperties["Description"].Value;
        var id = (int)(uint)instanceProperties["ProcessID"].Value;
        var parentID = (int)(uint)instanceProperties["ParentProcessID"].Value;

        name = name.Substring(0, name.Length - 4);

        ProcessCollection.AddProcess(id, parentID, name);
    }

    void OnProcessStopped(object sender, EventArrivedEventArgs e)
    {
        var properties = e.NewEvent.Properties;
        var name = (string)properties["ProcessName"].Value;
        var id = (int)(uint)properties["ProcessID"].Value;
        var parentID = (int)(uint)properties["ParentProcessID"].Value;

        name = name.Substring(0, name.Length - 4);

        ProcessCollection.RemoveProcessByID(id);
    }

    void StartWatchers()
    {
        processCreation.Start();
        processStopWatcher.Start();
    }

    void StopWatchers()
    {
        processCreation.Stop();
        processStopWatcher.Stop();
    }

    #region IDisposable
    bool disposed;
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        StopWatchers();

        processCreation.Dispose();
        processStopWatcher.Dispose();
    }

    ~ProcessWatcher() => Dispose();
    #endregion
}