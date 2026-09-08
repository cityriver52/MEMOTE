namespace memoNOW;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        _mainWindow = new MainWindow();
        _mainWindow.Show();
    }
}
