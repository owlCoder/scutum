using System.Collections.Concurrent;
using T08.Application.Directories;

namespace T08.Infrastructure.Stubs;

/// <summary>
/// Stub za R1-02 dok T01 ne objavi servis (SEC-DEP-02). Podržava normalan i failure ishod;
/// namerno nema security logiku T01 (SEC-DEP-03): samo vraća ono što mu je upisano.
/// </summary>
public sealed class StubAssetDirectory : IAssetDirectory
{
    private readonly ConcurrentDictionary<string, AssetInfo> _assets = new();

    /// <summary>Simulira pad T01 (503).</summary>
    public bool Unavailable { get; set; }

    public StubAssetDirectory Add(AssetInfo asset)
    {
        _assets[asset.Id] = asset;
        return this;
    }

    public Task<DirectoryResult<AssetInfo>> GetAsync(string assetId, Guid correlationId, CancellationToken ct = default)
    {
        if (Unavailable) return Task.FromResult(DirectoryResult<AssetInfo>.Unavailable());
        if (!_assets.TryGetValue(assetId, out var asset)) return Task.FromResult(DirectoryResult<AssetInfo>.NotFound());
        return Task.FromResult(asset.Status == AssetStatus.Decommissioned
            ? DirectoryResult<AssetInfo>.Inactive()
            : DirectoryResult<AssetInfo>.Found(asset));
    }
}

/// <summary>Stub za R1-14 dok T11 ne objavi servis; isto pravilo kao <see cref="StubAssetDirectory"/>.</summary>
public sealed class StubZoneDirectory : IZoneDirectory
{
    private readonly ConcurrentDictionary<string, ZoneInfo> _zones = new();

    public bool Unavailable { get; set; }

    public StubZoneDirectory Add(ZoneInfo zone)
    {
        _zones[zone.Id] = zone;
        return this;
    }

    public Task<DirectoryResult<ZoneInfo>> GetAsync(string zoneId, Guid correlationId, CancellationToken ct = default)
    {
        if (Unavailable) return Task.FromResult(DirectoryResult<ZoneInfo>.Unavailable());
        if (!_zones.TryGetValue(zoneId, out var zone)) return Task.FromResult(DirectoryResult<ZoneInfo>.NotFound());
        return Task.FromResult(zone.Status == ZoneStatus.Inactive
            ? DirectoryResult<ZoneInfo>.Inactive()
            : DirectoryResult<ZoneInfo>.Found(zone));
    }
}
