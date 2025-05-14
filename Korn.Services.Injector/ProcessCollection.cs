using System.Runtime.InteropServices;
using Korn.Utils.Algorithms;
using Korn.Utils;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
unsafe delegate void ProcessDelegate(ProcessEntry entry);

class ProcessEntry
{
    public ProcessEntry(int id, int parentId, string name, ulong hash)
        => (ID, ParentID, Name, Hash) = (id, parentId, name, hash);

    public int ID;
    public int ParentID;
    public string Name;
    public ulong Hash;
}

unsafe class ProcessCollection : IDisposable
{
    const int MAX_PROCESSES = 2048;

    public ProcessCollection()
    {
        States = new StateCollection(MAX_PROCESSES);
        Entries = new ProcessEntry[MAX_PROCESSES];
    }

    public event ProcessDelegate? ProcessStarted, ProcessStopped;

    StateCollection States;
    ProcessEntry[] Entries;

    public void AddProcess(int id, string name) => AddProcess(id, new ExternalProcessId(id).ParentId, name);
    public void AddProcess(int id, int parentID, string name) => AddProcess(id, parentID, name, StringHasher.CalculateHash(name));
    public void AddProcess(int id, int parentID, string name, ulong hash) => AddProcess(new ProcessEntry(id, parentID, name, hash));
    void AddProcess(ProcessEntry entry)
    {
        var index = States.HoldEntry();
        Entries[index] = entry;
        ProcessStarted?.Invoke(entry);
    }

    public void RemoveProcessByID(int id) => RemoveProcess(FindProcessEntryIndexByID(id));
    void RemoveProcess(int index)
    {
        if (index == -1)
            return;

        ProcessEntry entry = Entries[index];
        States.FreeEntry(index); 
        ProcessStopped?.Invoke(entry);
    }

    int FindProcessEntryIndexByID(int id)
    {
        var to = States.TopHoldedIndex + 1;
        for (int index = 0; index < to; index++)
            if (Entries[index].ID == id)
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
    }

    ~ProcessCollection() => Dispose();
    #endregion
}