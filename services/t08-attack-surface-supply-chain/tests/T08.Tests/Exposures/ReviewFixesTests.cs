using System.Net;
using T08.Application.Audit;
using T08.Application.Directories;
using T08.Application.Exposures;
using T08.Domain.Exposures;
using T08.Infrastructure.Audit;
using T08.Infrastructure.Directories;
using T08.Infrastructure.Http;
using T08.Infrastructure.Persistence;
using T08.Infrastructure.Stubs;
using T08.Tests.Infrastructure;

namespace T08.Tests.Exposures;

/// <summary>Sink sa unapred zadatim ishodom ili izuzetkom, da se vidi kako handler reaguje na neuspeh audita.</summary>
internal sealed class ScriptedAuditSink(Func<AuditEvent, AuditWriteOutcome> respond) : IAuditSink
{
    public List<AuditEvent> Seen { get; } = [];

    public Task<AuditWriteOutcome> RecordAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        Seen.Add(auditEvent);
        return Task.FromResult(respond(auditEvent));
    }
}

[TestFixture]
public class ClassifyExposureReviewFixesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Corr = Guid.NewGuid();

    private InMemoryExposureRepository _repo = null!;
    private StubAssetDirectory _assets = null!;
    private InMemoryAuditSink _audit = null!;

    private static ActorContext Analyst(string scope = "scope-a") =>
        new("analyst-1", scope, new HashSet<string> { Permissions.ExposureClassify });

    private static ClassifyExposureCommand Cmd(string id = "e-1", Classification c = Classification.Approved, DateTimeOffset? exp = null) =>
        new(id, c, exp, Corr);

    private ClassifyExposureHandler Handler(IAuditSink? sink = null) =>
        new(_repo, _assets, sink ?? _audit, new FixedTime(Now));

    [SetUp]
    public void SetUp()
    {
        _repo = new InMemoryExposureRepository();
        _repo.Add(Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", "scope-a", "javni API partnera"));
        _assets = new StubAssetDirectory()
            .Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Critical, AssetStatus.Active, "scope-a"));
        _audit = new InMemoryAuditSink();
    }

    [Test]
    public async Task Closed_exposure_is_rejected_before_the_asset_is_read_even_if_the_asset_service_is_down()
    {
        var closed = (await _repo.GetAsync("e-1"))!;
        var version = closed.Version;
        closed.Close("decommissioned", Now);
        await _repo.SaveAsync(closed, version);
        _assets.Unavailable = true;

        var result = await Handler().HandleAsync(Analyst(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("EXPOSURE_CLOSED"));
    }

    [Test]
    public async Task Missing_exposure_is_not_audited_so_random_ids_cannot_flood_the_audit_log()
    {
        for (var i = 0; i < 20; i++) await Handler().HandleAsync(Analyst(), Cmd($"e-missing-{i}"));

        Assert.That(_audit.Events, Is.Empty);
    }

    [Test]
    public async Task Exposure_in_another_scope_is_audited_as_denied_but_answers_like_a_missing_one()
    {
        var result = await Handler().HandleAsync(Analyst("scope-b"), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        var evt = _audit.Events.Single();
        Assert.That(evt.Action, Is.EqualTo("exposure.access.denied"));
        Assert.That(evt.Actor, Is.EqualTo("analyst-1"));
    }

    [Test]
    public async Task Asset_that_moved_to_another_scope_is_not_classified()
    {
        _assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Critical, AssetStatus.Active, "scope-b"));

        var result = await Handler().HandleAsync(Analyst(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("ASSET_SCOPE_MISMATCH"));
        Assert.That((await _repo.GetAsync("e-1"))!.Classification, Is.EqualTo(Classification.Unknown));
        Assert.That(_audit.Events, Is.Empty);
    }

    [Test]
    public async Task Unknown_asset_is_not_usable()
    {
        var result = await new ClassifyExposureHandler(_repo, new StubAssetDirectory(), _audit, new FixedTime(Now))
            .HandleAsync(Analyst(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("ASSET_NOT_USABLE"));
        Assert.That((await _repo.GetAsync("e-1"))!.Classification, Is.EqualTo(Classification.Unknown));
    }

    [Test]
    public async Task Inactive_asset_is_not_usable()
    {
        _assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Critical, AssetStatus.Inactive, "scope-a"));

        var result = await Handler().HandleAsync(Analyst(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("ASSET_NOT_USABLE"));
    }

    [Test]
    public async Task Guid_identifiers_from_T01_are_accepted_even_when_they_start_with_a_digit()
    {
        const string exposureId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
        const string assetId = "9b2c1d7e-0000-4000-8000-000000000001";
        _repo.Add(Exposure.Register(exposureId, assetId, "zone-1", "owner-1", "scope-a", "javni API"));
        _assets.Add(new AssetInfo(assetId, "owner-1", AssetCriticality.High, AssetStatus.Active, "scope-a"));

        var result = await Handler().HandleAsync(Analyst(), Cmd(exposureId));

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task Rejected_audit_keeps_the_change_but_is_reported_to_the_caller()
    {
        var sink = new ScriptedAuditSink(_ => AuditWriteOutcome.Rejected);

        var result = await Handler(sink).HandleAsync(Analyst(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.AuditDelivered, Is.False);
        Assert.That((await _repo.GetAsync("e-1"))!.Classification, Is.EqualTo(Classification.Approved));
    }

    [Test]
    public async Task Throwing_audit_sink_does_not_turn_a_saved_change_into_an_error_and_is_reported()
    {
        var sink = new ScriptedAuditSink(_ => throw new InvalidOperationException("sink pao"));

        var result = await Handler(sink).HandleAsync(Analyst(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.AuditDelivered, Is.False);
    }

    [Test]
    public async Task Buffered_event_counts_as_kept_but_unbuffered_unavailable_sink_does_not()
    {
        _audit.Unavailable = true;
        var buffered = await Handler(new BufferedAuditSink(_audit)).HandleAsync(Analyst(), Cmd());
        Assert.That(buffered.AuditDelivered, Is.True);

        _repo.Add(Exposure.Register("e-2", "asset-1", "zone-1", "owner-1", "scope-a", "javni API"));
        var direct = await Handler(_audit).HandleAsync(Analyst(), Cmd("e-2"));
        Assert.That(direct.Succeeded, Is.True);
        Assert.That(direct.AuditDelivered, Is.False);
    }

    [Test]
    public async Task Extending_a_temporary_exposure_is_audited_with_the_new_expiry()
    {
        await Handler().HandleAsync(Analyst(), Cmd(c: Classification.Temporary, exp: Now.AddDays(5)));

        // Poseban correlation id: isti id + isto vreme bi InMemoryAuditSink prepoznao kao ponovljen događaj.
        var extended = await Handler().HandleAsync(Analyst(),
            new ClassifyExposureCommand("e-1", Classification.Temporary, Now.AddDays(30), Guid.NewGuid()));

        Assert.That(extended.Changed, Is.True);
        // Oba događaja imaju isto vreme, pa se traži po sadržaju, ne po redosledu.
        var evt = _audit.Events.Single(e => e.Action == "exposure.classification.changed" && e.Details["from"] == "Temporary");
        Assert.That(evt.Details["to"], Is.EqualTo("Temporary"));
        Assert.That(evt.Details["expiresAt"], Is.EqualTo(Now.AddDays(30).ToString("O")));
    }
}

[TestFixture]
public class BufferedAuditSinkReviewFixesTests
{
    private static AuditEvent Evt(string id, int sec = 0) =>
        AuditEvent.Create(new DateTimeOffset(2026, 10, 26, 12, 0, sec, TimeSpan.Zero), "a", "exposure.classification.changed",
            $"exposure:{id}", AuditOutcome.Success, Guid.NewGuid(), AuditSensitivity.High);

    [Test]
    public async Task Queue_is_bounded_and_overflow_is_counted()
    {
        var buffered = new BufferedAuditSink(new InMemoryAuditSink { Unavailable = true }, maxPending: 3);

        for (var i = 0; i < 10; i++) await buffered.RecordAsync(Evt($"e-{i}", i));

        Assert.That(buffered.Pending, Is.EqualTo(3));
        Assert.That(buffered.Dropped, Is.EqualTo(7));
    }

    [Test]
    public async Task Rejected_event_is_not_buffered()
    {
        var buffered = new BufferedAuditSink(new ScriptedAuditSink(_ => AuditWriteOutcome.Rejected));

        var outcome = await buffered.RecordAsync(Evt("e-1"));

        Assert.That(outcome, Is.EqualTo(AuditWriteOutcome.Rejected));
        Assert.That(buffered.Pending, Is.Zero);
    }

    [Test]
    public async Task New_event_never_overtakes_an_older_one_that_is_still_pending()
    {
        // T04 "guta" samo e-1; ostalo bi prihvatio. Novi događaj ipak ne sme da ga pretekne.
        var upstream = new ScriptedAuditSink(e => e.Target == "exposure:e-1" ? AuditWriteOutcome.Unavailable : AuditWriteOutcome.Accepted);
        var buffered = new BufferedAuditSink(upstream);

        await buffered.RecordAsync(Evt("e-1", 1));
        var outcome = await buffered.RecordAsync(Evt("e-2", 2));

        Assert.That(outcome, Is.EqualTo(AuditWriteOutcome.Buffered));
        Assert.That(buffered.Pending, Is.EqualTo(2));
        Assert.That(upstream.Seen.Any(e => e.Target == "exposure:e-2"), Is.False);
    }
}

[TestFixture]
public class DirectoryAdapterReviewFixesTests
{
    private static readonly Guid Corr = Guid.NewGuid();

    [Test]
    public async Task Guid_asset_id_is_sent_upstream()
    {
        const string id = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
        var upstream = FakeUpstream.Json(HttpStatusCode.OK,
            $$"""{"id":"{{id}}","ownerId":"u","criticality":"High","status":"Active","scope":"s"}""");

        var result = await new HttpAssetDirectory(upstream.Client()).GetAsync(id, Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Found));
        Assert.That(upstream.LastRequest!.RequestUri!.AbsolutePath, Is.EqualTo($"/api/v1/assets/{id}"));
    }

    [Test]
    public async Task Inactive_asset_in_200_body_is_inactive()
    {
        var upstream = FakeUpstream.Json(HttpStatusCode.OK,
            """{"id":"asset-1","ownerId":"u","criticality":"High","status":"Inactive","scope":"s"}""");

        var result = await new HttpAssetDirectory(upstream.Client()).GetAsync("asset-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Inactive));
    }

    [Test]
    public async Task Bearer_token_is_sent_when_configured_and_omitted_when_not()
    {
        var upstream = FakeUpstream.Json(HttpStatusCode.NotFound, "{}");
        using var withToken = new HttpClient(new BearerTokenHandler(() => "dev-token") { InnerHandler = upstream })
        { BaseAddress = new Uri("http://upstream.test/") };
        await new HttpAssetDirectory(withToken).GetAsync("asset-1", Corr);
        Assert.That(upstream.LastRequest!.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
        Assert.That(upstream.LastRequest.Headers.Authorization.Parameter, Is.EqualTo("dev-token"));

        using var withoutToken = new HttpClient(new BearerTokenHandler(() => null) { InnerHandler = upstream })
        { BaseAddress = new Uri("http://upstream.test/") };
        await new HttpAssetDirectory(withoutToken).GetAsync("asset-1", Corr);
        Assert.That(upstream.LastRequest!.Headers.Authorization, Is.Null);
    }
}
