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

#if DEV
var bootstrapperExecutable = @"C:\Data\programming\vs projects\korn\Korn.Bootstrapper\Korn.Bootstrapper\bin\x64\Debug\net8.0-windows\Korn.Bootstrapper.dll";
#else
var kornDirectory = SystemVariablesUtils.GetKornPath()!;
var bootstrapperExecutable = Path.Combine(kornDirectory, "Bootsrapper", "bin", "Korn.Bootstrapper.dll");
#endif

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
    var process = hashedProcess.Process;

    using var processManager = new ExternalProcessManager(process);
    processManager.SuspendProcess();

    var processModules = process.Modules.Cast<ProcessModule>().ToArray();
    var isBootstrapperInjected = processModules.Any(m => Path.GetFileName(m.FileName) is "Korn.Bootstrapper.dll" or "Korn.Bootstrapper.netframework.dll");
    if (!isBootstrapperInjected)
    {
        var isNet8 = net8ProcessHashes.Contains(hashedProcess.NameHash);

        if (isNet8)
        {
            Console.WriteLine($"Injecting in \"{hashedProcess.Name}\" process with pid {hashedProcess.ID}");

            using var injector = new UnsafeInjector(hashedProcess.Process);

            if (injector.IsCoreClr)
                injector.InjectInCoreClr(bootstrapperExecutable);
        }
    }

    processManager.ResumeProcess();
}