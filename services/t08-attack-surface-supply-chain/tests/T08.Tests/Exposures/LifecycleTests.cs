using T08.Application.Audit;
using T08.Application.Directories;
using T08.Application.Exposures;
using T08.Domain.Exposures;
using T08.Infrastructure.Audit;
using T08.Infrastructure.Persistence;
using T08.Infrastructure.Stubs;

namespace T08.Tests.Exposures;

[TestFixture]
public class ExposureConfirmDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static Exposure New() => Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", "scope-a", "javni API partnera");

    [Test]
    public void Confirm_records_the_time_and_bumps_the_version()
    {
        var e = New();

        e.Confirm(Now);

        Assert.That(e.LastConfirmedAt, Is.EqualTo(Now));
        Assert.That(e.Version, Is.EqualTo(2));
    }

    [Test]
    public void Every_confirmation_is_a_new_review()
    {
        var e = New();
        e.Confirm(Now);

        e.Confirm(Now.AddDays(30));

        Assert.That(e.LastConfirmedAt, Is.EqualTo(Now.AddDays(30)));
        Assert.That(e.Version, Is.EqualTo(3));
    }

    [Test]
    public void Closed_exposure_cannot_be_confirmed()
    {
        var e = New();
        e.Close("decommissioned", Now);

        var ex = Assert.Throws<DomainRuleException>(() => e.Confirm(Now))!;

        Assert.That(ex.Code, Is.EqualTo("EXPOSURE_CLOSED"));
        Assert.That(e.LastConfirmedAt, Is.Null);
    }
}

/// <summary>Zajednička postavka: kritičan asset u scope-u A, vlasnik owner-1, aktivna zona.</summary>
public abstract class LifecycleTestBase
{
    protected static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    protected static readonly Guid Corr = Guid.NewGuid();

    protected InMemoryExposureRepository Repo = null!;
    protected StubAssetDirectory Assets = null!;
    protected StubZoneDirectory Zones = null!;
    protected InMemoryAuditSink Audit = null!;

    /// <summary>RO: ima dozvole za akcije, ali samo nad svojim resursom (nema manage.any).</summary>
    protected static ActorContext Owner(string scope = "scope-a") =>
        new("owner-1", scope, new HashSet<string>
        {
            Permissions.ExposureRegister, Permissions.ExposureConfirm, Permissions.ExposureClose
        });

    /// <summary>SA: iste dozvole + manage.any, pa sme i nad tuđim resursom u svom scope-u.</summary>
    protected static ActorContext Admin(string scope = "scope-a") =>
        new("admin-1", scope, new HashSet<string>
        {
            Permissions.ExposureRegister, Permissions.ExposureConfirm, Permissions.ExposureClose, Permissions.ExposureManageAny
        });

    /// <summary>Isti vlasnik id, ali bez dozvola za ove akcije (npr. AU).</summary>
    protected static ActorContext Auditor(string scope = "scope-a") =>
        new("auditor-1", scope, new HashSet<string> { "exposure.read" });

    /// <summary>AN: sme da klasifikuje, ali ne da registruje, potvrđuje ni zatvara.</summary>
    protected static ActorContext Analyst(string scope = "scope-a") =>
        new("analyst-1", scope, new HashSet<string> { Permissions.ExposureClassify });

    /// <summary>RO drugog vlasnika: ima dozvole, ali nije vlasnik ovog resursa.</summary>
    protected static ActorContext OtherOwner() =>
        new("owner-2", "scope-a", new HashSet<string>
        {
            Permissions.ExposureRegister, Permissions.ExposureConfirm, Permissions.ExposureClose
        });

    protected void SetUpDirectories(AssetCriticality criticality = AssetCriticality.Critical)
    {
        Repo = new InMemoryExposureRepository();
        Assets = new StubAssetDirectory()
            .Add(new AssetInfo("asset-1", "owner-1", criticality, AssetStatus.Active, "scope-a"));
        Zones = new StubZoneDirectory()
            .Add(new ZoneInfo("zone-1", "owner-1", ZoneTrustLevel.Dmz, ZoneStatus.Active));
        Audit = new InMemoryAuditSink();
    }

