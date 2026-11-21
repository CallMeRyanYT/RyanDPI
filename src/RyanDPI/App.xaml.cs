using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using RyanDPI.Models;
using RyanDPI.Services;

namespace RyanDPI
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;
        private const string MutexName = "Global\\RyanDPI_SingleInstance_Mutex";

        public static string? UpdatedToVersion { get; private set; }
        public static bool SkipAutoUpdateCheck { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ParseCommandLineArgs(e.Args);
            LocalizationService.SetLanguage(RyanDpiSettings.Load().Language);
            LogService.Initialize();

            // Single-instance check
            try
            {
                _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
                if (!createdNew)
                {
                    MessageBox.Show(
                        LocalizationService.T("Dlg_AlreadyRunningBody"),
                        LocalizationService.T("Dlg_AlreadyRunningTitle"),
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
                    LocalizationService.T("Dlg_AdminRequiredBody"),
                    LocalizationService.T("Dlg_AdminRequiredTitle"),
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
                LocalizationService.Tf("Dlg_ErrorBody", e.Exception.Message),
                LocalizationService.T("Dlg_ErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogService.Error($"Critical unhandled exception: {e.ExceptionObject}");
            MessageBox.Show(
                LocalizationService.Tf("Dlg_CriticalBody", e.ExceptionObject),
                LocalizationService.T("Dlg_CriticalTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        public static void ParseCommandLineArgs(string[]? args)
        {
            if (args == null || args.Length == 0) return;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--no-update-check", StringComparison.OrdinalIgnoreCase))
                {
                    SkipAutoUpdateCheck = true;
                }
                else if (args[i].Equals("--updated", StringComparison.OrdinalIgnoreCase))
                {
                    SkipAutoUpdateCheck = true;
                    UpdatedToVersion = (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        ? args[++i]
                        : AutoUpdateService.GetCurrentVersionString();
                }
            }
        }

        public static void ReleaseSingleInstanceMutex()
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
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ReleaseSingleInstanceMutex();
            base.OnExit(e);
        }
    }
}
