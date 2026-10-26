using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using T08.Api;
using T08.Application.Audit;
using T08.Application.Directories;

namespace T08.Tests.Api;

/// <summary>
/// SEC-OPS-05 / 4.8: vrednost timeout-a koju <see cref="T08Host"/> stvarno podešava na upstream klijentima (T01, T11, T04) je 5 s.
/// Ponasanje pri isteku vremena pokrivaju <c>RealHttpClientTimeoutTests</c> (200 ms na samom klijentu); ovde se proverava samo podesena vrednost.
/// </summary>
[TestFixture]
public class UpstreamTimeoutConfigTests
{
    [Test]
    public async Task Upstream_http_clients_are_configured_with_a_5_second_timeout()
    {
        await using var app = T08Host.Build([
            "--urls=http://127.0.0.1:0", "--environment=Development",
            "--T08_DEV_TOKEN_KEY=0123456789abcdef0123456789abcdef",
            "--T01_BASE_URL=http://127.0.0.1:1", "--T11_BASE_URL=http://127.0.0.1:1", "--T04_BASE_URL=http://127.0.0.1:1",
            "--T08_SERVICE_TOKEN="]);

        var factory = app.Services.GetRequiredService<IHttpClientFactory>();

        // Imena tipiziranih klijenata: AddHttpClient<TPort, TImpl> koristi ime TPort, AddHttpClient<HttpAuditSink> ime HttpAuditSink.
        foreach (var name in new[] { nameof(IAssetReader), nameof(IZoneReader), "HttpAuditSink" })
        {
            Assert.That(factory.CreateClient(name).Timeout, Is.EqualTo(TimeSpan.FromSeconds(5)), name);
        }
    }
}