    protected static string[] Actions(IEnumerable<AuditEvent> events) => events.Select(e => e.Action).OrderBy(a => a).ToArray();
}

[TestFixture]
public class RegisterExposureHandlerTests : LifecycleTestBase
{
    private RegisterExposureHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        SetUpDirectories();
        _handler = new RegisterExposureHandler(Repo, Assets, Zones, Audit, new FixedTime(Now), () => "e-new");
    }

    private static RegisterExposureCommand Cmd(string asset = "asset-1", string zone = "zone-1", string justification = "javni API partnera") =>
        new(asset, zone, justification, Corr);

    [Test]
    public async Task Owner_registers_and_owner_and_scope_come_from_the_asset_not_the_request()
    {
        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.ExposureId, Is.EqualTo("e-new"));
        var stored = (await Repo.GetAsync("e-new"))!;
        Assert.That(stored.Classification, Is.EqualTo(Classification.Unknown));
        Assert.That(stored.Status, Is.EqualTo(ExposureStatus.Active));
        Assert.That(stored.OwnerId, Is.EqualTo("owner-1"));
        Assert.That(stored.Scope, Is.EqualTo("scope-a"));
        Assert.That(stored.AssetId, Is.EqualTo("asset-1"));
        Assert.That(stored.ZoneId, Is.EqualTo("zone-1"));
    }

    [Test]
    public async Task Registration_writes_a_complete_registered_event()
    {
        await _handler.HandleAsync(Owner(), Cmd());

        var evt = Audit.Events.Single(e => e.Action == "exposure.registered");
        Assert.That(evt.Actor, Is.EqualTo("owner-1"));
        Assert.That(evt.Target, Is.EqualTo("exposure:e-new"));
        Assert.That(evt.Outcome, Is.EqualTo(AuditOutcome.Success));
        Assert.That(evt.Sensitivity, Is.EqualTo(AuditSensitivity.High));
        Assert.That(evt.CorrelationId, Is.EqualTo(Corr));
        Assert.That(evt.OccurredAt, Is.EqualTo(Now));
        Assert.That(evt.Details.Keys, Is.EquivalentTo(new[] { "assetId", "zoneId" }));
        Assert.That(evt.Details["assetId"], Is.EqualTo("asset-1"));
        Assert.That(evt.Details["zoneId"], Is.EqualTo("zone-1"));
    }

    [Test]
    public async Task Registration_never_puts_the_justification_into_the_audit_event()
    {
        await _handler.HandleAsync(Owner(), Cmd(justification: "tajni razlog koji ne sme u audit"));

        Assert.That(Audit.Events.SelectMany(e => e.Details.Values), Has.None.Contain("tajni razlog"));
    }

    [Test]
    public async Task Critical_asset_registration_also_writes_the_critical_exposure_event()
    {
        await _handler.HandleAsync(Owner(), Cmd());

        var evt = Audit.Events.Single(e => e.Action == "exposure.critical.changed");
        Assert.That(evt.Sensitivity, Is.EqualTo(AuditSensitivity.High));
        Assert.That(evt.Details["change"], Is.EqualTo("registered"));
        Assert.That(evt.Details["from"], Is.EqualTo("none"));
        Assert.That(evt.Details["to"], Is.EqualTo("Unknown"));
    }

    [Test]
    public async Task Non_critical_asset_registration_writes_only_the_registered_event()
    {
        Assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Low, AssetStatus.Active, "scope-a"));

        await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(Actions(Audit.Events), Is.EqualTo(new[] { "exposure.registered" }));
        Assert.That(Audit.Events.Single().Sensitivity, Is.EqualTo(AuditSensitivity.Low));
    }

    [Test]
    public async Task Admin_may_register_for_an_asset_owned_by_someone_else()
    {
        var result = await _handler.HandleAsync(Admin(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That((await Repo.GetAsync("e-new"))!.OwnerId, Is.EqualTo("owner-1"));   // vlasnik ostaje vlasnik asset-a
    }

    [Test]
    public async Task Default_id_is_generated_by_the_server_and_has_a_valid_contract_format()
    {
        var handler = new RegisterExposureHandler(Repo, Assets, Zones, Audit, new FixedTime(Now));

        var first = await handler.HandleAsync(Owner(), Cmd());
        var second = await handler.HandleAsync(Owner(), Cmd());

        Assert.That(ContractIdentifier.IsValid(first.ExposureId), Is.True);
        Assert.That(first.ExposureId, Is.Not.EqualTo(second.ExposureId));
    }

    // Negativni testovi iz security matrice (Registrovati exposure): Deny za AN, AU, X i tuđeg RO.

    [Test]
    public async Task Analyst_and_auditor_cannot_register_and_the_denial_is_audited_against_the_asset()
    {
        foreach (var actor in new[] { Analyst(), Auditor() })
        {
            var result = await _handler.HandleAsync(actor, Cmd() with { CorrelationId = Guid.NewGuid() });

            Assert.That(result.ErrorCode, Is.EqualTo("FORBIDDEN"));
        }

        Assert.That(await Repo.GetAsync("e-new"), Is.Null);
        Assert.That(Audit.Events, Has.Count.EqualTo(2));
        var evt = Audit.Events.First();
        Assert.That(evt.Action, Is.EqualTo("exposure.access.denied"));
        Assert.That(evt.Outcome, Is.EqualTo(AuditOutcome.Failure));
        Assert.That(evt.Target, Is.EqualTo("asset:asset-1"));
        Assert.That(evt.Details["attempted"], Is.EqualTo("exposure.registered"));
        Assert.That(evt.Details["code"], Is.EqualTo("FORBIDDEN"));
    }

    [Test]
    public async Task Owner_of_another_asset_cannot_register_on_this_one()
    {
        var result = await _handler.HandleAsync(OtherOwner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("FORBIDDEN"));
        Assert.That(await Repo.GetAsync("e-new"), Is.Null);
        Assert.That(Audit.Events.Single().Action, Is.EqualTo("exposure.access.denied"));
    }

    [Test]
    public async Task Asset_in_another_scope_looks_exactly_like_a_missing_asset()
    {
        var outOfScope = await _handler.HandleAsync(Admin("scope-b"), Cmd());
        var missing = await _handler.HandleAsync(Admin(), Cmd(asset: "asset-404"));

        Assert.That(outOfScope.ErrorCode, Is.EqualTo("ASSET_NOT_USABLE"));
        Assert.That(outOfScope with { }, Is.EqualTo(missing));
        Assert.That(await Repo.GetAsync("e-new"), Is.Null);
        Assert.That(Audit.Events.Single().Details["code"], Is.EqualTo("ASSET_NOT_USABLE"));   // interno se razlikuje samo u auditu
    }

    [Test]
    public async Task Decommissioned_asset_cannot_get_a_new_exposure()
    {
        Assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Critical, AssetStatus.Decommissioned, "scope-a"));

        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("ASSET_NOT_USABLE"));
        Assert.That(Audit.Events, Is.Empty);
    }

    [Test]
    public async Task Missing_or_inactive_zone_is_rejected()
    {
        Zones.Add(new ZoneInfo("zone-off", "owner-1", ZoneTrustLevel.External, ZoneStatus.Inactive));

        var missing = await _handler.HandleAsync(Owner(), Cmd(zone: "zone-404"));
        var inactive = await _handler.HandleAsync(Owner(), Cmd(zone: "zone-off"));

        Assert.That(missing.ErrorCode, Is.EqualTo("ZONE_NOT_USABLE"));
        Assert.That(inactive.ErrorCode, Is.EqualTo("ZONE_NOT_USABLE"));
        Assert.That(await Repo.GetAsync("e-new"), Is.Null);
        Assert.That(Audit.Events, Is.Empty);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task Upstream_failure_fails_closed_without_storing_or_auditing(bool assetDown, bool zoneDown)
    {
        Assets.Unavailable = assetDown;
        Zones.Unavailable = zoneDown;

        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("UPSTREAM_UNAVAILABLE"));
        Assert.That(await Repo.GetAsync("e-new"), Is.Null);
        Assert.That(Audit.Events, Is.Empty);
    }

    [TestCase("../asset-1", "zone-1")]
    [TestCase("ASSET-1", "zone-1")]
    [TestCase("", "zone-1")]
    [TestCase("asset-1", "zone 1")]
    [TestCase("asset-1", "")]
    public async Task Malformed_identifiers_never_reach_upstream_or_the_audit(string asset, string zone)
    {
        var result = await _handler.HandleAsync(Owner(), Cmd(asset, zone));

        Assert.That(result.ErrorCode, Is.EqualTo("INVALID_EXPOSURE"));
        Assert.That(Audit.Events, Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task Blank_justification_is_rejected_and_nothing_is_stored(string justification)
    {
        var result = await _handler.HandleAsync(Owner(), Cmd(justification: justification));

        Assert.That(result.ErrorCode, Is.EqualTo("INVALID_EXPOSURE"));
        Assert.That(await Repo.GetAsync("e-new"), Is.Null);
        Assert.That(Audit.Events, Is.Empty);
    }

    [Test]
    public async Task Duplicate_generated_id_is_a_conflict_not_an_overwrite()
    {
        await _handler.HandleAsync(Owner(), Cmd(justification: "prvi"));

        var again = await _handler.HandleAsync(Owner(), Cmd(justification: "drugi"));

        Assert.That(again.ErrorCode, Is.EqualTo("CONFLICT"));
        Assert.That((await Repo.GetAsync("e-new"))!.Justification, Is.EqualTo("prvi"));
        Assert.That(Audit.Events.Count(e => e.Action == "exposure.registered"), Is.EqualTo(1));
    }

    [Test]
    public async Task Registration_succeeds_and_events_are_kept_when_audit_is_down()
    {
        Audit.Unavailable = true;
        var buffered = new BufferedAuditSink(Audit);
        var handler = new RegisterExposureHandler(Repo, Assets, Zones, buffered, new FixedTime(Now), () => "e-new");

        var result = await handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.AuditDelivered, Is.True);   // sačuvano u baferu
        Assert.That(buffered.Pending, Is.EqualTo(2));

        Audit.Unavailable = false;
        await buffered.FlushAsync();
        Assert.That(Actions(Audit.Events), Is.EqualTo(new[] { "exposure.critical.changed", "exposure.registered" }));
    }

    [Test]
    public async Task Unbuffered_audit_failure_is_reported_to_the_caller()
    {
        Audit.Unavailable = true;

        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.AuditDelivered, Is.False);
    }
}

