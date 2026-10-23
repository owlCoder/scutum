using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace t12_tests
{
    public class HealthTests
    {
        [Test]
        public async Task HealthEndpoint_ReturnsOk()
        {
            using var client = new HttpClient();
            var baseUrl = Environment.GetEnvironmentVariable("TEST_BASE_URL") ?? "http://localhost:8012";
            var url = $"{baseUrl}/health";
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
            Assert.That(content.Trim(), Is.EqualTo("OK"));
        }

        [Test]
        public async Task HealthEndpoint_ReturnsNotFound()
        {
            using var client = new HttpClient();
            var baseUrl = Environment.GetEnvironmentVariable("TEST_BASE_URL") ?? "http://localhost:8012";
            var url = $"{baseUrl}/invalid";
            var response = await client.GetAsync(url);

            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.NotFound));

        }
    }
}
