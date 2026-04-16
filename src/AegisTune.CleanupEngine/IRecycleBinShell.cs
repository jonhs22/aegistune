using System.Runtime.Versioning;

namespace AegisTune.CleanupEngine;

[SupportedOSPlatform("windows")]
public interface IRecycleBinShell
{
    RecycleBinSnapshot Query();

    void Empty();
}

public sealed record RecycleBinSnapshot(
    bool IsAvailable,
    long ItemCount,
    long TotalBytes,
    string? Note = null)
{
    public bool HasFindings => ItemCount > 0 || TotalBytes > 0;
}
