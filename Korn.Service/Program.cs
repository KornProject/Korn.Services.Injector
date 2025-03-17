#define DEV

using Korn.AssemblyInjector;
using Korn.Shared;
using Korn.Utils;
using System.Diagnostics;

DeveloperTools.SetLocalLibraries();
KornShared.Logger.WriteMessage($"Service({Process.GetCurrentProcess().Id}) started");

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
;

using var processCollection = new ProcessCollection();
using var processWatcher = new ProcessWatcher(processCollection);
using var devenvWatcher = new DevenvWatcher(processWatcher);

devenvWatcher.ProcessStarted += OnProcessStarted;

#if !DEV
processCollection.InitializeExistedProcessesWithTimeOrder();
#endif

Thread.Sleep(int.MaxValue);

void OnProcessStarted(HashedProcess hashedProcess)
{
    try
    {
        var entry = hashedProcess.Entry;
        //if (entry.Name != "ServiceHub.RoslynCodeAnalysisService")
        //    return;

        const string dllname = "Korn.Bootstrapper.dll";

        var process = hashedProcess.Process;
        var processModules = process.Modules.Cast<ProcessModule>().ToArray();
        var isBootstrapperInjected = processModules.Any(m => Path.GetFileName(m.FileName) is dllname);
        if (isBootstrapperInjected)
            return;

        var isNet8 = net8ProcessHashes.Contains(entry.Hash);
        var isNet472 = net472ProcessHashes.Contains(entry.Hash);

        if (isNet8 || isNet472)
        {
            Console.WriteLine($"Injecting in \"{entry.Name}\"({entry.ID})");
            process.Exited += (s, e) => Console.WriteLine($"{process.ProcessName}({entry.ID}) exited with code {process.ExitCode}"); // doesn't work by some reason
            
            Console.WriteLine("Write smth");
            Console.ReadLine();

            /*
            if (entry.Name == "VBCSCompiler")
            {
                Console.WriteLine("Write smth");
                Console.ReadLine();
            }
            */

            using var injector = new UnsafeInjector(hashedProcess.Process);

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