[TestFixture]
public class ConfirmExposureHandlerTests : LifecycleTestBase
{
    private ConfirmExposureHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        SetUpDirectories();
        Repo.Add(Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", "scope-a", "javni API partnera"));
        _handler = new ConfirmExposureHandler(Repo, Assets, Audit, new FixedTime(Now));
    }

    private static ConfirmExposureCommand Cmd(string id = "e-1") => new(id, Corr);

    [Test]
    public async Task Owner_confirms_and_a_complete_confirmed_event_is_written()
    {
        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That((await Repo.GetAsync("e-1"))!.LastConfirmedAt, Is.EqualTo(Now));

        var evt = Audit.Events.Single();
        Assert.That(evt.Action, Is.EqualTo("exposure.confirmed"));
        Assert.That(evt.Actor, Is.EqualTo("owner-1"));
        Assert.That(evt.Target, Is.EqualTo("exposure:e-1"));
        Assert.That(evt.Outcome, Is.EqualTo(AuditOutcome.Success));
        Assert.That(evt.Sensitivity, Is.EqualTo(AuditSensitivity.High));
        Assert.That(evt.CorrelationId, Is.EqualTo(Corr));
        Assert.That(evt.OccurredAt, Is.EqualTo(Now));
        Assert.That(evt.Details.Keys, Is.EquivalentTo(new[] { "assetId" }));
    }

