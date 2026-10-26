using T08.Application.Directories;

namespace T08.Infrastructure.Directories;

/// <summary>Adapter ka R1-14 (T11), ugovor C-ZONE-v1. BaseAddress se postavlja konfiguracijom (npr. T11_BASE_URL).</summary>
public sealed class HttpZoneDirectory(HttpClient http) : IZoneDirectory
{
    private sealed record Dto(string? Id, string? OwnerId, string? TrustLevel, string? Status);

    public Task<DirectoryResult<ZoneInfo>> GetAsync(string zoneId, Guid correlationId, CancellationToken ct = default) =>
        DirectoryHttp.GetAsync<Dto, ZoneInfo>(http, "zones", zoneId, correlationId, dto => Map(zoneId, dto), ct);

    private static DirectoryResult<ZoneInfo> Map(string requestedId, Dto dto)
    {
        if (dto.Id != requestedId
            || string.IsNullOrWhiteSpace(dto.OwnerId)
            || !DirectoryHttp.TryParseEnum<ZoneTrustLevel>(dto.TrustLevel, out var trustLevel)
            || !DirectoryHttp.TryParseEnum<ZoneStatus>(dto.Status, out var status))
        {
            return DirectoryResult<ZoneInfo>.Unavailable();   // neispravan odgovor nije uspeh
        }

        return status == ZoneStatus.Inactive
            ? DirectoryResult<ZoneInfo>.Inactive()
            : DirectoryResult<ZoneInfo>.Found(new ZoneInfo(requestedId, dto.OwnerId, trustLevel, status));
    }
}
