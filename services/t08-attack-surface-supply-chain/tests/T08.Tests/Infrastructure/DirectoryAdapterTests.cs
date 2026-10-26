using System.Net;
using System.Text;
using T08.Application.Directories;
using T08.Infrastructure.Directories;
using T08.Infrastructure.Stubs;

namespace T08.Tests.Infrastructure;

/// <summary>Lažni HTTP upstream: vraća unapred zadat odgovor i pamti poslednji zahtev.</summary>
internal sealed class FakeUpstream(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        LastRequest = request;
        return Task.FromResult(respond(request));
    }

    public static FakeUpstream Json(HttpStatusCode status, string body) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    public static FakeUpstream Throws(Exception ex) => new(_ => throw ex);

    public HttpClient Client() => new(this) { BaseAddress = new Uri("http://upstream.test/") };
}

[TestFixture]
public class HttpAssetDirectoryTests
{
    private static readonly Guid Corr = Guid.NewGuid();

    private const string ValidAsset =
        """{"id":"asset-1","ownerId":"user-1","criticality":"Critical","status":"Active","scope":"scope-a"}""";

    [Test]
    public async Task Found_asset_is_mapped_and_correlation_id_is_sent()
    {
        var upstream = FakeUpstream.Json(HttpStatusCode.OK, ValidAsset);

        var result = await new HttpAssetDirectory(upstream.Client()).GetAsync("asset-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Found));
        Assert.That(result.Value, Is.EqualTo(new AssetInfo("asset-1", "user-1", AssetCriticality.Critical, AssetStatus.Active, "scope-a")));
        Assert.That(upstream.LastRequest!.RequestUri!.AbsolutePath, Is.EqualTo("/api/v1/assets/asset-1"));
        Assert.That(upstream.LastRequest.Headers.GetValues("X-Correlation-Id").Single(), Is.EqualTo(Corr.ToString()));
    }

    [TestCase(HttpStatusCode.NotFound, DirectoryOutcome.NotFound)]
    [TestCase(HttpStatusCode.Conflict, DirectoryOutcome.Inactive)]
    [TestCase(HttpStatusCode.ServiceUnavailable, DirectoryOutcome.Unavailable)]
    [TestCase(HttpStatusCode.InternalServerError, DirectoryOutcome.Unavailable)]
    [TestCase(HttpStatusCode.Unauthorized, DirectoryOutcome.Unavailable)]
    public async Task Upstream_status_is_mapped_to_stable_outcome(HttpStatusCode status, DirectoryOutcome expected)
    {
        var result = await new HttpAssetDirectory(FakeUpstream.Json(status, """{"code":"X","message":"m"}""").Client())
            .GetAsync("asset-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(expected));
        Assert.That(result.Value, Is.Null);
    }

    [Test]
    public async Task Decommissioned_asset_in_200_body_is_inactive_not_found()
    {
        var body = ValidAsset.Replace("\"Active\"", "\"Decommissioned\"");

        var result = await new HttpAssetDirectory(FakeUpstream.Json(HttpStatusCode.OK, body).Client()).GetAsync("asset-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Inactive));
    }

    [TestCase("""{"id":"asset-2","ownerId":"u","criticality":"High","status":"Active","scope":"s"}""", Description = "tuđ id")]
    [TestCase("""{"id":"asset-1","ownerId":"","criticality":"High","status":"Active","scope":"s"}""", Description = "bez vlasnika")]
    [TestCase("""{"id":"asset-1","ownerId":"u","criticality":"Extreme","status":"Active","scope":"s"}""", Description = "nepoznata kritičnost")]
    [TestCase("""{"id":"asset-1","ownerId":"u","criticality":"2","status":"Active","scope":"s"}""", Description = "broj umesto imena")]
    [TestCase("""{"id":"asset-1","ownerId":"u","criticality":"High","status":"Active"}""", Description = "bez scope-a")]
    [TestCase("not json", Description = "nije JSON")]
    [TestCase("null", Description = "prazno telo")]
    public async Task Malformed_or_mismatched_200_is_unavailable_never_success(string body)
    {
        var result = await new HttpAssetDirectory(FakeUpstream.Json(HttpStatusCode.OK, body).Client()).GetAsync("asset-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Unavailable));
        Assert.That(result.Value, Is.Null);
    }

    [Test]
    public async Task Network_failure_is_unavailable()
    {
        var upstream = FakeUpstream.Throws(new HttpRequestException("down"));

        var result = await new HttpAssetDirectory(upstream.Client()).GetAsync("asset-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Unavailable));
    }

    [Test]
    public async Task Timeout_is_unavailable()
    {
        var upstream = FakeUpstream.Throws(new TaskCanceledException("timeout"));

        var result = await new HttpAssetDirectory(upstream.Client()).GetAsync("asset-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Unavailable));
    }

    [TestCase("../zones/zone-1")]
    [TestCase("Asset-1")]
    [TestCase("asset-1\n")]
    [TestCase("")]
    public async Task Invalid_identifier_is_not_sent_upstream(string id)
    {
        var upstream = FakeUpstream.Json(HttpStatusCode.OK, ValidAsset);

        var result = await new HttpAssetDirectory(upstream.Client()).GetAsync(id, Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.NotFound));
        Assert.That(upstream.Calls, Is.Zero);
    }
}

[TestFixture]
public class HttpZoneDirectoryTests
{
    private static readonly Guid Corr = Guid.NewGuid();
    private const string ValidZone = """{"id":"zone-1","ownerId":"user-2","trustLevel":"Dmz","status":"Active"}""";

    [Test]
    public async Task Found_zone_is_mapped()
    {
        var upstream = FakeUpstream.Json(HttpStatusCode.OK, ValidZone);

        var result = await new HttpZoneDirectory(upstream.Client()).GetAsync("zone-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Found));
        Assert.That(result.Value, Is.EqualTo(new ZoneInfo("zone-1", "user-2", ZoneTrustLevel.Dmz, ZoneStatus.Active)));
        Assert.That(upstream.LastRequest!.RequestUri!.AbsolutePath, Is.EqualTo("/api/v1/zones/zone-1"));
    }

    [TestCase(HttpStatusCode.NotFound, DirectoryOutcome.NotFound)]
    [TestCase(HttpStatusCode.Conflict, DirectoryOutcome.Inactive)]
    [TestCase(HttpStatusCode.ServiceUnavailable, DirectoryOutcome.Unavailable)]
    public async Task Upstream_status_is_mapped_to_stable_outcome(HttpStatusCode status, DirectoryOutcome expected)
    {
        var result = await new HttpZoneDirectory(FakeUpstream.Json(status, "{}").Client()).GetAsync("zone-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(expected));
    }

    [Test]
    public async Task Inactive_zone_in_200_body_is_inactive()
    {
        var body = ValidZone.Replace("\"Active\"", "\"Inactive\"");

        var result = await new HttpZoneDirectory(FakeUpstream.Json(HttpStatusCode.OK, body).Client()).GetAsync("zone-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Inactive));
    }

    [Test]
    public async Task Response_for_another_zone_is_unavailable()
    {
        var body = ValidZone.Replace("zone-1", "zone-9");

        var result = await new HttpZoneDirectory(FakeUpstream.Json(HttpStatusCode.OK, body).Client()).GetAsync("zone-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Unavailable));
    }
}

/// <summary>Stubovi moraju davati iste ishode kao pravi adapteri, da isti zahtevi važe nad oba (SEC-DEP-04).</summary>
[TestFixture]
public class StubDirectoryTests
{
    private static readonly Guid Corr = Guid.NewGuid();

    [Test]
    public async Task Asset_stub_covers_found_not_found_inactive_and_unavailable()
    {
        var stub = new StubAssetDirectory()
            .Add(new AssetInfo("asset-1", "u", AssetCriticality.High, AssetStatus.Active, "s"))
            .Add(new AssetInfo("asset-old", "u", AssetCriticality.Low, AssetStatus.Decommissioned, "s"));

        Assert.That((await stub.GetAsync("asset-1", Corr)).Outcome, Is.EqualTo(DirectoryOutcome.Found));
        Assert.That((await stub.GetAsync("asset-x", Corr)).Outcome, Is.EqualTo(DirectoryOutcome.NotFound));
        Assert.That((await stub.GetAsync("asset-old", Corr)).Outcome, Is.EqualTo(DirectoryOutcome.Inactive));

        stub.Unavailable = true;
        Assert.That((await stub.GetAsync("asset-1", Corr)).Outcome, Is.EqualTo(DirectoryOutcome.Unavailable));
    }

    [Test]
    public async Task Zone_stub_covers_found_not_found_inactive_and_unavailable()
    {
        var stub = new StubZoneDirectory()
            .Add(new ZoneInfo("zone-1", "u", ZoneTrustLevel.External, ZoneStatus.Active))
            .Add(new ZoneInfo("zone-old", "u", ZoneTrustLevel.Internal, ZoneStatus.Inactive));

        Assert.That((await stub.GetAsync("zone-1", Corr)).Outcome, Is.EqualTo(DirectoryOutcome.Found));
        Assert.That((await stub.GetAsync("zone-x", Corr)).Outcome, Is.EqualTo(DirectoryOutcome.NotFound));
        Assert.That((await stub.GetAsync("zone-old", Corr)).Outcome, Is.EqualTo(DirectoryOutcome.Inactive));

        stub.Unavailable = true;
        Assert.That((await stub.GetAsync("zone-1", Corr)).Outcome, Is.EqualTo(DirectoryOutcome.Unavailable));
    }
}