    [Test]
    public async Task Confirmation_does_not_change_the_exposure_so_no_critical_event_is_written()
    {
        await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(Actions(Audit.Events), Is.EqualTo(new[] { "exposure.confirmed" }));
    }

    [Test]
    public async Task Admin_may_confirm_someone_elses_exposure()
    {
        var result = await _handler.HandleAsync(Admin(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(Audit.Events.Single().Actor, Is.EqualTo("admin-1"));
    }

    // Negativni testovi iz security matrice (Potvrditi exposure): Deny za AN, AU, X i tuđeg RO.

    [Test]
    public async Task Analyst_auditor_and_foreign_owner_are_forbidden_and_each_denial_is_audited()
    {
        foreach (var actor in new[] { Analyst(), Auditor(), OtherOwner() })
        {
            var result = await _handler.HandleAsync(actor, Cmd() with { CorrelationId = Guid.NewGuid() });

            Assert.That(result.ErrorCode, Is.EqualTo("FORBIDDEN"));
        }

        Assert.That((await Repo.GetAsync("e-1"))!.LastConfirmedAt, Is.Null);
        Assert.That(Audit.Events, Has.Count.EqualTo(3));
        Assert.That(Audit.Events.Select(e => e.Action).Distinct(), Is.EqualTo(new[] { "exposure.access.denied" }));
        Assert.That(Audit.Events.Select(e => e.Details["attempted"]).Distinct(), Is.EqualTo(new[] { "exposure.confirm" }));
    }

    [Test]
    public async Task Exposure_in_another_scope_looks_exactly_like_a_missing_one()
    {
        var outOfScope = await _handler.HandleAsync(Admin("scope-b"), Cmd());
        var missing = await _handler.HandleAsync(Admin(), Cmd("e-404"));

        Assert.That(outOfScope.ErrorCode, Is.EqualTo("NOT_FOUND"));
        Assert.That(outOfScope with { }, Is.EqualTo(missing));
        Assert.That((await Repo.GetAsync("e-1"))!.LastConfirmedAt, Is.Null);
    }

    [TestCase("../e-1")]
    [TestCase("E-1")]
    [TestCase("")]
    public async Task Malformed_identifier_is_not_found_and_not_audited(string id)
    {
        var result = await _handler.HandleAsync(Admin(), Cmd(id));

        Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        Assert.That(Audit.Events, Is.Empty);
    }

    [Test]
    public async Task Closed_exposure_cannot_be_confirmed()
    {
        var e = (await Repo.GetAsync("e-1"))!;
        var v = e.Version;
        e.Close("decommissioned", Now);
        await Repo.SaveAsync(e, v);

        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("EXPOSURE_CLOSED"));
        Assert.That(Audit.Events, Is.Empty);
    }

    [Test]
    public async Task Asset_service_down_fails_closed_without_changing_anything()
    {
        Assets.Unavailable = true;

        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("UPSTREAM_UNAVAILABLE"));
        Assert.That((await Repo.GetAsync("e-1"))!.LastConfirmedAt, Is.Null);
        Assert.That(Audit.Events, Is.Empty);
    }

