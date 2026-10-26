using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using T08.Application.Exposures;

namespace T08.Infrastructure.Security;

/// <summary>
/// Razvojna zamena za identitet (R1-03, T12) i dozvole (R1-05, T02) dok njihovi ugovori ne postoje.
/// Token je <c>base64url(payload).base64url(HMAC-SHA256)</c> sa <c>sub</c>, <c>scope</c>, <c>perms</c> i <c>exp</c>.
/// <see cref="ActorContext"/> se gradi isključivo iz validiranog tokena, nikad iz tela zahteva (SEC-CONV-02).
/// Bez podešenog ključa (<see cref="MinKeyBytes"/>+ bajtova) nijedan token nije validan (fail closed).
/// Zamenjuje se pravim validatorom kad T12/T02 objave ugovor; nije mehanizam za produkciju.
/// </summary>
public sealed class DevActorTokens(string? key, TimeProvider time)
{
    public const int MinKeyBytes = 32;

    private sealed record Payload(string? Sub, string? Scope, string[]? Perms, long Exp);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly byte[]? _key = key is not null && Encoding.UTF8.GetByteCount(key) >= MinKeyBytes
        ? Encoding.UTF8.GetBytes(key)
        : null;

    public bool Enabled => _key is not null;

    /// <summary>Izdaje token za razvoj i testove. Baca izuzetak ako ključ nije podešen.</summary>
    public string Issue(string actorId, string scope, IEnumerable<string> permissions, TimeSpan lifetime)
    {
        if (_key is null) throw new InvalidOperationException("Razvojni ključ nije podešen.");

        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new Payload(actorId, scope, permissions.ToArray(), time.GetUtcNow().Add(lifetime).ToUnixTimeSeconds()), Json);
        var body = Base64Url(payload);
        return $"{body}.{Base64Url(Sign(body))}";
    }

    /// <summary>Vraća kontekst samo za ispravan, nepodmetnut i neistekao token sa obaveznim sub i scope.</summary>
    public bool TryValidate(string? token, out ActorContext? actor)
    {
        actor = null;
        if (_key is null || string.IsNullOrWhiteSpace(token) || token.Length > 4096) return false;

        var parts = token.Split('.');
        if (parts.Length != 2) return false;

        try
        {
            if (!CryptographicOperations.FixedTimeEquals(Sign(parts[0]), FromBase64Url(parts[1]))) return false;

            var payload = JsonSerializer.Deserialize<Payload>(FromBase64Url(parts[0]), Json);
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.Sub)
                || string.IsNullOrWhiteSpace(payload.Scope)
                || payload.Exp <= time.GetUtcNow().ToUnixTimeSeconds())
            {
                return false;
            }

            actor = new ActorContext(payload.Sub, payload.Scope, (payload.Perms ?? []).ToHashSet(StringComparer.Ordinal));
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private byte[] Sign(string body) => HMACSHA256.HashData(_key!, Encoding.ASCII.GetBytes(body));

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
