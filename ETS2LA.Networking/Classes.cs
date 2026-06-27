namespace ETS2LA.Networking;

[Serializable]
public struct ApiServer
{
    public required string Name { get; set; }
    public required string BaseUrl { get; set; }
}