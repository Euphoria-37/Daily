// Isolated updater integration-test executable. Never reads user settings or tasks.
using System.IO;
using System.Reflection;
public static class UpdateProbe
{
    public static void Main()
    {
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "update-started.txt"), "started");
    }
}
