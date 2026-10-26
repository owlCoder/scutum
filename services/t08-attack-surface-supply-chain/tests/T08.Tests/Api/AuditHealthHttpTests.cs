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
/// Vidljivost audit reda (<c>/health/audit</c>) i end-to-end provera da tajne iz zahteva ne završe u audit događajima.
/// Isti host kao <see cref="LifecycleHttpTests"/>.
/// </summary>
[TestFixture]
public class AuditHealthHttpTests
{
    private const string Key = "unit-test-key-unit-test-key-unit-test-key";
    private const string Scope = "scope-a";

    private WebApplication _app = null!;
    private HttpClient _http = null!;
    private DevActorTokens _tokens = null!;
    private InMemoryAuditSink _audit = null!;
    private BufferedAuditSink _buffer = null!;

    [SetUp]
    public async Task StartHost()
    {
        _app = T08Host.Build([
            "--urls=http://127.0.0.1:0", "--environment=Development",
            $"--T08_DEV_TOKEN_KEY={Key}", "--T01_BASE_URL=", "--T11_BASE_URL=", "--T04_BASE_URL=", "--T08_SERVICE_TOKEN="]);

        _app.Services.GetRequiredService<StubAssetDirectory>()
            .Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Critical, AssetStatus.Active, Scope));
        _app.Services.GetRequiredService<StubZoneDirectory>()
            .Add(new ZoneInfo("zone-1", "owner-1", ZoneTrustLevel.Internal, ZoneStatus.Active));
        _app.Services.GetRequiredService<InMemoryExposureRepository>()
            .Add(Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", Scope, "javni API partnera"));

        _audit = _app.Services.GetRequiredService<InMemoryAuditSink>();
        _buffer = _app.Services.GetRequiredService<BufferedAuditSink>();
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

    private Task<HttpResponseMessage> Post(string path, string json)
    {
        var token = _tokens.Issue("owner-1", Scope,
            [Permissions.ExposureRegister, Permissions.ExposureClose], TimeSpan.FromMinutes(5));
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        return _http.SendAsync(request);
    }

    private async Task<(HttpStatusCode Status, int Pending, int Dropped, string State)> AuditHealth()
    {
        var response = await _http.GetAsync("/health/audit");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        return (response.StatusCode, root.GetProperty("pending").GetInt32(), root.GetProperty("dropped").GetInt32(),
            root.GetProperty("status").GetString()!);
    }

    [Test]
    public async Task Audit_health_is_ok_and_empty_when_nothing_is_waiting()
    {
        var health = await AuditHealth();

        Assert.That(health, Is.EqualTo((HttpStatusCode.OK, 0, 0, "ok")));
    }

    [Test]
    public async Task Audit_health_shows_buffered_events_while_T04_is_down_and_drains_them_when_it_is_back()
    {
        _audit.Unavailable = true;
        var response = await Post("/api/v1/exposures", """{"assetId":"asset-1","zoneId":"zone-1","justification":"x"}""");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var whileDown = await AuditHealth();
        Assert.That(whileDown.Status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(whileDown.State, Is.EqualTo("degraded"));
        Assert.That(whileDown.Pending, Is.GreaterThan(0));
        Assert.That(_audit.Events, Is.Empty);

        _audit.Unavailable = false;
        var afterRecovery = await AuditHealth();   // health proba radi flush

        Assert.That(afterRecovery, Is.EqualTo((HttpStatusCode.OK, 0, 0, "ok")));
        Assert.That(_audit.Events.Select(e => e.Action), Does.Contain(ExposureAudit.Registered));
    }

    [Test]
    public async Task Audit_health_is_503_incomplete_once_the_buffer_has_dropped_an_event()
    {
        _audit.Unavailable = true;
        for (var i = 0; i < 1001; i++)   // podrazumevani maxPending je 1000
        {
            await _buffer.RecordAsync(AuditEvent.Create(DateTimeOffset.UtcNow, "owner-1", ExposureAudit.Confirmed, $"exposure:e-{i}",
                AuditOutcome.Success, Guid.NewGuid(), AuditSensitivity.Low, new Dictionary<string, string> { ["assetId"] = "asset-1" }));
        }

        var health = await AuditHealth();

        Assert.That(health.Status, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That((health.Dropped, health.State), Is.EqualTo((1, "incomplete")));
    }

    private static AuditEvent SampleEvent(int i) => AuditEvent.Create(DateTimeOffset.UtcNow, "owner-1", ExposureAudit.Confirmed, $"exposure:e-{i}",
        AuditOutcome.Success, Guid.NewGuid(), AuditSensitivity.Low, new Dictionary<string, string> { ["assetId"] = "asset-1" });

    [Test]
    public async Task Dropping_an_event_from_a_full_buffer_is_reported_through_OnDropped()
    {
        var dropped = new List<(string Action, int Total)>();
        _buffer.OnDropped = (e, total) => dropped.Add((e.Action, total));
        _audit.Unavailable = true;

        for (var i = 0; i < 1002; i++) await _buffer.RecordAsync(SampleEvent(i));

        Assert.That(dropped, Is.EqualTo(new[] { (ExposureAudit.Confirmed, 1), (ExposureAudit.Confirmed, 2) }));
    }

    [Test]
    public async Task Background_flush_drains_the_buffer_without_any_new_audit_or_health_call()
    {
        await using var app = T08Host.Build([
            "--urls=http://127.0.0.1:0", "--environment=Development", "--T08_AUDIT_FLUSH_SECONDS=1",
            "--T01_BASE_URL=", "--T11_BASE_URL=", "--T04_BASE_URL=", "--T08_SERVICE_TOKEN="]);
        var sink = app.Services.GetRequiredService<InMemoryAuditSink>();
        var buffer = app.Services.GetRequiredService<BufferedAuditSink>();
        await app.StartAsync();

        sink.Unavailable = true;
        await buffer.RecordAsync(SampleEvent(1));
        Assert.That(buffer.Pending, Is.EqualTo(1));

        sink.Unavailable = false;
        for (var i = 0; i < 50 && buffer.Pending > 0; i++) await Task.Delay(100);

        Assert.That(buffer.Pending, Is.EqualTo(0));
        Assert.That(sink.Events, Has.Count.EqualTo(1));
        await app.StopAsync();
    }

    [Test]
    public async Task Secret_looking_request_text_never_reaches_any_audit_event_over_HTTP()
    {
        const string secret = "hunter2-SUPER-SECRET-token-AKIAIOSFODNN7EXAMPLE";

        var registered = await Post("/api/v1/exposures",
            $$"""{"assetId":"asset-1","zoneId":"zone-1","justification":"password={{secret}}"}""");
        var closed = await Post("/api/v1/exposures/e-1/closure", $$"""{"reasonCode":"{{secret}}"}""");

        Assert.That(registered.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(closed.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));   // reasonCode je kod, ne slobodan tekst

        Assert.That(_audit.Events, Is.Not.Empty);
        foreach (var auditEvent in _audit.Events)
            Assert.That(JsonSerializer.Serialize(auditEvent), Does.Not.Contain("hunter2").And.Not.Contain("AKIA"));
    }

    [Test]
    public async Task Token_whose_sub_looks_like_a_secret_never_reaches_audit_over_HTTP()
    {
        var token = _tokens.Issue("password=hunter2-SUPER-SECRET", Scope, [Permissions.ExposureRegister], TimeSpan.FromMinutes(5));
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/exposures")
        {
            Content = new StringContent("""{"assetId":"asset-1","zoneId":"zone-1","justification":"x"}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var response = await _http.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));   // ne 500: token se odbija pre audita
        Assert.That(_audit.Events, Is.Empty);
    }

    [TestCase("classification", """{"classification":"Approved"}""")]
    [TestCase("confirmation", "{}")]
    [TestCase("closure", """{"reasonCode":"removed"}""")]
    public async Task Route_id_that_looks_like_a_secret_is_404_and_never_reaches_audit_over_HTTP(string route, string body)
    {
        var token = _tokens.Issue("owner-1", Scope,
            [Permissions.ExposureClassify, Permissions.ExposureConfirm, Permissions.ExposureClose], TimeSpan.FromMinutes(5));
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/exposures/password=hunter2-SUPER-SECRET/{route}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var response = await _http.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));   // ne 500: ID van [a-z0-9-] se odbija pre audita
        Assert.That(_audit.Events, Is.Empty);
        Assert.That(_buffer.Pending, Is.EqualTo(0));
    }
}
