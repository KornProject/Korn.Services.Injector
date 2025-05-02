using Korn;
using Korn.AssemblyInjector;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;

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
]).Select(name => name.GetHashCode()).ToArray();

int[] net472ProcessHashes =
((string[])
[
    //"ServiceHub.IntellicodeModelService", // unused
    "devenv",
    //"MSBuild", // not used
    "VBCSCompiler"
]).Select(name => name.GetHashCode()).ToArray();

((string[])["MSBuild", "VBCSCompiler"]).ToList().ForEach(name => Process.GetProcessesByName(name).ToList().ForEach(p => p.Kill()));

using var processCollection = new ProcessCollection();
using var processWatcher = new ProcessWatcher(processCollection);
using var devenvWatcher = new DevenvWatcher(processWatcher);

devenvWatcher.ProcessStarted += OnProcessStarted;

Thread.Sleep(int.MaxValue);

void OnProcessStarted(HashedProcess hashedProcess)
{
    try
    {
        var entry = hashedProcess.Entry;
        const string dllname = Korn.Interface.Bootstrapper.ExecutableFileName;

        var process = hashedProcess.Process;
        var processModules = process.Modules.Cast<ProcessModule>().ToArray();
        var isBootstrapperInjected = processModules.Any(m => Path.GetFileName(m.FileName) is dllname);
        if (isBootstrapperInjected)
            return;

        var processId = process.Id;
        var isNet8 = net8ProcessHashes.Contains(entry.Hash);
        var isNet472 = net472ProcessHashes.Contains(entry.Hash) ;

        if (isNet8 || isNet472)
        {
            crasher.StartWatchProcess(processId);

            using var injector = new UnsafeInjector(process);

            logger.WriteLine($"Injection in {entry.Name}({entry.ID})");

            if (isNet8 && injector.IsCoreClr)
                injector.InjectInCoreClr(Path.Combine(Korn.Interface.Bootstrapper.BinNet8Directory, dllname));
            else if (isNet472 && injector.IsClr)
                injector.InjectInClr(Path.Combine(Korn.Interface.Bootstrapper.BinNet472Directory, dllname));
        }
    }
    catch (Exception ex) 
    {
        Console.WriteLine(ex);
    }    
}