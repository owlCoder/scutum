using System.Net;
using System.Text.Json;
using T08.Application.Audit;
using T08.Application.Directories;
using T08.Infrastructure.Audit;

namespace T08.Tests.Audit;

[TestFixture]
public class AuditEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Corr = Guid.NewGuid();

    private static AuditEvent Make(string actor = "user-1", IReadOnlyDictionary<string, string>? details = null) =>
        AuditEvent.Create(Now, actor, "exposure.registered", "exposure:e-1", AuditOutcome.Success, Corr, AuditSensitivity.Low, details);

    [TestCase("")]
    [TestCase("  ")]
    public void Empty_actor_is_rejected(string actor) => Assert.Throws<ArgumentException>(() => Make(actor));

    [Test]
    public void Empty_correlation_id_is_rejected() =>
        Assert.Throws<ArgumentException>(() =>
            AuditEvent.Create(Now, "u", "a", "t", AuditOutcome.Success, Guid.Empty, AuditSensitivity.Low));

    [TestCase("password")]
    [TestCase("accessToken")]
    [TestCase("Authorization")]
    [TestCase("client_secret")]
    public void Secret_like_detail_keys_are_rejected(string key) =>
        Assert.Throws<ArgumentException>(() => Make(details: new Dictionary<string, string> { [key] = "x" }));

    [TestCase("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ1c2VyLTEifQ.c2lnbmF0dXJl", Description = "JWT")]
    [TestCase("Bearer abcdef0123456789", Description = "Bearer zaglavlje")]
    [TestCase("-----BEGIN PRIVATE KEY-----", Description = "privatni ključ")]
    [TestCase("password=hunter2", Description = "password=")]
    [TestCase("failed with api_key: sk-123", Description = "api_key u slobodnom tekstu")]
    public void Secret_like_detail_values_are_rejected_even_under_a_harmless_key(string value) =>
        Assert.Throws<ArgumentException>(() => Make(details: new Dictionary<string, string> { ["note"] = value }));

    private const string Jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ1c2VyLTEifQ.c2lnbmF0dXJl";

    [TestCase("actor", Jwt)]
    [TestCase("actor", "Bearer abcdef0123456789")]
    [TestCase("action", "password=hunter2")]
    [TestCase("action", "-----BEGIN PRIVATE KEY-----")]
    [TestCase("target", "exposure:api_key: sk-123")]
    [TestCase("target", Jwt)]
    public void Secret_like_actor_action_and_target_are_rejected(string field, string value)
    {
        var actor = field == "actor" ? value : "user-1";
        var action = field == "action" ? value : "exposure.registered";
        var target = field == "target" ? value : "exposure:e-1";

        var ex = Assert.Throws<ArgumentException>(() =>
            AuditEvent.Create(Now, actor, action, target, AuditOutcome.Success, Corr, AuditSensitivity.Low));

        Assert.That(ex!.ParamName, Is.EqualTo(field));
    }

    [TestCase("asset-1")]
    [TestCase("Unknown")]
    [TestCase("decommissioned")]
    [TestCase("3f2504e04f8911d39a0c0305e82c3301")]
    [TestCase("Token bucket exhausted")]   // reč bez „=“ ili „:“ nije tajna
    public void Identifiers_and_codes_are_accepted_as_detail_values(string value) =>
        Assert.DoesNotThrow(() => Make(details: new Dictionary<string, string> { ["note"] = value }));

    [Test]
    public void Details_over_2_KB_are_rejected() =>
        Assert.Throws<ArgumentException>(() =>
            Make(details: new Dictionary<string, string> { ["note"] = new string('a', AuditEvent.MaxDetailsBytes) }));

    [Test]
    public void Details_are_copied_so_later_mutation_does_not_change_the_event()
    {
        var source = new Dictionary<string, string> { ["assetId"] = "asset-1" };
        var evt = Make(details: source);

        source["assetId"] = "tampered";

        Assert.That(evt.Details["assetId"], Is.EqualTo("asset-1"));
    }

    [Test]
    public void Same_event_has_same_idempotency_key()
    {
        var first = Make();
        var second = Make();

        Assert.That(second.IdempotencyKey, Is.EqualTo(first.IdempotencyKey));
    }
}

