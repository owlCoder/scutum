using T08.Application.Directories;
using T08.Application.Exposures;
using T08.Domain.Exposures;

namespace T08.Tests.Exposures;

/// <summary>R1-18-UC3: opis izloženosti (endpoint, port, protokol, namena) kroz use-case.</summary>
[TestFixture]
public class DescribeExposureSurfaceHandlerTests : LifecycleTestBase
{
    private DescribeExposureSurfaceHandler _handler = null!;

    [SetUp]
    public void SetUp() => Start(AssetCriticality.Critical);

    private void Start(AssetCriticality criticality)
    {
        SetUpDirectories(criticality);
        Repo.Add(Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", "scope-a", "javni API partnera"));
        _handler = new DescribeExposureSurfaceHandler(Repo, Assets, Audit, new FixedTime(Now));
    }

    private static DescribeSurfaceCommand Cmd(
        string endpoint = "app.example.com", int port = 443, string protocol = "https", string purpose = "Javni portal") =>
        new("e-1", endpoint, port, protocol, purpose, Corr);

    private async Task<Exposure> Stored() => (await Repo.GetAsync("e-1"))!;

    [Test]
    public async Task Owner_describes_surface_and_it_is_stored_normalized()
    {
        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Changed, Is.True);
        var stored = await Stored();
        Assert.That(stored.Surface!.Endpoint, Is.EqualTo("app.example.com"));
        Assert.That(stored.Surface.Port, Is.EqualTo(443));
        Assert.That(stored.Surface.Protocol, Is.EqualTo("HTTPS"));
        Assert.That(stored.Surface.Purpose, Is.EqualTo("Javni portal"));
    }

    [Test]
    public async Task Critical_asset_writes_surface_event_and_critical_change_event()
    {
        await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(Actions(Audit.Events), Is.EqualTo(new[] { "exposure.critical.changed", "exposure.surface.described" }));
    }

    [Test]
    public async Task Low_criticality_asset_writes_only_the_surface_event()
    {
        Start(AssetCriticality.Low);

        await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(Actions(Audit.Events), Is.EqualTo(new[] { "exposure.surface.described" }));
    }

    [Test]
    public async Task Audit_never_contains_endpoint_or_purpose()
    {
        await _handler.HandleAsync(Owner(), Cmd(purpose: "tajna namena koja ne sme u audit"));

        var values = Audit.Events.SelectMany(e => e.Details.Values).ToList();
        Assert.That(values, Has.None.Contain("app.example.com"));
        Assert.That(values, Has.None.Contain("tajna namena"));
    }

    [Test]
    public async Task Same_description_again_is_idempotent_and_writes_no_second_audit()
    {
        await _handler.HandleAsync(Owner(), Cmd());
        var eventsBefore = Audit.Events.Count;

        var again = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(again.Succeeded, Is.True);
        Assert.That(again.Changed, Is.False);
        Assert.That(Audit.Events.Count, Is.EqualTo(eventsBefore));
    }

    [TestCase("", 443, "HTTPS", "x", "INVALID_ENDPOINT")]
    [TestCase("app.example.com", 0, "HTTPS", "x", "INVALID_PORT")]
    [TestCase("app.example.com", 443, "FTP", "x", "INVALID_PROTOCOL")]
    [TestCase("app.example.com", 443, "HTTPS", "  ", "INVALID_PURPOSE")]
    public async Task Invalid_input_is_rejected_and_nothing_is_stored(
        string endpoint, int port, string protocol, string purpose, string code)
    {
        var result = await _handler.HandleAsync(Owner(), Cmd(endpoint, port, protocol, purpose));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(code));
        Assert.That((await Stored()).Surface, Is.Null);
        Assert.That(Audit.Events, Is.Empty);
    }

    [Test]
    public async Task Other_owner_is_forbidden_and_the_denial_is_audited()
    {
        var result = await _handler.HandleAsync(OtherOwner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("FORBIDDEN"));
        Assert.That((await Stored()).Surface, Is.Null);
        var denied = Audit.Events.Single(e => e.Action == "exposure.access.denied");
        Assert.That(denied.Details["attempted"], Is.EqualTo("exposure.surface.describe"));
    }

    [Test]
    public async Task Actor_without_register_permission_is_forbidden()
    {
        var result = await _handler.HandleAsync(Analyst(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("FORBIDDEN"));
        Assert.That((await Stored()).Surface, Is.Null);
    }

    [Test]
    public async Task Other_scope_looks_like_not_found_and_changes_nothing()
    {
        var result = await _handler.HandleAsync(Owner("scope-b"), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        Assert.That((await Stored()).Surface, Is.Null);
    }

    [Test]
    public async Task Admin_may_describe_another_owners_exposure_in_the_same_scope()
    {
        var result = await _handler.HandleAsync(Admin(), Cmd());

        Assert.That(result.Succeeded, Is.True);
        Assert.That((await Stored()).Surface, Is.Not.Null);
    }

    [Test]
    public async Task Closed_exposure_cannot_be_described()
    {
        var e = await Stored();
        var version = e.Version;
        e.Close("decommissioned", Now);
        await Repo.SaveAsync(e, version);

        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("EXPOSURE_CLOSED"));
        Assert.That((await Stored()).Surface, Is.Null);
    }

    [Test]
    public async Task Asset_service_down_changes_nothing()
    {
        Assets.Unavailable = true;

        var result = await _handler.HandleAsync(Owner(), Cmd());

        Assert.That(result.ErrorCode, Is.EqualTo("UPSTREAM_UNAVAILABLE"));
        Assert.That((await Stored()).Surface, Is.Null);
    }
}