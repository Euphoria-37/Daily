using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace DailyTodo
{
    public static class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle, int command);

        [STAThread]
        public static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "Local\\DailyTodo.Desktop.v1", out created))
            {
                if (!created)
                {
                    var handle = FindWindow(null, "Daily");
                    if (handle != IntPtr.Zero) { ShowWindow(handle, 9); SetForegroundWindow(handle); }
                    return;
                }
                try
                {
                    var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyTodo", "tasks.xml");
                    app.Run(new MainWindow(new TodoStore(path)));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Daily · 无法启动", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}

