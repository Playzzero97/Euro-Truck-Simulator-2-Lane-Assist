// TODO: Refactor ETS2LA.Game data loading.
//       Currently it's not up to the standards I want.

#if WINDOWS
using Microsoft.Win32;
using System.Diagnostics;
#endif

using ETS2LA.Logging;
using ETS2LA.Game.SDK;
using ETS2LA.Game.Steam;
using ETS2LA.Game.Output;
using ETS2LA.Shared;


namespace ETS2LA.Game;

public class GameHandler
{
    private static readonly Lazy<GameHandler> _instance = new(() => new GameHandler());
    public static GameHandler Current => _instance.Value;
    
    public List<Installation> Installations { get; } = new();

    public GameHandler()
    {
        PopulateInstallations();

        // Spawn all the SDK readers. They'll start
        // sending out events as they read the game.
        var camera = CameraProvider.Current;
        var navigation = NavigationProvider.Current;
        var semaphores = SemaphoreProvider.Current;
        var traffic = TrafficProvider.Current;
        var parkedVehicles = ParkedVehiclesProvider.Current;

        // Spawn the game output handler as well.
        var output = GameOutput.Current;
    }

    private void PopulateInstallations()
    {
        Dictionary<string, string> games = SteamHandler.FindGamesInLibraries(new List<string>
        {
            SteamHandler.EuroTruckSimulator2AppId,
            SteamHandler.AmericanTruckSimulatorAppId
        });

        Logger.Info($"Found {games.Count} game installations.");
        foreach ((string appId, string gamePath) in games)
        {
            GameType type = appId == SteamHandler.EuroTruckSimulator2AppId
                            ? GameType.EuroTruckSimulator2
                            : GameType.AmericanTruckSimulator;

            string executablePath = Path.Combine(
                # if WINDOWS
                    gamePath, 
                    "bin", 
                    "win_x64", 
                    type == GameType.EuroTruckSimulator2 ? "eurotrucks2.exe" 
                                                         : "amtrucks.exe"
                # elif MACOSX
                    gamePath, 
                    "bin", 
                    "macOS",
                    type == GameType.EuroTruckSimulator2 ? "eurotrucks2" 
                                                         : "amtrucks"
                # else
                    gamePath, 
                    "bin", 
                    "linux_x64", 
                    type == GameType.EuroTruckSimulator2 ? "eurotrucks2" 
                                                         : "amtrucks"
                    gamePath, 
                    "bin", 
                    "linux_x64", 
                    type == GameType.EuroTruckSimulator2 ? "eurotrucks2" 
                                                         : "amtrucks"
                # endif
            );

            string gameName = type == GameType.EuroTruckSimulator2 ? "Euro Truck Simulator 2" 
                                                                   : "American Truck Simulator";
            string documentsPath = Path.Combine(
                #if WINDOWS
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                    gameName
                #else
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local", "share", gameName
                #endif
            );

            string version = "Unknown";
            # if WINDOWS
                try
                {
                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(executablePath);
                    version = info.FileVersion != null ? (info.FileVersion.Split(".")[0] + "." + info.FileVersion.Split(".")[1]) : version;
                }
                catch (FileNotFoundException ex)
                {
                    Logger.Warn($"Executable not found at '{executablePath}': {ex.Message}");
                }
            # endif
            // TODO: Is there a way we can somehow get the version automatically on linux?

            Installations.Add(new Installation
            {
                Type = type,
                Path = gamePath,
                DocumentsPath = documentsPath,
                ExecutablePath = executablePath,
                Version = version
            });

            Installation installation = Installations[^1];
        }
    }
}