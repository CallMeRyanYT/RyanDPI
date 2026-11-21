using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using RyanDPI.Services;

namespace RyanDPI
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;
        private const string MutexName = "Global\\RyanDPI_SingleInstance_Mutex";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            LogService.Initialize();

            // Single-instance check
            try
            {
                _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
                if (!createdNew)
                {
                    MessageBox.Show(
                        "RyanDPI is already running. Check your taskbar or end RyanDPI.exe in Task Manager.",
                        "RyanDPI Already Running",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    Shutdown();
                    return;
                }
            }
            catch
            {
                // Continue if mutex check fails
            }

            // Verify Administrator rights
            if (!IsRunningAsAdministrator())
            {
                MessageBox.Show(
                    "RyanDPI requires Administrator privileges to manage Windows network services and encrypted DNS.\n\nPlease right-click RyanDPI.exe and select 'Run as administrator'.",
                    "Administrator Privileges Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        public static void ApplyTheme(bool isDark)
        {
            var path = isDark
                ? "pack://application:,,,/RyanDPI;component/Themes/DarkTheme.xaml"
                : "pack://application:,,,/RyanDPI;component/Themes/LightTheme.xaml";
            var dictUri = new Uri(path, UriKind.Absolute);
            var dictionaries = Current.Resources.MergedDictionaries;
            dictionaries.Clear();
            dictionaries.Add(new ResourceDictionary { Source = dictUri });
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogService.Error($"Unhandled UI exception: {e.Exception}");
            MessageBox.Show(
                $"An unexpected error occurred:\n{e.Exception.Message}",
                "RyanDPI Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogService.Error($"Critical unhandled exception: {e.ExceptionObject}");
            MessageBox.Show(
                $"A critical error occurred:\n{e.ExceptionObject}",
                "RyanDPI Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            }
            catch
            {
                // Ignore mutex cleanup error
            }

            base.OnExit(e);
        }
    }
}
