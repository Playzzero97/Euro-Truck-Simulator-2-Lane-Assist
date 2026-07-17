using ETS2LA.Backend;
using ETS2LA.Shared;

namespace ETS2LA.UI.Services;

public sealed class PluginManagerService
{
    public PluginBackend backend = null!;

    public PluginManagerService()
    {
        backend = PluginBackend.Current;
        Task.Run(() => backend.Start());
    }

    public List<IPlugin> GetPlugins()
    {
        if (backend.PluginHandler == null)
            return new List<IPlugin>();
        
        return backend.PluginHandler.LoadedPlugins;
    }

    public void UnloadPlugins()
    {
        backend.PluginHandler?.UnloadPlugins();
    }

    public void ReloadPlugins()
    {
        backend.PluginHandler?.UnloadPlugins();
        backend.PluginHandler?.LoadPlugins();
    }
    

    public bool SetEnabled(IPlugin plugin, bool enable)
    {
        var ok = enable
            ? backend.PluginHandler!.EnablePlugin(plugin)
            : backend.PluginHandler!.DisablePlugin(plugin);

        return ok;
    }

    public void Shutdown()
    {
    }
}