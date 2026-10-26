using T08.Application.Audit;
using T08.Application.Directories;
using T08.Application.Exposures;
using T08.Domain.Exposures;
using T08.Infrastructure.Audit;
using T08.Infrastructure.Persistence;
using T08.Infrastructure.Stubs;

namespace T08.Tests.Exposures;

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

[TestFixture]
public class ExposureClassifyDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static Exposure New() => Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", "scope-a", "javni API partnera");

    private static string CodeOf(TestDelegate act) => Assert.Throws<DomainRuleException>(act)!.Code;

    [Test]
    public void New_exposure_starts_as_unknown()
    {
        var e = New();

        Assert.That(e.Classification, Is.EqualTo(Classification.Unknown));
        Assert.That(e.Status, Is.EqualTo(ExposureStatus.Active));
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Active_exposure_requires_owner_and_justification(string blank)
    {
        Assert.That(CodeOf(() => Exposure.Register("e-1", "a", "z", blank, "s", "j")), Is.EqualTo("INVALID_EXPOSURE"));
        Assert.That(CodeOf(() => Exposure.Register("e-1", "a", "z", "o", "s", blank)), Is.EqualTo("INVALID_EXPOSURE"));
    }

    [Test]
    public void Approved_has_no_expiry_and_bumps_version()
    {
        var e = New();

        var changed = e.Classify(Classification.Approved, null, Now);

        Assert.That(changed, Is.True);
        Assert.That(e.Classification, Is.EqualTo(Classification.Approved));
        Assert.That(e.ExpiresAt, Is.Null);
        Assert.That(e.Version, Is.EqualTo(2));
    }

    [Test]
    public void Temporary_requires_future_expiry()
    {
        Assert.That(CodeOf(() => New().Classify(Classification.Temporary, null, Now)), Is.EqualTo("INVALID_EXPIRY"));
        Assert.That(CodeOf(() => New().Classify(Classification.Temporary, Now, Now)), Is.EqualTo("INVALID_EXPIRY"));
        Assert.That(CodeOf(() => New().Classify(Classification.Temporary, Now.AddSeconds(-1), Now)), Is.EqualTo("INVALID_EXPIRY"));

        var e = New();
        e.Classify(Classification.Temporary, Now.AddDays(7), Now);
        Assert.That(e.ExpiresAt, Is.EqualTo(Now.AddDays(7)));
    }

    [TestCase(Classification.Approved)]
    [TestCase(Classification.Unknown)]
    public void Non_temporary_classification_must_not_have_expiry(Classification target)
    {
        Assert.That(CodeOf(() => New().Classify(target, Now.AddDays(1), Now)), Is.EqualTo("INVALID_EXPIRY"));
    }

    [Test]
    public void Returning_to_unknown_is_allowed_and_clears_expiry()
    {
        var e = New();
        e.Classify(Classification.Temporary, Now.AddDays(3), Now);

        e.Classify(Classification.Unknown, null, Now);

        Assert.That(e.Classification, Is.EqualTo(Classification.Unknown));
        Assert.That(e.ExpiresAt, Is.Null);
    }

    [Test]
    public void Same_classification_is_a_no_op_that_keeps_version()
    {
        var e = New();
        e.Classify(Classification.Approved, null, Now);

        var changed = e.Classify(Classification.Approved, null, Now);

        Assert.That(changed, Is.False);
        Assert.That(e.Version, Is.EqualTo(2));
    }

    [Test]
    public void Undefined_enum_value_is_rejected()
    {
        Assert.That(CodeOf(() => New().Classify((Classification)99, null, Now)), Is.EqualTo("INVALID_CLASSIFICATION"));
    }

    [Test]
    public void Closed_exposure_cannot_be_classified()
    {
        var e = New();
        e.Close("decommissioned", Now);

        Assert.That(CodeOf(() => e.Classify(Classification.Approved, null, Now)), Is.EqualTo("EXPOSURE_CLOSED"));
        Assert.That(e.Classification, Is.EqualTo(Classification.Unknown));
    }
}

