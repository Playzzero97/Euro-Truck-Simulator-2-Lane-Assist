using Velopack;
using Velopack.Sources;

namespace ETS2LA.Networking.Updates;

public class UpdaterSource
{
    public IUpdateSource source;
    public string sourceName;

    public UpdaterSource(IUpdateSource source, string sourceName)
    {
        this.source = source;
        this.sourceName = sourceName;
    }
}

[Serializable]
public class UpdaterSettings
{
    public string? SelectedSource { get; set; }
    public bool IsSourceSelectedByUser { get; set; }
}