    [Test]
    public async Task Decommissioned_asset_blocks_confirmation()
    {
        Assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Critical, AssetStatus.Decommissioned, "scope-a"));

        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("ASSET_NOT_USABLE"));
        Assert.That((await Repo.GetAsync("e-1"))!.LastConfirmedAt, Is.Null);
    }

    [Test]
    public async Task Concurrent_modification_is_a_conflict_and_writes_no_audit()
    {
        var handler = new ConfirmExposureHandler(new RacingLifecycleRepository(Repo, Now), Assets, Audit, new FixedTime(Now));

        var result = await handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("CONFLICT"));
        Assert.That(Audit.Events, Is.Empty);
    }
}

[TestFixture]
public class CloseExposureHandlerTests : LifecycleTestBase
{
    private CloseExposureHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        SetUpDirectories();
        Repo.Add(Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", "scope-a", "javni API partnera"));
        _handler = new CloseExposureHandler(Repo, Assets, Audit, new FixedTime(Now));
    }

    private static CloseExposureCommand Cmd(string id = "e-1", string reason = "decommissioned") => new(id, reason, Corr);

    [Test]
    public async Task Owner_closes_and_a_complete_closed_event_is_written()
    {
        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        var stored = (await Repo.GetAsync("e-1"))!;
        Assert.That(stored.Status, Is.EqualTo(ExposureStatus.Closed));
        Assert.That(stored.CloseReason, Is.EqualTo("decommissioned"));
        Assert.That(stored.ClosedAt, Is.EqualTo(Now));

        var evt = Audit.Events.Single(e => e.Action == "exposure.closed");
        Assert.That(evt.Actor, Is.EqualTo("owner-1"));
        Assert.That(evt.Target, Is.EqualTo("exposure:e-1"));
        Assert.That(evt.Outcome, Is.EqualTo(AuditOutcome.Success));
        Assert.That(evt.Sensitivity, Is.EqualTo(AuditSensitivity.High));
        Assert.That(evt.CorrelationId, Is.EqualTo(Corr));
        Assert.That(evt.Details["assetId"], Is.EqualTo("asset-1"));
        Assert.That(evt.Details["reason"], Is.EqualTo("decommissioned"));
    }

