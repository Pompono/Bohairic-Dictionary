using Microsoft.UI.Xaml;

namespace Bohairic.Desktop;

public partial class App : Application
{
    private Window? window;
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => LogError(e.Exception);
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            window = new MainWindow();
            window.Activate();
        }
        catch (Exception ex) { LogError(ex); throw; }
    }
    private static void LogError(Exception error)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BohairicDictionary");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "error.log"), $"{DateTimeOffset.Now:O}\n{error}\n\n");
        }
        catch { /* Preserve the original error if diagnostic storage is unavailable. */ }
    }
}
