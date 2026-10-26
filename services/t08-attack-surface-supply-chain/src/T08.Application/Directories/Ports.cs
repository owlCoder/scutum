namespace T08.Application.Directories;

public enum AssetCriticality { Low, Medium, High, Critical }

/// <summary>T01 razlikuje Inactive i Decommissioned; za T08 oba znače „ne vezuj novo“.</summary>
public enum AssetStatus { Active, Inactive, Decommissioned }

/// <summary>Asset kako ga vidi T08 (C-ASSET-v1). Referenca, ne kopija: čita se pri svakoj odluci.</summary>
public sealed record AssetInfo(string Id, string OwnerId, AssetCriticality Criticality, AssetStatus Status, string Scope);

/// <summary>Port ka R1-02 (T01).</summary>
public interface IAssetReader
{
    Task<DirectoryResult<AssetInfo>> GetAsync(string assetId, Guid correlationId, CancellationToken ct = default);
}

public enum ZoneTrustLevel { Internal, Dmz, External }

public enum ZoneStatus { Active, Inactive }

/// <summary>Security zona kako je vidi T08 (C-ZONE-v1).</summary>
public sealed record ZoneInfo(string Id, string OwnerId, ZoneTrustLevel TrustLevel, ZoneStatus Status);

/// <summary>Port ka R1-14 (T11).</summary>
public interface IZoneReader
{
    Task<DirectoryResult<ZoneInfo>> GetAsync(string zoneId, Guid correlationId, CancellationToken ct = default);
}
