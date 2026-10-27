using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace Scutum.T28.Tests.Api;

public sealed class HealthEndpointTests
{
    [Test]
    public async Task Health_ReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("OK"));
    }
}
