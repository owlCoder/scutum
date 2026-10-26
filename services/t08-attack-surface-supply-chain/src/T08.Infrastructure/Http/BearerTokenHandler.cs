using System.Net.Http.Headers;

namespace T08.Infrastructure.Http;

/// <summary>
/// Dodaje <c>Authorization: Bearer</c> identitet servisa na odlazne pozive ka T01/T04/T11 (ugovori C-ASSET/C-ZONE/C-AUDIT:
/// scope i izvor događaja određuje upstream iz tokena, ne iz parametara). Token dolazi iz konfiguracije
/// (promenljiva okruženja), nikad iz koda. Ako token nije podešen, zaglavlje se ne šalje i upstream odbija poziv (fail closed).
/// Token se čita pri svakom pozivu, pa rotacija ne traži restart.
/// </summary>
public sealed class BearerTokenHandler(Func<string?> tokenSource) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = tokenSource();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
