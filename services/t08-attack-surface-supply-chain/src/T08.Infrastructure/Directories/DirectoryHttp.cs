using System.Net.Http.Json;
using System.Text.Json;
using T08.Application.Directories;

namespace T08.Infrastructure.Directories;

/// <summary>
/// Zajednički HTTP klijent za čitanje iz tuđih servisa. Odgovor drugog tima je spoljni ulaz (trust granica 2):
/// ne-200 ishodi se mapiraju na stabilne ishode, a svaka greška, timeout ili neispravan odgovor je
/// <see cref="DirectoryOutcome.Unavailable"/>, nikad prazan uspeh.
/// </summary>
internal static class DirectoryHttp
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<DirectoryResult<T>> GetAsync<TDto, T>(
        HttpClient http,
        string collection,
        string id,
        Guid correlationId,
        Func<TDto, DirectoryResult<T>> map,
        CancellationToken ct)
        where TDto : class
        where T : class
    {
        // Neispravan identifikator se ne šalje upstream-u (nema path injection-a) i ne može postojati.
        if (!ContractIdentifier.IsValid(id)) return DirectoryResult<T>.NotFound();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/{collection}/{id}");
        request.Headers.Add("X-Correlation-Id", correlationId.ToString());

        try
        {
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            switch ((int)response.StatusCode)
            {
                case 200:
                    var dto = await response.Content.ReadFromJsonAsync<TDto>(Json, ct).ConfigureAwait(false);
                    return dto is null ? DirectoryResult<T>.Unavailable() : map(dto);
                case 404:
                    return DirectoryResult<T>.NotFound();
                case 409:
                    return DirectoryResult<T>.Inactive();
                default:
                    return DirectoryResult<T>.Unavailable();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return DirectoryResult<T>.Unavailable();
        }
    }

    /// <summary>Enum iz upstream odgovora: samo tačno ime vrednosti, bez brojeva i nepoznatih vrednosti.</summary>
    public static bool TryParseEnum<TEnum>(string? raw, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        return !string.IsNullOrEmpty(raw)
               && !char.IsDigit(raw[0]) && raw[0] != '-'
               && Enum.TryParse(raw, ignoreCase: false, out value)
               && Enum.IsDefined(value);
    }
}
