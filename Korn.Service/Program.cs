using Korn.Utils.System;
using System.Diagnostics;
using System.Management;

var startWatch = new ManagementEventWatcher(
      new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
startWatch.EventArrived += startWatch_EventArrived;
startWatch.Start();

Thread.Sleep(TimeSpan.MaxValue);

void startWatch_EventArrived(object sender, EventArrivedEventArgs e)
{
    var properties = e.NewEvent.Properties;
    var name = (string)properties["ProcessName"].Value;
    var id = (int)(uint)properties["ProcessID"].Value;

    var a = DateTime.Now.Ticks;
    Console.WriteLine("Process started: {0}", name);
    if (name == "ConsoleTest.exe")
    {
        var process = Process.GetProcessById(id);
        using var processManager = new ExternalProcessManager(process);

        processManager.SuspendProcess();

        _ = 3;
    }

    _ = 3;
}