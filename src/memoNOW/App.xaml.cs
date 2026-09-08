using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace memoNOW;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        HookDiagnostics();
        DiagnosticLog.Write($"START memoNOW; OS={Environment.OSVersion}; 64bitOS={Environment.Is64BitOperatingSystem}; 64bitProcess={Environment.Is64BitProcess}; runtime={RuntimeInformation.FrameworkDescription}");

        try
        {
            base.OnStartup(e);
            DiagnosticLog.Write("Application startup entered.");

            _mainWindow = new MainWindow();
            DiagnosticLog.Write("MainWindow constructed.");

            _mainWindow.Show();
            DiagnosticLog.Write("MainWindow.Show completed.");
        }
        catch (Exception ex)
        {
            DiagnosticLog.WriteException("fatal startup", ex);
            ShowFatalStartupMessage();
            Shutdown(-1);
        }
    }

    private void HookDiagnostics()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                DiagnosticLog.WriteException("AppDomain unhandled exception", ex);
            }
            else
            {
                DiagnosticLog.Write($"ERROR AppDomain unhandled exception: {args.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            DiagnosticLog.WriteException("unobserved task exception", args.Exception);
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        DiagnosticLog.WriteException("dispatcher unhandled exception", e.Exception);
        e.Handled = false;
    }

    private static void ShowFatalStartupMessage()
    {
        try
        {
            System.Windows.MessageBox.Show(
                $"memoNOW の起動中にエラーが発生しました。\n\n診断ログ:\n{DiagnosticLog.CurrentLogPath}",
                "memoNOW 起動エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
        }
    }
}