    [Test]
    public async Task Closing_a_critical_asset_exposure_also_writes_the_critical_exposure_event()
    {
        await _handler.HandleAsync(Owner(), Cmd());

        var evt = Audit.Events.Single(e => e.Action == "exposure.critical.changed");
        Assert.That(evt.Sensitivity, Is.EqualTo(AuditSensitivity.High));
        Assert.That(evt.Details["change"], Is.EqualTo("status"));
        Assert.That(evt.Details["from"], Is.EqualTo("Active"));
        Assert.That(evt.Details["to"], Is.EqualTo("Closed"));
    }

    [Test]
    public async Task Closing_a_non_critical_asset_exposure_writes_only_the_closed_event()
    {
        Assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Low, AssetStatus.Active, "scope-a"));

        await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(Actions(Audit.Events), Is.EqualTo(new[] { "exposure.closed" }));
        Assert.That(Audit.Events.Single().Sensitivity, Is.EqualTo(AuditSensitivity.Low));
    }

    [Test]
    public async Task Decommissioned_asset_does_not_block_closing_and_is_audited_conservatively_as_critical()
    {
        Assets.Add(new AssetInfo("asset-1", "owner-1", AssetCriticality.Low, AssetStatus.Decommissioned, "scope-a"));

        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That((await Repo.GetAsync("e-1"))!.Status, Is.EqualTo(ExposureStatus.Closed));
        Assert.That(Actions(Audit.Events), Is.EqualTo(new[] { "exposure.closed", "exposure.critical.changed" }));
        Assert.That(Audit.Events.All(e => e.Sensitivity == AuditSensitivity.High), Is.True);
    }

    [Test]
    public async Task Admin_may_close_someone_elses_exposure()
    {
        var result = await _handler.HandleAsync(Admin(), Cmd());

        Assert.That(result.Succeeded, Is.True);
    }

    // Negativni testovi iz security matrice (Zatvoriti exposure): Deny za AN, AU, X i tuđeg RO.

    [Test]
    public async Task Analyst_auditor_and_foreign_owner_are_forbidden_and_each_denial_is_audited()
    {
        foreach (var actor in new[] { Analyst(), Auditor(), OtherOwner() })
        {
            var result = await _handler.HandleAsync(actor, Cmd() with { CorrelationId = Guid.NewGuid() });

            Assert.That(result.ErrorCode, Is.EqualTo("FORBIDDEN"));
        }

        Assert.That((await Repo.GetAsync("e-1"))!.Status, Is.EqualTo(ExposureStatus.Active));
        Assert.That(Audit.Events, Has.Count.EqualTo(3));
        Assert.That(Audit.Events.Select(e => e.Details["attempted"]).Distinct(), Is.EqualTo(new[] { "exposure.close" }));
    }

    [Test]
    public async Task Exposure_in_another_scope_looks_exactly_like_a_missing_one()
    {
        var outOfScope = await _handler.HandleAsync(Admin("scope-b"), Cmd());
        var missing = await _handler.HandleAsync(Admin(), Cmd("e-404"));

        Assert.That(outOfScope.ErrorCode, Is.EqualTo("NOT_FOUND"));
        Assert.That(outOfScope with { }, Is.EqualTo(missing));
        Assert.That((await Repo.GetAsync("e-1"))!.Status, Is.EqualTo(ExposureStatus.Active));
    }

    [TestCase("Free text reason!")]
    [TestCase("razlog sa razmakom")]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Reason_must_be_a_stable_code_not_free_text(string reason)
    {
        var result = await _handler.HandleAsync(Owner(), Cmd(reason: reason));

        Assert.That(result.ErrorCode, Is.EqualTo("INVALID_EXPOSURE"));
        Assert.That((await Repo.GetAsync("e-1"))!.Status, Is.EqualTo(ExposureStatus.Active));
        Assert.That(Audit.Events, Is.Empty);
    }

    [Test]
    public async Task Closing_twice_is_rejected_and_writes_no_second_audit()
    {
        await _handler.HandleAsync(Owner(), Cmd());
        var before = Audit.Events.Count;

        var again = await _handler.HandleAsync(Owner(), Cmd(reason: "other-reason"));

        Assert.That(again.ErrorCode, Is.EqualTo("EXPOSURE_CLOSED"));
        Assert.That(Audit.Events, Has.Count.EqualTo(before));
        Assert.That((await Repo.GetAsync("e-1"))!.CloseReason, Is.EqualTo("decommissioned"));
    }

    [Test]
    public async Task Asset_service_down_fails_closed_and_the_exposure_stays_active()
    {
        Assets.Unavailable = true;

        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("UPSTREAM_UNAVAILABLE"));
        Assert.That((await Repo.GetAsync("e-1"))!.Status, Is.EqualTo(ExposureStatus.Active));
        Assert.That(Audit.Events, Is.Empty);
    }

    [Test]
    public async Task Concurrent_modification_is_a_conflict_and_writes_no_audit()
    {
        var handler = new CloseExposureHandler(new RacingLifecycleRepository(Repo, Now), Assets, Audit, new FixedTime(Now));

        var result = await handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("CONFLICT"));
        Assert.That(Audit.Events, Is.Empty);
    }

    [Test]
    public async Task Closing_succeeds_and_events_are_kept_when_audit_is_down()
    {
        Audit.Unavailable = true;
        var buffered = new BufferedAuditSink(Audit);
        var handler = new CloseExposureHandler(Repo, Assets, buffered, new FixedTime(Now));

        var result = await handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(buffered.Pending, Is.EqualTo(2));
    }
}

/// <summary>Simulira da je neko drugi izmenio exposure između čitanja i upisa (promena verzije).</summary>
internal sealed class RacingLifecycleRepository(InMemoryExposureRepository inner, DateTimeOffset now) : IExposureRepository
{
    public Task<Exposure?> GetAsync(string id, CancellationToken ct = default) => inner.GetAsync(id, ct);

    public Task<bool> AddAsync(Exposure exposure, CancellationToken ct = default) => inner.AddAsync(exposure, ct);

    public async Task<bool> SaveAsync(Exposure exposure, int expectedVersion, CancellationToken ct = default)
    {
        var other = (await inner.GetAsync(exposure.Id, ct))!;
        var v = other.Version;
        other.Confirm(now);   // podiže verziju
        await inner.SaveAsync(other, v, ct);
        return await inner.SaveAsync(exposure, expectedVersion, ct);
    }
}
