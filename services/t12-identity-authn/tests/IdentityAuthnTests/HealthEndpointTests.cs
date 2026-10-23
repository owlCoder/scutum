using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;

namespace IdentityAuthnTests;

public class HealthEndpointTests
{

    [Test]
    public async Task HealthEndpoint_ReturnsOk()
    {
        using var client = new HttpClient();
        var url = "http://localhost:8012/health";
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        Assert.That(content, Is.EqualTo("OK"));
    }

    [Test]
    public async Task HealthEndpoint_ReturnsNotFound()
    {
        using var client = new HttpClient();
        var url = "http://localhost:8012/invalid";
        var response = await client.GetAsync(url);

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.NotFound));

    }

}
