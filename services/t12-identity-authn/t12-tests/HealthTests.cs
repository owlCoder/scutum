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
            var url = "http://localhost:8012/health";
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
            Assert.That(content.Trim(), Is.EqualTo("OK"));
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
}
