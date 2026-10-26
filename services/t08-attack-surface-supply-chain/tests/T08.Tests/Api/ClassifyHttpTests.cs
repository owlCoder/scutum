using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
/// R1-18-UC4 preko pravog HTTP-a (Kestrel na lokalnom portu, pravi <see cref="T08Host"/>): 401, 403, 404, oblik zahteva i matrica po ulogama.
/// Mapiranje uloga u dozvole ne postoji u kodu (čeka T12/T02), pa test uloge predstavlja skupom dozvola u tokenu, kako ih matrica definiše.
/// </summary>
[TestFixture]
public class ClassifyHttpTests
{
    private const string Key = "unit-test-key-unit-test-key-unit-test-key";
    private const string Scope = "scope-a";
    private const string Classify = Permissions.ExposureClassify;

    private WebApplication _app = null!;
    private HttpClient _http = null!;
    private DevActorTokens _tokens = null!;
    private InMemoryExposureRepository _repo = null!;
    private InMemoryAuditSink _audit = null!;

    [SetUp]
    public async Task StartHost()
    {
        // Prazne vrednosti isključuju upstream servise iz okruženja mašine: uvek stub i lokalni audit.
        _app = T08Host.Build([
            "--urls=http://127.0.0.1:0", "--environment=Development",
            $"--T08_DEV_TOKEN_KEY={Key}", "--T01_BASE_URL=", "--T11_BASE_URL=", "--T04_BASE_URL=", "--T08_SERVICE_TOKEN="]);

        _app.Services.GetRequiredService<StubAssetDirectory>()
            .Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Low, AssetStatus.Active, Scope));
        _repo = _app.Services.GetRequiredService<InMemoryExposureRepository>();
        _repo.Add(Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", Scope, "javni API partnera"));
        _repo.Add(Exposure.Register("e-other", "asset-1", "zone-1", "owner-1", "scope-b", "tuđi scope"));
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

    private string Token(string actor = "u-1", string scope = Scope, params string[] perms) =>
        _tokens.Issue(actor, scope, perms, TimeSpan.FromMinutes(5));

    private Task<HttpResponseMessage> Post(string id, string? bearer, string json = """{"classification":"Approved"}""",
        Guid? correlation = null, bool withCorrelation = true, string scheme = "Bearer")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/exposures/{id}/classification")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue(scheme, bearer);
        if (withCorrelation) request.Headers.Add("X-Correlation-Id", (correlation ?? Guid.NewGuid()).ToString());
        return _http.SendAsync(request);
    }

    private static async Task<string> CodeOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString()!;
    }

    private async Task<Classification> StoredClassification(string id) => (await _repo.GetAsync(id))!.Classification;

    // ---------- 401 ----------

    [Test]
    public async Task Missing_token_is_401_with_challenge_and_changes_and_audits_nothing()
    {
        var response = await Post("e-1", bearer: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.WwwAuthenticate.ToString(), Is.EqualTo("Bearer"));
        Assert.That(await CodeOf(response), Is.EqualTo("UNAUTHENTICATED"));
        Assert.That(await StoredClassification("e-1"), Is.EqualTo(Classification.Unknown));
        Assert.That(_audit.Events, Is.Empty);
    }

    [Test]
    public async Task Garbage_wrong_scheme_and_tampered_tokens_are_401()
    {
        var good = Token(perms: Classify);
        var tampered = good[..^2] + (good[^1] == 'A' ? "BB" : "AA");
        var otherKey = new DevActorTokens("another-key-another-key-another-key-xx", TimeProvider.System)
            .Issue("u-1", Scope, [Classify], TimeSpan.FromMinutes(5));

        foreach (var (token, scheme) in new[] { ("not-a-token", "Bearer"), (tampered, "Bearer"), (otherKey, "Bearer"), (good, "Basic") })
        {
            var response = await Post("e-1", token, scheme: scheme);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), $"{scheme} {token[..Math.Min(12, token.Length)]}");
        }

        Assert.That(await StoredClassification("e-1"), Is.EqualTo(Classification.Unknown));
        Assert.That(_audit.Events, Is.Empty);
    }

    [Test]
    public async Task Expired_token_is_401()
    {
        var expired = _tokens.Issue("u-1", Scope, [Classify], TimeSpan.FromSeconds(-5));

        var response = await Post("e-1", expired);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await StoredClassification("e-1"), Is.EqualTo(Classification.Unknown));
    }

    [Test]
    public async Task Host_without_dev_key_rejects_every_token_fail_closed()
    {
        await using var noKey = T08Host.Build([
            "--urls=http://127.0.0.1:0", "--T08_DEV_TOKEN_KEY=", "--T01_BASE_URL=", "--T11_BASE_URL=", "--T04_BASE_URL="]);
        await noKey.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(noKey.Urls.First()) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/exposures/e-1/classification")
        {
            Content = JsonContent.Create(new { classification = "Approved" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(perms: Classify));
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        await noKey.StopAsync();
    }

    // ---------- 403 ----------

    [Test]
    public async Task Actor_without_classify_permission_is_403_and_the_denial_is_audited_with_the_token_identity()
    {
        var correlation = Guid.NewGuid();

        var response = await Post("e-1", Token("u-ro", perms: "exposure.read"), correlation: correlation);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await CodeOf(response), Is.EqualTo("FORBIDDEN"));
        Assert.That(await StoredClassification("e-1"), Is.EqualTo(Classification.Unknown));
        var denied = _audit.Events.Single();
        Assert.That(denied.Outcome, Is.EqualTo(AuditOutcome.Failure));
        Assert.That(denied.Target, Is.EqualTo("exposure:e-1"));
        Assert.That(denied.CorrelationId, Is.EqualTo(correlation));
        Assert.That(denied.Actor, Is.EqualTo("u-ro"));
    }

    // Matrica iz docs/security-matrix.md: Allow za SA i AN; Deny za RO, AU i X.
    [TestCase("SA", true)]
    [TestCase("AN", true)]
    [TestCase("RO", false)]
    [TestCase("AU", false)]
    [TestCase("X", false)]
    public async Task Role_matrix_allows_only_SA_and_AN_to_classify(string role, bool allowed)
    {
        string[] perms = role switch
        {
            "SA" or "AN" => [Classify, "exposure.read"],
            "RO" or "AU" => ["exposure.read"],
            _ => []
        };

        var response = await Post("e-1", Token($"u-{role}", perms: perms));

        Assert.That(response.StatusCode, Is.EqualTo(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden));
        Assert.That(await StoredClassification("e-1"), Is.EqualTo(allowed ? Classification.Approved : Classification.Unknown));
    }

    // ---------- 404 ----------

    [Test]
    public async Task Missing_foreign_scope_and_malformed_ids_give_the_same_404_even_without_permission()
    {
        var withPerm = Token(perms: Classify);
        var withoutPerm = Token();

        var missing = await Post("e-nope", withPerm);
        var foreign = await Post("e-other", withPerm);
        var foreignNoPerm = await Post("e-other", withoutPerm);
        var malformed = await Post("%2E%2E%2Fsecrets", withPerm);

        foreach (var response in new[] { missing, foreign, foreignNoPerm, malformed })
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("""{"code":"NOT_FOUND"}"""));
        }

        Assert.That(await StoredClassification("e-other"), Is.EqualTo(Classification.Unknown));
    }

    [Test]
    public async Task Foreign_scope_is_audited_internally_but_a_missing_id_is_not()
    {
        await Post("e-nope", Token(perms: Classify));
        Assert.That(_audit.Events, Is.Empty);

        await Post("e-other", Token("u-x", perms: Classify));

        var denied = _audit.Events.Single();
        Assert.That(denied.Outcome, Is.EqualTo(AuditOutcome.Failure));
        Assert.That(denied.Target, Is.EqualTo("exposure:e-other"));
    }

    // ---------- oblik zahteva ----------

    [Test]
    public async Task Missing_or_invalid_correlation_id_is_400()
    {
        var token = Token(perms: Classify);

        var missing = await Post("e-1", token, withCorrelation: false);
        var empty = await Post("e-1", token, correlation: Guid.Empty);

        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodeOf(missing), Is.EqualTo("INVALID_CORRELATION_ID"));
        Assert.That(empty.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await StoredClassification("e-1"), Is.EqualTo(Classification.Unknown));
    }

    [TestCase("""{"classification":1}""")]
    [TestCase("""{"classification":"approved"}""")]
    [TestCase("""{"classification":"7"}""")]
    [TestCase("""{"classification":"-1"}""")]
    [TestCase("""{"classification":""}""")]
    [TestCase("""{}""")]
    public async Task Invalid_classification_value_is_400_and_changes_nothing(string json)
    {
        var response = await Post("e-1", Token(perms: Classify), json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await StoredClassification("e-1"), Is.EqualTo(Classification.Unknown));
    }

    [TestCase("not json")]
    [TestCase("null")]
    [TestCase("")]
    public async Task Malformed_body_is_400(string json)
    {
        var response = await Post("e-1", Token(perms: Classify), json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await StoredClassification("e-1"), Is.EqualTo(Classification.Unknown));
    }

    [Test]
    public async Task Oversized_body_is_rejected_and_changes_nothing()
    {
        var json = $$"""{"classification":"Approved","note":"{{new string('x', 8192)}}"}""";

        var response = await Post("e-1", Token(perms: Classify), json);

        Assert.That((int)response.StatusCode, Is.GreaterThanOrEqualTo(400).And.LessThan(500));
        Assert.That(await StoredClassification("e-1"), Is.EqualTo(Classification.Unknown));
    }

    [Test]
    public async Task Temporary_without_expiry_is_400_with_the_domain_code()
    {
        var response = await Post("e-1", Token(perms: Classify), """{"classification":"Temporary"}""");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodeOf(response), Is.EqualTo("INVALID_EXPIRY"));
    }

    // ---------- uspeh i identitet ----------

    [Test]
    public async Task Allowed_actor_classifies_and_actor_comes_from_the_token_not_the_body()
    {
        var json = """{"classification":"Approved","actor":"mallory","actorId":"mallory","scope":"scope-b","permissions":["x"]}""";

        var response = await Post("e-1", Token("u-sa", perms: Classify), json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("classification").GetString(), Is.EqualTo("Approved"));
        Assert.That(doc.RootElement.GetProperty("changed").GetBoolean(), Is.True);
        Assert.That(await StoredClassification("e-1"), Is.EqualTo(Classification.Approved));
        Assert.That(_audit.Events.Single().Actor, Is.EqualTo("u-sa"));
    }

    [Test]
    public async Task Repeating_the_same_classification_is_200_unchanged_without_a_second_audit_event()
    {
        var token = Token(perms: Classify);
        await Post("e-1", token);

        var response = await Post("e-1", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("changed").GetBoolean(), Is.False);
        Assert.That(_audit.Events.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Asset_service_down_is_503_and_nothing_changes()
    {
        _app.Services.GetRequiredService<StubAssetDirectory>().Unavailable = true;

        var response = await Post("e-1", Token(perms: Classify));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(await CodeOf(response), Is.EqualTo("UPSTREAM_UNAVAILABLE"));
        Assert.That(await StoredClassification("e-1"), Is.EqualTo(Classification.Unknown));
        Assert.That(_audit.Events, Is.Empty);
    }
}
