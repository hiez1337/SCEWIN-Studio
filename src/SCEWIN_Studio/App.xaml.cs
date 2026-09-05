using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ModernWpf;
using SCEWIN_Studio.Services;

namespace SCEWIN_Studio;

public partial class App : Application
{
    public const string CrashLogFileName = "crash_startup.log";

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Kill any pre-existing orphan SCEWIN_64 processes before UI starts
            ScewinRunner.KillOrphanProcesses();

            base.OnStartup(e);
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
        }
        catch (Exception ex)
        {
            LogCrash("App.OnStartup", ex);
            ShowFatalErrorMessage(ex);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            ScewinRunner.KillOrphanProcesses();
        }
        catch
        {
        }
        base.OnExit(e);
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        try
        {
            ScewinRunner.KillOrphanProcesses();
        }
        catch
        {
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash("DispatcherUnhandledException", e.Exception);
        ShowFatalErrorMessage(e.Exception);
        e.Handled = false;
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception ?? new Exception($"Non-exception object: {e.ExceptionObject}");
        LogCrash("AppDomain.UnhandledException", ex);
        ShowFatalErrorMessage(ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    public static void LogCrash(string context, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"[CRASH REPORT] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
            sb.AppendLine($"Context: {context}");
            sb.AppendLine($"OS Version: {Environment.OSVersion}");
            sb.AppendLine($"64-Bit OS: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"Command Line: {Environment.CommandLine}");
            sb.AppendLine($"Current Directory: {Environment.CurrentDirectory}");
            sb.AppendLine($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
            sb.AppendLine($"User: {Environment.UserName}");

            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                sb.AppendLine($"Is Administrator: {principal.IsInRole(WindowsBuiltInRole.Administrator)}");
            }
            catch
            {
                sb.AppendLine("Is Administrator: Unknown");
            }

            if (ex != null)
            {
                sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"Stack Trace:\n{ex.StackTrace}");

                var inner = ex.InnerException;
                int depth = 1;
                while (inner != null)
                {
                    sb.AppendLine($"\n--- Inner Exception #{depth} ({inner.GetType().FullName}) ---");
                    sb.AppendLine($"Message: {inner.Message}");
                    sb.AppendLine($"Stack Trace:\n{inner.StackTrace}");
                    inner = inner.InnerException;
                    depth++;
                }
            }
            sb.AppendLine("================================================================================\n");

            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CrashLogFileName);
            File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            try
            {
                var fallbackPath = Path.Combine(Path.GetTempPath(), "SCEWIN_Studio_" + CrashLogFileName);
                File.AppendAllText(fallbackPath, $"[{DateTime.Now}] Crash logging in context: {context}\n{ex}\n", Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    private static void ShowFatalErrorMessage(Exception ex)
    {
        try
        {
            MessageBox.Show(
                $"SCEWIN Studio encountered a fatal error and must close:\n\n{ex.Message}\n\nTechnical details have been written to {CrashLogFileName}.",
                "SCEWIN Studio - Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
        }
    }
}