[TestFixture]
public class ClassifyExposureHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Corr = Guid.NewGuid();

    private InMemoryExposureRepository _repo = null!;
    private StubAssetDirectory _assets = null!;
    private InMemoryAuditSink _audit = null!;
    private ClassifyExposureHandler _handler = null!;

    private static ActorContext Analyst(string scope = "scope-a") =>
        new("analyst-1", scope, new HashSet<string> { Permissions.ExposureClassify });

    /// <summary>RO, AU i X: nemaju dozvolu klasifikovanja.</summary>
    private static ActorContext NoPermission(string scope = "scope-a") =>
        new("owner-1", scope, new HashSet<string> { "exposure.read" });

    [SetUp]
    public void SetUp()
    {
        _repo = new InMemoryExposureRepository();
        _repo.Add(Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", "scope-a", "javni API partnera"));
        _assets = new StubAssetDirectory()
            .Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Critical, AssetStatus.Active, "scope-a"));
        _audit = new InMemoryAuditSink();
        _handler = new ClassifyExposureHandler(_repo, _assets, _audit, new FixedTime(Now));
    }

    private static ClassifyExposureCommand Cmd(Classification c = Classification.Approved, DateTimeOffset? exp = null, string id = "e-1") =>
        new(id, c, exp, Corr);

    [Test]
    public async Task Analyst_classifies_and_a_high_sensitivity_audit_event_is_written()
    {
        var result = await _handler.HandleAsync(Analyst(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Changed, Is.True);
        Assert.That((await _repo.GetAsync("e-1"))!.Classification, Is.EqualTo(Classification.Approved));

        var evt = _audit.Events.Single(e => e.Action == "exposure.classification.changed");
        Assert.That(evt.Actor, Is.EqualTo("analyst-1"));
        Assert.That(evt.Sensitivity, Is.EqualTo(AuditSensitivity.High));
        Assert.That(evt.Details["from"], Is.EqualTo("Unknown"));
        Assert.That(evt.Details["to"], Is.EqualTo("Approved"));
    }

    [Test]
    public async Task Critical_asset_classification_also_writes_the_critical_exposure_event()
    {
        await _handler.HandleAsync(Analyst(), Cmd());

        var evt = _audit.Events.Single(e => e.Action == "exposure.critical.changed");
        Assert.That(evt.Actor, Is.EqualTo("analyst-1"));
        Assert.That(evt.Target, Is.EqualTo("exposure:e-1"));
        Assert.That(evt.Outcome, Is.EqualTo(AuditOutcome.Success));
        Assert.That(evt.Sensitivity, Is.EqualTo(AuditSensitivity.High));
        Assert.That(evt.CorrelationId, Is.EqualTo(Corr));
        Assert.That(evt.Details["change"], Is.EqualTo("classification"));
        Assert.That(evt.Details["from"], Is.EqualTo("Unknown"));
        Assert.That(evt.Details["to"], Is.EqualTo("Approved"));
    }

    [Test]
    public async Task Non_critical_asset_does_not_write_the_critical_exposure_event()
    {
        _assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Low, AssetStatus.Active, "scope-a"));

        await _handler.HandleAsync(Analyst(), Cmd());

        Assert.That(_audit.Events.Select(e => e.Action), Is.EqualTo(new[] { "exposure.classification.changed" }));
    }

    [Test]
    public async Task Repeating_the_same_classification_succeeds_without_a_second_audit_event()
    {
        await _handler.HandleAsync(Analyst(), Cmd());

        var again = await _handler.HandleAsync(Analyst(), Cmd());

        Assert.That(again.Succeeded, Is.True);
        Assert.That(again.Changed, Is.False);
        Assert.That(_audit.Events, Has.Count.EqualTo(2));   // classification.changed + critical.changed, ništa novo pri ponavljanju
    }

    // Negativni testovi iz security matrice: Deny za RO / AU / X.

    [Test]
    public async Task Actor_without_classify_permission_is_forbidden_and_the_denial_is_audited()
    {
        var result = await _handler.HandleAsync(NoPermission(), Cmd());

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("FORBIDDEN"));
        Assert.That((await _repo.GetAsync("e-1"))!.Classification, Is.EqualTo(Classification.Unknown));

        var evt = _audit.Events.Single();
        Assert.That(evt.Action, Is.EqualTo("exposure.access.denied"));
        Assert.That(evt.Outcome, Is.EqualTo(AuditOutcome.Failure));
        Assert.That(evt.Details["code"], Is.EqualTo("FORBIDDEN"));
    }

    [Test]
    public async Task Exposure_in_another_scope_looks_exactly_like_a_missing_one()
    {
        var outOfScope = await _handler.HandleAsync(Analyst("scope-b"), Cmd());
        var missing = await _handler.HandleAsync(Analyst(), Cmd(id: "e-404"));

        Assert.That(outOfScope.ErrorCode, Is.EqualTo("NOT_FOUND"));
        Assert.That(outOfScope with { }, Is.EqualTo(missing));   // isti odgovor, bez curenja postojanja
        Assert.That((await _repo.GetAsync("e-1"))!.Classification, Is.EqualTo(Classification.Unknown));
    }

    [Test]
    public async Task Scope_is_checked_before_permission_so_a_foreign_user_cannot_learn_the_exposure_exists()
    {
        var result = await _handler.HandleAsync(NoPermission("scope-b"), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
    }

    [TestCase("../e-1")]
    [TestCase("E-1")]
    [TestCase("")]
    public async Task Malformed_identifier_is_not_found_and_does_not_reach_the_repository(string id)
    {
        var result = await _handler.HandleAsync(Analyst(), Cmd(id: id));

        Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        Assert.That(_audit.Events, Is.Empty);
    }

    [Test]
    public async Task Asset_service_down_fails_closed_without_changing_anything()
    {
        _assets.Unavailable = true;

        var result = await _handler.HandleAsync(Analyst(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("UPSTREAM_UNAVAILABLE"));
        Assert.That((await _repo.GetAsync("e-1"))!.Classification, Is.EqualTo(Classification.Unknown));
        Assert.That(_audit.Events, Is.Empty);
    }

    [Test]
    public async Task Decommissioned_asset_blocks_classification()
    {
        _assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Critical, AssetStatus.Decommissioned, "scope-a"));

        var result = await _handler.HandleAsync(Analyst(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("ASSET_NOT_USABLE"));
    }

    [Test]
    public async Task Domain_rule_violation_returns_its_stable_code_and_changes_nothing()
    {
        var result = await _handler.HandleAsync(Analyst(), Cmd(Classification.Temporary, exp: null));

        Assert.That(result.ErrorCode, Is.EqualTo("INVALID_EXPIRY"));
        Assert.That((await _repo.GetAsync("e-1"))!.Classification, Is.EqualTo(Classification.Unknown));
        Assert.That(_audit.Events, Is.Empty);
    }

    [Test]
    public async Task Closed_exposure_is_rejected()
    {
        var closed = (await _repo.GetAsync("e-1"))!;
        var version = closed.Version;
        closed.Close("decommissioned", Now);
        await _repo.SaveAsync(closed, version);

        var result = await _handler.HandleAsync(Analyst(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("EXPOSURE_CLOSED"));
    }

    [Test]
    public async Task Concurrent_modification_is_a_conflict_not_a_silent_overwrite()
    {
        var racing = new RacingRepository(_repo);
        var handler = new ClassifyExposureHandler(racing, _assets, _audit, new FixedTime(Now));

        var result = await handler.HandleAsync(Analyst(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("CONFLICT"));
        Assert.That(_audit.Events, Is.Empty);   // nema audita za promenu koja nije upisana
    }

    [Test]
    public async Task Low_criticality_asset_produces_a_low_sensitivity_event()
    {
        _assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Low, AssetStatus.Active, "scope-a"));

        await _handler.HandleAsync(Analyst(), Cmd(Classification.Temporary, Now.AddDays(5)));

        Assert.That(_audit.Events.Single().Sensitivity, Is.EqualTo(AuditSensitivity.Low));
    }

    [Test]
    public async Task Classification_still_succeeds_and_event_is_kept_when_audit_service_is_down()
    {
        _audit.Unavailable = true;
        var buffered = new BufferedAuditSink(_audit);
        var handler = new ClassifyExposureHandler(_repo, _assets, buffered, new FixedTime(Now));

        var result = await handler.HandleAsync(Analyst(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(buffered.Pending, Is.EqualTo(2));   // oba događaja (promena + kritična promena) su sačuvana

        _audit.Unavailable = false;
        await buffered.FlushAsync();
        Assert.That(_audit.Events, Has.Count.EqualTo(2));
    }

    /// <summary>Simulira da je neko drugi izmenio exposure između čitanja i upisa.</summary>
    private sealed class RacingRepository(InMemoryExposureRepository inner) : IExposureRepository
    {
        public Task<Exposure?> GetAsync(string id, CancellationToken ct = default) => inner.GetAsync(id, ct);

        public Task<bool> AddAsync(Exposure exposure, CancellationToken ct = default) => inner.AddAsync(exposure, ct);

        public async Task<bool> SaveAsync(Exposure exposure, int expectedVersion, CancellationToken ct = default)
        {
            var other = (await inner.GetAsync(exposure.Id, ct))!;
            var v = other.Version;
            other.Classify(Classification.Unknown, null, Now);   // no-op neće podići verziju
            other.Close("race", Now);
            await inner.SaveAsync(other, v, ct);
            return await inner.SaveAsync(exposure, expectedVersion, ct);
        }
    }
}