[TestFixture]
public class ExposureAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Corr = Guid.NewGuid();

    [TestCase(AssetCriticality.Critical, AuditSensitivity.High, true)]
    [TestCase(AssetCriticality.High, AuditSensitivity.High, true)]
    [TestCase(AssetCriticality.Medium, AuditSensitivity.Medium, false)]
    [TestCase(AssetCriticality.Low, AuditSensitivity.Low, false)]
    public void Sensitivity_and_mandatory_follow_asset_criticality(AssetCriticality c, AuditSensitivity s, bool mandatory)
    {
        Assert.That(ExposureAudit.SensitivityFor(c), Is.EqualTo(s));
        Assert.That(ExposureAudit.IsMandatory(c), Is.EqualTo(mandatory));
    }

    [Test]
    public void Classification_change_carries_identifiers_and_codes_only()
    {
        var evt = ExposureAudit.ClassificationWasChanged(Now, "analyst-1", Corr, "e-1", "asset-1", "Unknown", "Approved", AssetCriticality.Critical);

        Assert.That(evt.Action, Is.EqualTo("exposure.classification.changed"));
        Assert.That(evt.Target, Is.EqualTo("exposure:e-1"));
        Assert.That(evt.Outcome, Is.EqualTo(AuditOutcome.Success));
        Assert.That(evt.Sensitivity, Is.EqualTo(AuditSensitivity.High));
        Assert.That(evt.Details, Is.EquivalentTo(new Dictionary<string, string>
            { ["assetId"] = "asset-1", ["from"] = "Unknown", ["to"] = "Approved" }));
    }

    [Test]
    public void Denied_access_is_a_failure_event_with_stable_code()
    {
        var evt = ExposureAudit.Denied(Now, "user-9", Corr, "exposure.classify", "e-1", "FORBIDDEN");

        Assert.That(evt.Outcome, Is.EqualTo(AuditOutcome.Failure));
        Assert.That(evt.Action, Is.EqualTo("exposure.access.denied"));
        Assert.That(evt.Details["code"], Is.EqualTo("FORBIDDEN"));
        Assert.That(evt.Details["attempted"], Is.EqualTo("exposure.classify"));
    }

    [Test]
    public void Registered_and_closed_events_are_built_for_the_same_target()
    {
        var registered = ExposureAudit.ExposureRegistered(Now, "ro-1", Corr, "e-1", "asset-1", "zone-1", AssetCriticality.Low);
        var closed = ExposureAudit.ExposureClosed(Now, "ro-1", Corr, "e-1", "asset-1", "decommissioned", AssetCriticality.Low);

        Assert.That(registered.Target, Is.EqualTo(closed.Target));
        Assert.That(registered.Action, Is.EqualTo("exposure.registered"));
        Assert.That(closed.Action, Is.EqualTo("exposure.closed"));
    }
}

