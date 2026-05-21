using CPE262_FINAL_PROJECT.Database;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace CPE262_FINAL_PROJECT
{
    public partial class App : Application
    {
        private Window? _window;

        public Window? MainWindow => _window;
        public static string? DatabaseStartupError { get; private set; }

        public App() { InitializeComponent(); }

        public static bool LaunchBackgroundLoginWindow()
        {
            try
            {
                var executablePath = Environment.ProcessPath
                    ?? Process.GetCurrentProcess().MainModule?.FileName;

                if (string.IsNullOrWhiteSpace(executablePath))
                    return false;

                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--background",
                    UseShellExecute = false,
                    WorkingDirectory = AppContext.BaseDirectory
                });

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to launch background login window: {ex.Message}");
                return false;
            }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                DatabaseHelper.InitializeDatabase();
                DatabaseStartupError = null;
            }
            catch (Exception ex)
            {
                DatabaseStartupError = $"Database initialization failed: {ex.Message}";
            }

            _window = new MainWindow();
            _window.Activate();

            bool isBackground = Environment.GetCommandLineArgs()
                .Any(a => a.Equals("--background", StringComparison.OrdinalIgnoreCase));

            if (isBackground)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                _window.DispatcherQueue.TryEnqueue(
                    Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                    () =>
                    {
                        SetWindowPos(hwnd, new IntPtr(1), 0, 0, 0, 0, 0x0013);
                    });
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
    }
}
