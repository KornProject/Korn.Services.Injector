using Korn.Utils;
using Korn.Utils.VisualStudio;
using System.Diagnostics;

#if DEBUG
var bootstrapperDirectory = @"C:\Data\programming\vs projects\korn\Korn.Bootstrapper\Korn.Bootstrapper\bin\Debug\netcoreapp3.1";
#else
const string KORN_PATH_VAR_NAME = "KORN_PATH";

var kornDirectory = SystemVariablesUtils.GetVariable(KORN_PATH_VAR_NAME)!;
var bootstrapperDirectory = Path.Combine(kornDirectory, "Bootsrapper", "bin");
#endif

var visualStudioPath = VSWhere.ResolveVisualStudioPath();

using var processCollection = new ProcessCollection();
using var processWatcher = new ProcessWatcher(processCollection);
using var devenvWatcher = new DevenvWatcher(processWatcher);

devenvWatcher.ProcessStarted += OnProcessStarted;

#if RELEASE
processCollection.InitializeExistedProcessesWithTimeOrder();
#endif

Thread.Sleep(int.MaxValue);

void OnProcessStarted(HashedProcess hashedProcess)
{
    var process = hashedProcess.Process;

    using var processManager = new ExternalProcessManager(process);
    processManager.SuspendProcess();

    var isBootstrapperInjected = process.Modules.Cast<ProcessModule>().Any(m => m.FileName.EndsWith("Korn.Bootstrapper.dll"));
    if (!isBootstrapperInjected)
    {
        if (hashedProcess.Name == "devenv")
        {
            Console.WriteLine($"Injecting in \"{hashedProcess.Name}\" process with pid {hashedProcess.ID}");

            using var injector = new Injector(process);

            injector.Inject(
                assemblyPath: Path.Combine(bootstrapperDirectory, "Korn.Bootstrapper.dll"),
                configPath: Path.Combine(bootstrapperDirectory, "Korn.Bootstrapper.runtimeconfig.json"),
                assemblyName: "Korn.Bootstrapper",
                classFullName: "Program",
                methodName: "ExternalMain"
            );
        }
    }

    processManager.ResumeProcess();
}