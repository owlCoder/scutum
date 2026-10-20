using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Scutum.T10.IntegrationTests;

public class HealthEndpointTests
{
    [Test]
    public async Task HealthVraca200()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
