using Korn;
using Korn.Logger;
using Korn.Modules.Com.Wmi;
using Korn.Modules.WinApi;
using Korn.Utils;

var logger = new KornLogger(Korn.Interface.InjectorService.LogFile);
var crasher = new KornCrashWatcher(logger);
logger.WriteMessage($"Services.Injector({Process.Current.ID}) started");

var net8Processes =
((string[])
[
    //"ServiceHub.Host.dotnet.x64",
    "ServiceHub.RoslynCodeAnalysisService",
    //"ServiceHub.ThreadedWaitDialog",
    //"ServiceHub.IdentityHost",
    //"ServiceHub.VSDetouredHost",
    //"Microsoft.ServiceHub.Controller" 
]);

var net472Processes =
((string[])
[
    //"ServiceHub.IntellicodeModelService", // unused
    "devenv",
    //"MSBuild", // not used
    "VBCSCompiler"
]);

((string[])[/*"MSBuild", */"VBCSCompiler"]).ToList().ForEach(name => Process.Processes.GetProcessesByName(name).ForEach(p => p.Kill()));

const string dllname = Korn.Interface.Bootstrapper.ExecutableFileName;

var processWatcher = new ProcessWatcher();
var devenvWatcher = new DevenvWatcher(processWatcher);

devenvWatcher.ProcessStarted += OnProcessStartedWrapper;
devenvWatcher.ProcessStopped += OnProcessStopped;

Thread.Sleep(int.MaxValue);

void OnProcessStartedWrapper(CreatedProcess entry)
{
    var processId = entry.ID;
    var process = new Process(processId);

    Task.Run(() =>
    {
        try
        {
            var isNet8 = net8Processes.Contains(entry.Name);
            var isNet472 = net472Processes.Contains(entry.Name);

            if (isNet8 || isNet472)
                OnProcessStarted(process, isNet8);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        process.Dispose();
    });
}

// it is has sence to add cache to clrResolver, but this will not be beneficial since it runs while the process is suspended
void OnProcessStarted(Process process, bool isNet8)
{
    process.FastSuspend();
    crasher.StartWatchProcess(process.ID);

    var modules = process.Modules;
    var clrModuleName = isNet8 ? "coreclr.dll" : "clr.dll";
    if (!modules.ContainsModule(clrModuleName))
    {
        const int INITIALIZATION_TIMEOUT = 200 * 10000; // 200ms

        var startTime = Kernel32.GetSystemTime();
        process.FastResume();

        do Task.Delay(1);
        while (!modules.ContainsModule(clrModuleName) && (startTime - Kernel32.GetSystemTime()) < INITIALIZATION_TIMEOUT);

        if (!modules.ContainsModule(clrModuleName))
            logger.Error($"Services.Injector->OnProcessStarted: The process has not been initialized");

        process.FastSuspend();
    }

    logger.WriteLine($"Injection in {process.Name}({process.ID})");
    var injector = new AssemblyInjector(process);

    if (isNet8)
    {
        var path = Path.Combine(Korn.Interface.Bootstrapper.BinNet8Directory, dllname);
        injector.InjectInCoreClr(path);
    }
    else
    {
        var path = Path.Combine(Korn.Interface.Bootstrapper.BinNet472Directory, dllname);
        injector.InjectInClr(path);
    }    

    // timeout. in case the main thread was loked, we will simply continue all threads after the timeout.
    // in anyway it gives a profit, so it makes sense.
    // lock can be either at the level of implementation of windows processes or in clr.
    Task.Delay(300);
    process.FastResume();
}

void OnProcessStopped(DestructedProcess stoppedProcessEntry)
{
    var stoppedProcessId = stoppedProcessEntry.ID;

    var activeProcesses = devenvWatcher.ActiveProcesses.ToList();
    for (var i = 0; i < activeProcesses.Count; i++)
    {
        var (pid, entry) = activeProcesses[i];
        var parentId = entry.ParentID;
        if (parentId == stoppedProcessId)
        {
            using var process = new Process(pid);
            process.Kill();
        }
    }
}