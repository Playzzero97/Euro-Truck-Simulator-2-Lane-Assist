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

    /// <summary>App IDs of the games installed in this library.</summary>
    public List<string> AppIds { get; set; } = new();
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
        #elif MACOSX
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Steam", "steamapps", "libraryfolders.vdf");
        #else
            string path = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".steam", "root", "steamapps", "libraryfolders.vdf");
            return path;
        #endif
    }

    /// <summary>
    ///  Parses libraryfolders.vdf into a list of Steam libraries. Each library
    ///  holds its root path and the app IDs of the games installed in it.
    ///  This includes both default and user added libraries.
    /// </summary>
    /// <returns>All configured Steam libraries.</returns>
    public static List<SteamLibrary> GetLibraries()
    {
        string vdfPath = GetLibraryVdfPath();
        List<SteamLibrary> libraries = new();
        if (!File.Exists(vdfPath))
            return libraries;

        SteamLibrary? current = null;
        bool inAppsBlock = false;
        foreach (string rawLine in File.ReadAllLines(vdfPath))
        {
            string line = rawLine.Trim();

            // The "path" key marks the start of a new library entry.
            if (line.StartsWith("\"path\""))
            {
                current = new SteamLibrary { Path = line.Split('"')[3] };
                libraries.Add(current);
                inAppsBlock = false;
            }
            // The "apps" key opens a block listing the installed app IDs.
            else if (line.StartsWith("\"apps\""))
            {
                inAppsBlock = true;
            }
            else if (inAppsBlock)
            {
                // Entries look like:  "227300"   "12345678"  where the key is the
                // app ID and the value is its size on disk. The block ends with "}".
                if (line.StartsWith("}"))
                    inAppsBlock = false;
                else if (line.StartsWith("\"") && current != null)
                    current.AppIds.Add(line.Split('"')[1]);
            }
        }

        return libraries;
    }

    /// <summary>
    ///  Finds the specified games across all Steam libraries by their app IDs.
    ///  An app ID is only resolved if libraryfolders.vdf reports it as installed
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
                if (foundGames.ContainsKey(appId) || !library.AppIds.Contains(appId))
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
#if WINDOWS
using Microsoft.Win32;
#endif

namespace ETS2LA.Game.Steam;

// TODO: Switch from names to IDs. Those are stored in libraryfolders.vdf directly.

/// <summary>
///  This class is used by ETS2LA to discover and manage Steam library folders.
///  We use it to find all ETS2 and ATS installations automatically.
/// </summary>
class SteamHandler
{
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
        #elif MACOSX
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Steam", "steamapps", "libraryfolders.vdf");
        #else
            return Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".steam", "root", "steamapps", "libraryfolders.vdf");
        #endif
    }

    /// <summary>
    ///  Gets a list of all current steam library folders.
    ///  This includes both default and user added libraries.
    /// </summary>
    /// <returns>Libraries as a string List.</returns>
    public static List<string> GetLibraryFolders()
    {
        string vdfPath = GetLibraryVdfPath();
        if (!File.Exists(vdfPath))
            return new List<string>();

        List<string> libraryFolders = new();
        foreach (string line in File.ReadAllLines(vdfPath))
        {
           #if WINDOWS
                if (line.Trim().StartsWith("\"") && line.Contains("\"path\""))
                {
                    string path = line.Split('"')[3];
                    libraryFolders.Add(Path.Combine(path, "steamapps", "common"));
                }
            #else
                if (line.Contains("\"path\""))
                {
                    string path = line.Split('"')[3];
                    libraryFolders.Add(Path.Combine(path, "steamapps", "common"));
                }
            #endif
        }

        return libraryFolders;
    }

    /// <summary>
    ///  Finds the specified games in all library folders. Game names
    ///  should be the names of the game folder.
    /// </summary>
    /// <param name="gameNames">List of games to search for.</param>
    /// <returns>List of found game paths.</returns>
    public static List<string> FindGamesInLibraries(List<string> gameNames)
    {
        List<string> foundGames = new();
        List<string> libraryFolders = GetLibraryFolders();

        foreach (string library in libraryFolders)
        {
            foreach (string gameName in gameNames)
            {
                string gamePath = Path.Combine(library, gameName);
                if (Directory.Exists(gamePath))
                {
                    foundGames.Add(gamePath);
                }
            }
        }

        return foundGames;
    }
}