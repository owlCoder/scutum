using T08.Application.Directories;

namespace T08.Infrastructure.Directories;

/// <summary>Adapter ka R1-02 (T01), ugovor C-ASSET-v1. BaseAddress se postavlja konfiguracijom (npr. T01_BASE_URL).</summary>
public sealed class HttpAssetDirectory(HttpClient http) : IAssetReader
{
    private sealed record Dto(string? Id, string? OwnerId, string? Criticality, string? Status, string? Scope);

    public Task<DirectoryResult<AssetInfo>> GetAsync(string assetId, Guid correlationId, CancellationToken ct = default) =>
        DirectoryHttp.GetAsync<Dto, AssetInfo>(http, "assets", assetId, correlationId, dto => Map(assetId, dto), ct);

    private static DirectoryResult<AssetInfo> Map(string requestedId, Dto dto)
    {
        if (dto.Id != requestedId
            || string.IsNullOrWhiteSpace(dto.OwnerId)
            || string.IsNullOrWhiteSpace(dto.Scope)
            || !DirectoryHttp.TryParseEnum<AssetCriticality>(dto.Criticality, out var criticality)
            || !DirectoryHttp.TryParseEnum<AssetStatus>(dto.Status, out var status))
        {
            return DirectoryResult<AssetInfo>.Unavailable();   // neispravan odgovor nije uspeh
        }

        return status != AssetStatus.Active
            ? DirectoryResult<AssetInfo>.Inactive()
            : DirectoryResult<AssetInfo>.Found(new AssetInfo(requestedId, dto.OwnerId, criticality, status, dto.Scope));
    }
}