[TestFixture]
public class HttpAuditSinkTests
{
    private static readonly AuditEvent Evt = ExposureAudit.ExposureRegistered(
        new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero), "ro-1", Guid.NewGuid(), "e-1", "asset-1", "zone-1", AssetCriticality.High);

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://t04.test/") };

    [Test]
    public async Task Event_is_posted_with_contract_fields_and_correlation_header()
    {
        string? body = null;
        HttpRequestMessage? sent = null;
        var handler = new RecordingHandler(async r =>
        {
            sent = r;
            body = await r.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });

        var outcome = await new HttpAuditSink(Client(handler)).RecordAsync(Evt);

        Assert.That(outcome, Is.EqualTo(AuditWriteOutcome.Accepted));
        Assert.That(sent!.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(sent.RequestUri!.AbsolutePath, Is.EqualTo("/api/v1/events"));
        Assert.That(sent.Headers.GetValues("X-Correlation-Id").Single(), Is.EqualTo(Evt.CorrelationId.ToString()));

        using var doc = JsonDocument.Parse(body!);
        var root = doc.RootElement;
        Assert.That(root.GetProperty("action").GetString(), Is.EqualTo("exposure.registered"));
        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("Success"));
        Assert.That(root.GetProperty("sensitivity").GetString(), Is.EqualTo("High"));
        Assert.That(root.GetProperty("target").GetString(), Is.EqualTo("exposure:e-1"));
        Assert.That(root.GetProperty("details").GetProperty("assetId").GetString(), Is.EqualTo("asset-1"));
        Assert.That(root.GetProperty("actor").GetString(), Is.EqualTo("ro-1"));
        Assert.That(root.GetProperty("correlationId").GetGuid(), Is.EqualTo(Evt.CorrelationId));
        Assert.That(root.GetProperty("occurredAt").GetDateTimeOffset(), Is.EqualTo(Evt.OccurredAt));
    }

    [Test]
    public async Task Real_HttpClient_timeout_is_unavailable_not_an_exception()
    {
        // Pravi HttpClient.Timeout (u hostu je 5 s; ovde 200 ms), a upstream nikad ne odgovara.
        using var http = new HttpClient(new CancellableHang()) { BaseAddress = new Uri("http://t04.test/"), Timeout = TimeSpan.FromMilliseconds(200) };

        var outcome = await new HttpAuditSink(http).RecordAsync(Evt);

        Assert.That(outcome, Is.EqualTo(AuditWriteOutcome.Unavailable));
    }

    [Test]
    public void Caller_cancellation_is_not_reported_as_unavailable()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var http = new HttpClient(new CancellableHang()) { BaseAddress = new Uri("http://t04.test/") };

        Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), () => new HttpAuditSink(http).RecordAsync(Evt, cts.Token));
    }

    /// <summary>Upstream koji nikad ne odgovara, ali poštuje otkazivanje (kao pravi socket).</summary>
    private sealed class CancellableHang : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return new HttpResponseMessage();
        }
    }

    [TestCase(HttpStatusCode.Accepted, AuditWriteOutcome.Accepted)]
    [TestCase(HttpStatusCode.BadRequest, AuditWriteOutcome.Rejected)]
    [TestCase(HttpStatusCode.ServiceUnavailable, AuditWriteOutcome.Unavailable)]
    [TestCase(HttpStatusCode.InternalServerError, AuditWriteOutcome.Unavailable)]
    [TestCase(HttpStatusCode.Unauthorized, AuditWriteOutcome.Unavailable)]
    [TestCase(HttpStatusCode.OK, AuditWriteOutcome.Unavailable)]   // ugovor kaže 202; drugi uspeh se ne računa kao upis
    public async Task Status_is_mapped_to_write_outcome(HttpStatusCode status, AuditWriteOutcome expected)
    {
        var handler = new RecordingHandler(_ => Task.FromResult(new HttpResponseMessage(status)));

        Assert.That(await new HttpAuditSink(Client(handler)).RecordAsync(Evt), Is.EqualTo(expected));
    }

    [Test]
    public async Task Network_failure_and_timeout_are_unavailable()
    {
        var down = new RecordingHandler(_ => throw new HttpRequestException("down"));
        var slow = new RecordingHandler(_ => throw new TaskCanceledException("timeout"));

        Assert.That(await new HttpAuditSink(Client(down)).RecordAsync(Evt), Is.EqualTo(AuditWriteOutcome.Unavailable));
        Assert.That(await new HttpAuditSink(Client(slow)).RecordAsync(Evt), Is.EqualTo(AuditWriteOutcome.Unavailable));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(request);
    }
}

[TestFixture]
public class InMemoryAndBufferedAuditSinkTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static AuditEvent Evt(string id, int second = 0) =>
        ExposureAudit.ExposureRegistered(T0.AddSeconds(second), "ro-1", Guid.NewGuid(), id, "asset-1", "zone-1", AssetCriticality.High);

    [Test]
    public async Task Repeated_event_is_not_duplicated()
    {
        var sink = new InMemoryAuditSink();
        var evt = Evt("e-1");

        await sink.RecordAsync(evt);
        await sink.RecordAsync(evt);

        Assert.That(sink.Events, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Unavailable_sink_reports_unavailable_and_stores_nothing()
    {
        var sink = new InMemoryAuditSink { Unavailable = true };

        Assert.That(await sink.RecordAsync(Evt("e-1")), Is.EqualTo(AuditWriteOutcome.Unavailable));
        Assert.That(sink.Events, Is.Empty);
    }

    [Test]
    public async Task Buffered_sink_keeps_event_while_upstream_is_down_and_delivers_in_order_after_recovery()
    {
        var upstream = new InMemoryAuditSink { Unavailable = true };
        var buffered = new BufferedAuditSink(upstream);

        Assert.That(await buffered.RecordAsync(Evt("e-1", 1)), Is.EqualTo(AuditWriteOutcome.Buffered));
        Assert.That(await buffered.RecordAsync(Evt("e-2", 2)), Is.EqualTo(AuditWriteOutcome.Buffered));
        Assert.That(buffered.Pending, Is.EqualTo(2));
        Assert.That(upstream.Events, Is.Empty);

        upstream.Unavailable = false;
        Assert.That(await buffered.RecordAsync(Evt("e-3", 3)), Is.EqualTo(AuditWriteOutcome.Accepted));

        Assert.That(buffered.Pending, Is.Zero);
        Assert.That(upstream.Events.Select(e => e.Target), Is.EqualTo(new[] { "exposure:e-1", "exposure:e-2", "exposure:e-3" }));
    }

    [Test]
    public async Task Buffered_sink_does_not_buffer_accepted_events()
    {
        var buffered = new BufferedAuditSink(new InMemoryAuditSink());

        await buffered.RecordAsync(Evt("e-1"));

        Assert.That(buffered.Pending, Is.Zero);
    }
}
