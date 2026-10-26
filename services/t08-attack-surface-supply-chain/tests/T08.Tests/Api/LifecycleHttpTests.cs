using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using T08.Api;
using T08.Application.Audit;
using T08.Application.Directories;
using T08.Application.Exposures;
using T08.Domain.Exposures;
using T08.Infrastructure.Audit;
using T08.Infrastructure.Persistence;
using T08.Infrastructure.Security;
using T08.Infrastructure.Stubs;

namespace T08.Tests.Api;

/// <summary>
/// Register / confirm / close preko pravog HTTP-a (isti host kao <see cref="ClassifyHttpTests"/>).
/// Uloge se predstavljaju skupom dozvola u tokenu (mapiranje SA/AN → dozvola čeka T12/T02).
/// </summary>
[TestFixture]
public class LifecycleHttpTests
{
    private const string Key = "unit-test-key-unit-test-key-unit-test-key";
    private const string Scope = "scope-a";

    private WebApplication _app = null!;
    private HttpClient _http = null!;
    private DevActorTokens _tokens = null!;
    private InMemoryExposureRepository _repo = null!;
    private InMemoryAuditSink _audit = null!;
    private StubAssetDirectory _assets = null!;

    [SetUp]
    public async Task StartHost()
    {
        _app = T08Host.Build([
            "--urls=http://127.0.0.1:0", "--environment=Development",
            $"--T08_DEV_TOKEN_KEY={Key}", "--T01_BASE_URL=", "--T11_BASE_URL=", "--T04_BASE_URL=", "--T08_SERVICE_TOKEN="]);

        _assets = _app.Services.GetRequiredService<StubAssetDirectory>();
        _assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Low, AssetStatus.Active, Scope));
        _assets.Add(new AssetInfo("asset-crit", "owner-1", AssetCriticality.Critical, AssetStatus.Active, Scope));
        _assets.Add(new AssetInfo("asset-foreign", "owner-9", AssetCriticality.Low, AssetStatus.Active, "scope-b"));
        _app.Services.GetRequiredService<StubZoneDirectory>()
            .Add(new ZoneInfo("zone-1", "owner-1", ZoneTrustLevel.Internal, ZoneStatus.Active));

        _repo = _app.Services.GetRequiredService<InMemoryExposureRepository>();
        _repo.Add(Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", Scope, "javni API partnera"));
        _repo.Add(Exposure.Register("e-other", "asset-foreign", "zone-1", "owner-9", "scope-b", "tuđi scope"));
        _audit = _app.Services.GetRequiredService<InMemoryAuditSink>();
        _tokens = new DevActorTokens(Key, TimeProvider.System);

        await _app.StartAsync();
        _http = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    [TearDown]
    public async Task StopHost()
    {
        _http.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private string Token(string actor, params string[] perms) =>
        _tokens.Issue(actor, Scope, perms, TimeSpan.FromMinutes(5));

    private Task<HttpResponseMessage> Send(string path, string? bearer, string? json = null, bool withCorrelation = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (withCorrelation) request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        return _http.SendAsync(request);
    }

    private const string RegisterBody = """{"assetId":"asset-1","zoneId":"zone-1","justification":"novi javni endpoint"}""";

    private static async Task<JsonElement> BodyOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static async Task<string> CodeOf(HttpResponseMessage response) => (await BodyOf(response)).GetProperty("code").GetString()!;

    // ---------- 401: svaka ruta, bez ikakve promene i audita ----------

    [TestCase("/api/v1/exposures", RegisterBody)]
    [TestCase("/api/v1/exposures/e-1/confirmation", null)]
    [TestCase("/api/v1/exposures/e-1/closure", """{"reasonCode":"no-longer-needed"}""")]
    public async Task Missing_or_invalid_token_is_401_and_changes_and_audits_nothing(string path, string? json)
    {
        foreach (var bearer in new string?[] { null, "not-a-token" })
        {
            var response = await Send(path, bearer, json);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(await CodeOf(response), Is.EqualTo("UNAUTHENTICATED"));
        }

        Assert.That(_audit.Events, Is.Empty);
        Assert.That((await _repo.GetAsync("e-1"))!.Status, Is.EqualTo(ExposureStatus.Active));
    }

    [TestCase("/api/v1/exposures", RegisterBody)]
    [TestCase("/api/v1/exposures/e-1/confirmation", null)]
    [TestCase("/api/v1/exposures/e-1/closure", """{"reasonCode":"no-longer-needed"}""")]
    public async Task Missing_correlation_id_is_400(string path, string? json)
    {
        var token = Token("owner-1", Permissions.ExposureRegister, Permissions.ExposureConfirm, Permissions.ExposureClose);

        var response = await Send(path, token, json, withCorrelation: false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodeOf(response), Is.EqualTo("INVALID_CORRELATION_ID"));
        Assert.That(_audit.Events, Is.Empty);
    }

    // ---------- register ----------

    [Test]
    public async Task Register_returns_201_takes_owner_and_scope_from_the_asset_and_audits_with_the_token_actor()
    {
        var response = await Send("/api/v1/exposures", Token("owner-1", Permissions.ExposureRegister), RegisterBody);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await BodyOf(response);
        var id = body.GetProperty("exposureId").GetString()!;
        Assert.That(id, Does.StartWith("exp-"));
        Assert.That(body.GetProperty("auditDelivered").GetBoolean(), Is.True);

        var stored = (await _repo.GetAsync(id))!;
        Assert.That((stored.OwnerId, stored.Scope, stored.Classification), Is.EqualTo(("owner-1", Scope, Classification.Unknown)));

        var audited = _audit.Events.Single();
        Assert.That((audited.Action, audited.Actor, audited.Outcome),
            Is.EqualTo((ExposureAudit.Registered, "owner-1", AuditOutcome.Success)));
    }

    [Test]
    public async Task Register_ignores_owner_and_scope_sent_in_the_body()
    {
        var json = """{"assetId":"asset-1","zoneId":"zone-1","justification":"x","ownerId":"attacker","scope":"scope-b"}""";

        var response = await Send("/api/v1/exposures", Token("owner-1", Permissions.ExposureRegister), json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var stored = (await _repo.GetAsync((await BodyOf(response)).GetProperty("exposureId").GetString()!))!;
        Assert.That((stored.OwnerId, stored.Scope), Is.EqualTo(("owner-1", Scope)));
    }

    [Test]
    public async Task Register_of_a_critical_asset_writes_both_the_registered_and_the_critical_event()
    {
        var json = """{"assetId":"asset-crit","zoneId":"zone-1","justification":"kritičan servis"}""";

        var response = await Send("/api/v1/exposures", Token("owner-1", Permissions.ExposureRegister), json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(_audit.Events.Select(e => e.Action),
            Is.EquivalentTo(new[] { ExposureAudit.Registered, ExposureAudit.CriticalExposureChanged }));
    }

    [Test]
    public async Task Register_without_permission_is_403_and_the_denial_is_audited()
    {
        var response = await Send("/api/v1/exposures", Token("owner-1", Permissions.ExposureClassify), RegisterBody);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await CodeOf(response), Is.EqualTo("FORBIDDEN"));
        Assert.That(_audit.Events.Single().Outcome, Is.EqualTo(AuditOutcome.Failure));
        Assert.That(_repo.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Register_for_somebody_elses_asset_without_manage_any_is_403()
    {
        var response = await Send("/api/v1/exposures", Token("u-2", Permissions.ExposureRegister), RegisterBody);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(_repo.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Register_with_manage_any_works_for_somebody_elses_asset()
    {
        var token = Token("u-sa", Permissions.ExposureRegister, Permissions.ExposureManageAny);

        var response = await Send("/api/v1/exposures", token, RegisterBody);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Register_for_an_asset_in_another_scope_looks_like_an_unusable_asset()
    {
        var json = """{"assetId":"asset-foreign","zoneId":"zone-1","justification":"x"}""";

        var response = await Send("/api/v1/exposures", Token("owner-9", Permissions.ExposureRegister), json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodeOf(response), Is.EqualTo("ASSET_NOT_USABLE"));
        Assert.That(_repo.Count, Is.EqualTo(2));
    }

    [TestCase("""{"assetId":"asset-1","zoneId":"zone-1"}""")]                              // nema justification
    [TestCase("""{"assetId":"Bad Id!","zoneId":"zone-1","justification":"x"}""")]          // neispravan format
    [TestCase("""{"zoneId":"zone-1","justification":"x"}""")]                              // nema assetId
    public async Task Register_with_an_invalid_body_is_400_INVALID_EXPOSURE(string json)
    {
        var response = await Send("/api/v1/exposures", Token("owner-1", Permissions.ExposureRegister), json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodeOf(response), Is.EqualTo("INVALID_EXPOSURE"));
        Assert.That(_repo.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Register_with_malformed_or_oversized_body_is_400_INVALID_REQUEST()
    {
        var token = Token("owner-1", Permissions.ExposureRegister);

        var malformed = await Send("/api/v1/exposures", token, "{not json");
        var oversized = await Send("/api/v1/exposures", token, $$"""{"assetId":"asset-1","zoneId":"zone-1","justification":"{{new string('x', 5000)}}"}""");

        Assert.That(malformed.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(oversized.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodeOf(oversized), Is.EqualTo("INVALID_REQUEST"));
        Assert.That(_repo.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Register_with_an_unknown_zone_is_409_ZONE_NOT_USABLE()
    {
        var json = """{"assetId":"asset-1","zoneId":"zone-nope","justification":"x"}""";

        var response = await Send("/api/v1/exposures", Token("owner-1", Permissions.ExposureRegister), json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodeOf(response), Is.EqualTo("ZONE_NOT_USABLE"));
        Assert.That(_repo.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Register_when_T01_is_down_is_503_and_nothing_is_written()
    {
        _assets.Unavailable = true;

        var response = await Send("/api/v1/exposures", Token("owner-1", Permissions.ExposureRegister), RegisterBody);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(await CodeOf(response), Is.EqualTo("UPSTREAM_UNAVAILABLE"));
        Assert.That(_repo.Count, Is.EqualTo(2));
        Assert.That(_audit.Events, Is.Empty);
    }

    // ---------- confirm ----------

    [Test]
    public async Task Confirm_by_the_owner_is_200_and_audited_without_a_critical_event()
    {
        var response = await Send("/api/v1/exposures/e-1/confirmation", Token("owner-1", Permissions.ExposureConfirm));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await _repo.GetAsync("e-1"))!.LastConfirmedAt, Is.Not.Null);
        var audited = _audit.Events.Single();
        Assert.That((audited.Action, audited.Actor), Is.EqualTo((ExposureAudit.Confirmed, "owner-1")));
    }

    [Test]
    public async Task Confirm_by_a_non_owner_without_manage_any_is_403_and_changes_nothing()
    {
        var response = await Send("/api/v1/exposures/e-1/confirmation", Token("u-2", Permissions.ExposureConfirm));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await _repo.GetAsync("e-1"))!.LastConfirmedAt, Is.Null);
        Assert.That(_audit.Events.Single().Outcome, Is.EqualTo(AuditOutcome.Failure));
    }

    [Test]
    public async Task Confirm_gives_the_same_404_for_unknown_malformed_and_foreign_scope_ids()
    {
        var token = Token("owner-1", Permissions.ExposureConfirm);

        var bodies = new List<string>();
        foreach (var id in new[] { "e-nope", "Bad%20Id", "e-other" })
        {
            var response = await Send($"/api/v1/exposures/{id}/confirmation", token);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), id);
            bodies.Add(await response.Content.ReadAsStringAsync());
        }

        Assert.That(bodies.Distinct().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Confirm_when_T01_is_down_is_503_and_changes_nothing()
    {
        _assets.Unavailable = true;

        var response = await Send("/api/v1/exposures/e-1/confirmation", Token("owner-1", Permissions.ExposureConfirm));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That((await _repo.GetAsync("e-1"))!.LastConfirmedAt, Is.Null);
    }

    // ---------- close ----------

    [Test]
    public async Task Close_by_the_owner_is_200_and_terminal()
    {
        var token = Token("owner-1", Permissions.ExposureClose, Permissions.ExposureConfirm);

        var first = await Send("/api/v1/exposures/e-1/closure", token, """{"reasonCode":"no-longer-needed"}""");
        var second = await Send("/api/v1/exposures/e-1/closure", token, """{"reasonCode":"no-longer-needed"}""");
        var confirmAfter = await Send("/api/v1/exposures/e-1/confirmation", token);

        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await _repo.GetAsync("e-1"))!.Status, Is.EqualTo(ExposureStatus.Closed));
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodeOf(second), Is.EqualTo("EXPOSURE_CLOSED"));
        Assert.That(confirmAfter.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(_audit.Events.Count(e => e.Action == ExposureAudit.Closed), Is.EqualTo(1));
    }

    [TestCase("""{}""")]
    [TestCase("""{"reasonCode":"Free text with spaces!"}""")]
    public async Task Close_with_a_missing_or_free_text_reason_is_400_and_stays_active(string json)
    {
        var response = await Send("/api/v1/exposures/e-1/closure", Token("owner-1", Permissions.ExposureClose), json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodeOf(response), Is.EqualTo("INVALID_EXPOSURE"));
        Assert.That((await _repo.GetAsync("e-1"))!.Status, Is.EqualTo(ExposureStatus.Active));
    }

    [Test]
    public async Task Close_without_permission_is_403_and_stays_active()
    {
        var response = await Send("/api/v1/exposures/e-1/closure", Token("owner-1", Permissions.ExposureConfirm),
            """{"reasonCode":"no-longer-needed"}""");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await _repo.GetAsync("e-1"))!.Status, Is.EqualTo(ExposureStatus.Active));
    }

    [Test]
    public async Task Close_still_works_when_the_asset_is_gone_and_is_audited_as_critical()
    {
        _assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Low, AssetStatus.Decommissioned, Scope));

        var response = await Send("/api/v1/exposures/e-1/closure", Token("owner-1", Permissions.ExposureClose),
            """{"reasonCode":"asset-decommissioned"}""");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_audit.Events.Select(e => e.Action),
            Is.EquivalentTo(new[] { ExposureAudit.Closed, ExposureAudit.CriticalExposureChanged }));
    }

    [Test]
    public async Task Close_when_T01_is_down_is_503_and_stays_active()
    {
        _assets.Unavailable = true;

        var response = await Send("/api/v1/exposures/e-1/closure", Token("owner-1", Permissions.ExposureClose),
            """{"reasonCode":"no-longer-needed"}""");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That((await _repo.GetAsync("e-1"))!.Status, Is.EqualTo(ExposureStatus.Active));
    }
}
