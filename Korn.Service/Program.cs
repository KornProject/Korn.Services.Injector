#define DEV

using Korn.AssemblyInjector;
using Korn.Utils;
using Korn.Utils.VisualStudio;
using System.Diagnostics;

int[] net8ProcessHashes = 
[
    "ServiceHub.IntellicodeModelService".GetHashCode(),
    "ServiceHub.Host.dotnet.x64".GetHashCode(),
    "ServiceHub.RoslynCodeAnalysisService".GetHashCode(),
    "ServiceHub.ThreadedWaitDialog".GetHashCode(),
    "ServiceHub.IdentityHost".GetHashCode(),
    "ServiceHub.VSDetouredHost".GetHashCode(),
    "Microsoft.ServiceHub.Controller".GetHashCode(),
    "MSBuild".GetHashCode(), // …
];

int[] netframework472ProcessHashes = [
    "devenv".GetHashCode()
];

var visualStudioPath = VSWhere.ResolveVisualStudioPath();

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
    const string dllname = "Korn.Bootstrapper.dll";
    var process = hashedProcess.Process;

    using var processManager = new ExternalProcessManager(process);
    processManager.SuspendProcess();

    var processModules = process.Modules.Cast<ProcessModule>().ToArray();
    var isBootstrapperInjected = processModules.Any(m => Path.GetFileName(m.FileName) is dllname);
    if (!isBootstrapperInjected)
    {
        var isNet8 = net8ProcessHashes.Contains(hashedProcess.NameHash);

        Console.WriteLine($"Injecting in \"{hashedProcess.Name}\" process with pid {hashedProcess.ID}");

        using var injector = new UnsafeInjector(hashedProcess.Process);

        if (isNet8 && injector.IsCoreClr)
            injector.InjectInCoreClr(Path.Combine(Korn.Interface.Bootstrapper.BinNet8Directory, dllname));
        else if (!isNet8 && injector.IsClr)
            injector.InjectInClr(Path.Combine(Korn.Interface.Bootstrapper.BinNet472Directory, dllname));
    }

    processManager.ResumeProcess();
}