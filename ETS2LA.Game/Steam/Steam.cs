#if WINDOWS
using Microsoft.Win32;
#endif

using ETS2LA.Logging;

namespace ETS2LA.Game.Steam;

/// <summary>
///  Represents a single Steam library folder as listed in libraryfolders.vdf.
/// </summary>
public class SteamLibrary
{
    /// <summary>Root path of the library (the folder that contains steamapps).</summary>
    public required string Path { get; set; }
}

/// <summary>
///  This class is used by ETS2LA to discover and manage Steam library folders.
///  We use it to find all ETS2 and ATS installations automatically.
/// </summary>
class SteamHandler
{
    /// <summary>Steam app ID of Euro Truck Simulator 2.</summary>
    public const string EuroTruckSimulator2AppId = "227300";

    /// <summary>Steam app ID of American Truck Simulator.</summary>
    public const string AmericanTruckSimulatorAppId = "270880";

    /// <summary>
    ///  Gets the path of the current libraryfolders.vdf file.
    ///  This file includes information about where Steam games are installed.
    /// </summary>
    /// <returns>Location of libraryfolders.vdf.</returns>
    public static string GetLibraryVdfPath()
    {
        #if WINDOWS
            string steamInstallFolder = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null) as string ?? "C:\\Program Files (x86)\\Steam";
            return Path.Combine(steamInstallFolder, "steamapps", "libraryfolders.vdf");
        #else
            string path = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".steam", "root", "steamapps", "libraryfolders.vdf");
            return path;
        #endif
    }

    /// <summary>
    ///  Parses libraryfolders.vdf into a list of Steam libraries, each holding
    ///  its root path. This includes both default and user added libraries.
    /// </summary>
    /// <returns>All configured Steam libraries.</returns>
    public static List<SteamLibrary> GetLibraries()
    {
        string vdfPath = GetLibraryVdfPath();
        List<SteamLibrary> libraries = new();
        if (!File.Exists(vdfPath))
            return libraries;

        foreach (string rawLine in File.ReadAllLines(vdfPath))
        {
            string line = rawLine.Trim();

            // The "path" key marks the start of a new library entry.
            if (line.StartsWith("\"path\""))
                libraries.Add(new SteamLibrary { Path = line.Split('"')[3] });
        }

        return libraries;
    }

    /// <summary>
    ///  Finds the specified games across all Steam libraries by their app IDs.
    ///  An app ID is only resolved if its appmanifest is present in a library
    ///  and its install folder actually exists on disk.
    /// </summary>
    /// <param name="appIds">Steam app IDs to search for.</param>
    /// <returns>Map of found app IDs to their install paths.</returns>
    public static Dictionary<string, string> FindGamesInLibraries(List<string> appIds)
    {
        Dictionary<string, string> foundGames = new();

        foreach (SteamLibrary library in GetLibraries())
        {
            string steamApps = Path.Combine(library.Path, "steamapps");
            foreach (string appId in appIds)
            {
                if (foundGames.ContainsKey(appId))
                    continue;

                string? installDir = GetInstallDir(steamApps, appId);
                if (installDir == null)
                    continue;

                string gamePath = Path.Combine(steamApps, "common", installDir);
                if (Directory.Exists(gamePath))
                    foundGames[appId] = gamePath;
            }
        }

        return foundGames;
    }

    /// <summary>
    ///  Reads the install folder name of an app from its appmanifest_{appId}.acf
    ///  file. This is the name of the game's folder inside steamapps/common.
    /// </summary>
    /// <param name="steamAppsPath">Path to the library's steamapps folder.</param>
    /// <param name="appId">Steam app ID to look up.</param>
    /// <returns>The install folder name, or null if it can't be determined.</returns>
    private static string? GetInstallDir(string steamAppsPath, string appId)
    {
        string manifestPath = Path.Combine(steamAppsPath, $"appmanifest_{appId}.acf");
        if (!File.Exists(manifestPath))
            return null;

        foreach (string rawLine in File.ReadAllLines(manifestPath))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("\"installdir\""))
                return line.Split('"')[3];
        }

        return null;
    }
}
