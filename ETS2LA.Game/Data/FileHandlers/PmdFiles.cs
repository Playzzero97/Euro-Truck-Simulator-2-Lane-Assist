using TruckLib;
using TruckLib.Models.Ppd;
using TruckLib.Models;

using ETS2LA.Logging;
using ETS2LA.Game.SiiFiles;


namespace ETS2LA.Game.PmdFiles;

public class PmdFileHandler
{
    private static readonly Lazy<PmdFileHandler> _instance = new(() => new PmdFileHandler());
    public static PmdFileHandler Current => _instance.Value;

    Dictionary<string, string> _modelPathCache = new Dictionary<string, string>();
    Dictionary<string, Model> _modelCache = new Dictionary<string, Model>();
    
    IFileSystem? _fs;

    public void SetFileSystem(IFileSystem fs)
    {
        _fs = fs;
    }

    private void UpdateModelPathCache()
    {
        if (_fs == null)
        {
            Logger.Error("File system not set for PmdFileHandler. Cannot update model path cache.");
            return;
        }

        SiiFileHandler.Current.SetFileSystem(_fs);
        var modelFiles = _fs.GetFiles("/def/world/");
        modelFiles = modelFiles.Where(f => f.EndsWith(".sii") && f.Contains("model") && !f.Contains("curve")).ToArray();
        foreach (var file in modelFiles)
        {
            try
            {
                var sii = SiiFileHandler.Current.GetSiiFile(file);
                if (sii == null)
                {
                    Logger.Error($"Failed to load {file} for model path cache.");
                    continue;
                }

                foreach (var unit in sii.Units)
                {
                    try
                    {
                        var token = unit.Name.Split('.').Last();
                        var path = unit.Attributes["model_desc"].Trim('"');
                        if (!_modelPathCache.ContainsKey(token))
                        {
                            _modelPathCache[token] = path;
                        }
                    } catch { }
                }
            } catch { }
        }
    }

    private Model? LoadPmdModel(string path)
    {
        try
        {
            if(_modelPathCache.Count == 0) UpdateModelPathCache();
            if (!_modelPathCache.ContainsKey(path))
            {
                Logger.Error($"Model token {path} not found in model path cache.");
                return null;
            }

            path = _modelPathCache[path];
            return Model.Open(path, _fs);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load PMD file at {path}: {ex}");
            return null;
        }
    }

    public Model? GetPmdModel(string path)
    {
        if (_modelCache.ContainsKey(path))
        {
            return _modelCache[path];
        }

        var file = LoadPmdModel(path);
        if (file != null)
        {
            _modelCache[path] = file;
        }

        return file;
    }
}