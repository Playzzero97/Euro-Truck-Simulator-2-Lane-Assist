using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ETS2LA.UI;

/// <summary>
///  The application class for ETS2LA's user interface.
///  This class is responsible for starting MainWindow.axaml.cs and initializing the UI.
/// 
///  If you're a plugin developer, you should instead look at ETS2LA.Overlay, there are docs
///  for that at https://docs.ets2la.com.
/// </summary>
public partial class App : Application
{
    public static Action? OnMainThreadTick;  // set this from Program.cs

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        #if DEBUG
            this.AttachDeveloperTools();
        #endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            OnMainThreadTick?.Invoke();
        }, Avalonia.Threading.DispatcherPriority.Render);

        base.OnFrameworkInitializationCompleted();
    }
}