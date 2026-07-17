using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;
using Optris.Icons.Avalonia.MaterialDesign;

namespace ETS2LA.UI;

/// <summary>
///  The main entrypoint for ETS2LA's user interface.
///  This class will call App.axaml.cs to start the UI.
/// </summary>
public class Program
{
    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>()
            .Register<MaterialDesignIconProvider>();

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI();
    }

    public static void Main(string[] args, Action? afterSetup = null)
    {
        var builder = BuildAvaloniaApp();

        #if MACOSX
            builder = builder.AfterSetup(_ =>
            {
                afterSetup?.Invoke();

                var timer = new Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
                };
                timer.Tick += (_, _) => ETS2LA.Overlay.OverlayHandler.Current.RenderFrame();
                timer.Start();
            });
        #else
            if (afterSetup != null)
                builder = builder.AfterSetup(_ => afterSetup());
        #endif

        builder.StartWithClassicDesktopLifetime(args);
    }
}