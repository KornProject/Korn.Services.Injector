using Korn.Interface.ServiceModule;

static class DeveloperTools
{
    public static void SetLocalLibraries()
    {
        const string kornPath = @"C:\Data\programming\vs projects\korn";

        var librariesList = Korn.Interface.ServiceModule.Libraries.DeserializeLibrariesList();
        var libraries = librariesList.Libraries;
        foreach (var library in libraries)
        {
            if (!string.IsNullOrEmpty(library.LocalFilePath)) 
                continue;

            var version = library.TargetVersion == "net8" ? "net8.0-windows" : "net472";
            var localPath = Path.Combine(kornPath, library.Name, library.Name, "bin", "x64", "Debug", version, library.Name + ".dll");
            library.LocalFilePath = localPath;
        }

        librariesList.Save();
    }
}