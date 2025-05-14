using Korn;
using Korn.Utils;
using System.Diagnostics;

var logger = new KornLogger(Korn.Interface.InjectorService.LogFile);
var crasher = new KornCrasher(logger);
logger.WriteMessage($"Services.Injector({Process.GetCurrentProcess().Id}) started");

var net8ProcessHashes =
((string[])
[
    //"ServiceHub.Host.dotnet.x64",
    "ServiceHub.RoslynCodeAnalysisService",
    //"ServiceHub.ThreadedWaitDialog",
    //"ServiceHub.IdentityHost",
    //"ServiceHub.VSDetouredHost",
    //"Microsoft.ServiceHub.Controller" 
]).Select(StringHasher.CalculateHash).ToArray();

var net472ProcessHashes =
((string[])
[
    //"ServiceHub.IntellicodeModelService", // unused
    "devenv",
    //"MSBuild", // not used
    "VBCSCompiler"
]).Select(StringHasher.CalculateHash).ToArray();

((string[])[/*"MSBuild", */"VBCSCompiler"]).ToList().ForEach(name => Process.GetProcessesByName(name).ToList().ForEach(p => p.Kill()));

const string dllname = Korn.Interface.Bootstrapper.ExecutableFileName;
//var dllnameFootprint = ExternalProcessModules.GetNameFootprint(dllname);

using var processCollection = new ProcessCollection();
using var processWatcher = new ProcessWatcher(processCollection);
using var devenvWatcher = new DevenvWatcher(processWatcher);

devenvWatcher.ProcessStarted += OnProcessStarted;

Thread.Sleep(int.MaxValue);

void OnProcessStarted(ProcessEntry entry)
{
    try
    {
        var pid = entry.ID;
        using var process = new ExternalProcessId(pid);
        using var modules = process.Modules;

        var isNet8 = net8ProcessHashes.Contains(entry.Hash);
        var isNet472 = net472ProcessHashes.Contains(entry.Hash);

        if (isNet8 || isNet472)
        {
            var systime = Kernel32.GetSystemTime();
            process.FastSuspendProcess();
            logger.WriteLine($"suspended");

            crasher.StartWatchProcess(pid);
            using var injector = new AssemblyInjector(pid);

            logger.WriteLine($"Injection in {entry.Name}({entry.ID})");

            if (isNet8)
                injector.InjectInCoreClr(Path.Combine(Korn.Interface.Bootstrapper.BinNet8Directory, dllname));
            else if (isNet472)
                injector.InjectInClr(Path.Combine(Korn.Interface.Bootstrapper.BinNet472Directory, dllname));
        }
    }
    catch (Exception ex) 
    {
        Console.WriteLine(ex);
    }    
}