using System.Net;
using System.Text;
using System.Text.Json;
using T08.Application.Directories;
using T08.Infrastructure.Directories;
using T08.Infrastructure.Stubs;

namespace T08.Tests.Infrastructure;

// SEC-DEP-04: isti skup zahteva se izvršava nad stub-om i nad pravim HTTP adapterom.
// Apstraktna klasa definiše ugovor (C-ASSET-v1 / C-ZONE-v1 kako ga T08 koristi); svaka implementacija
// samo prevodi „stanje drugog servisa" (World) na svoj način. Novi adapter = nova podklasa, isti testovi.

/// <summary>Stanje koje „T01" ima u scenariju.</summary>
internal sealed class AssetWorld
{
    public List<AssetInfo> Assets { get; } = [];
    public bool Unavailable { get; set; }
}

/// <summary>Stanje koje „T11" ima u scenariju.</summary>
internal sealed class ZoneWorld
{
    public List<ZoneInfo> Zones { get; } = [];
    public bool Unavailable { get; set; }
}

public abstract class AssetReaderContract
{
    private static readonly Guid Corr = Guid.NewGuid();
    private static readonly AssetInfo ActiveAsset =
        new("asset-1", "user-1", AssetCriticality.Critical, AssetStatus.Active, "scope-a");

    internal abstract IAssetReader Build(AssetWorld world);

    private static AssetWorld With(params AssetInfo[] assets)
    {
        var world = new AssetWorld();
        world.Assets.AddRange(assets);
        return world;
    }

    [Test]
    public async Task Active_asset_is_found_with_all_fields()
    {
        var result = await Build(With(ActiveAsset)).GetAsync("asset-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Found));
        Assert.That(result.Value, Is.EqualTo(ActiveAsset));
    }

    [Test]
    public async Task Asset_with_guid_identifier_starting_with_a_digit_is_found()
    {
        var guid = Guid.NewGuid().ToString("D");
        var asset = ActiveAsset with { Id = guid };

        var result = await Build(With(asset)).GetAsync(guid, Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Found));
        Assert.That(result.Value!.Id, Is.EqualTo(guid));
    }

    [Test]
    public async Task Unknown_asset_is_not_found()
    {
        var result = await Build(With(ActiveAsset)).GetAsync("asset-x", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.NotFound));
        Assert.That(result.Value, Is.Null);
    }

    [TestCase(AssetStatus.Decommissioned)]
    [TestCase(AssetStatus.Inactive)]
    public async Task Non_active_asset_is_inactive_and_carries_no_value(AssetStatus status)
    {
        var old = ActiveAsset with { Id = "asset-old", Status = status };

        var result = await Build(With(ActiveAsset, old)).GetAsync("asset-old", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Inactive));
        Assert.That(result.Value, Is.Null);
    }

    [Test]
    public async Task Unavailable_upstream_is_never_a_success()
    {
        var world = With(ActiveAsset);
        world.Unavailable = true;

        var result = await Build(world).GetAsync("asset-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Unavailable));
        Assert.That(result.Value, Is.Null);
    }

    [TestCase("../zones/zone-1")]
    [TestCase("Asset-1")]
    [TestCase("asset-1\n")]
    [TestCase("")]
    public async Task Invalid_identifier_is_not_found(string id)
    {
        var result = await Build(With(ActiveAsset)).GetAsync(id, Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.NotFound));
        Assert.That(result.Value, Is.Null);
    }
}

public abstract class ZoneReaderContract
{
    private static readonly Guid Corr = Guid.NewGuid();
    private static readonly ZoneInfo ActiveZone = new("zone-1", "user-2", ZoneTrustLevel.Dmz, ZoneStatus.Active);

    internal abstract IZoneReader Build(ZoneWorld world);

    private static ZoneWorld With(params ZoneInfo[] zones)
    {
        var world = new ZoneWorld();
        world.Zones.AddRange(zones);
        return world;
    }

    [Test]
    public async Task Active_zone_is_found_with_all_fields()
    {
        var result = await Build(With(ActiveZone)).GetAsync("zone-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Found));
        Assert.That(result.Value, Is.EqualTo(ActiveZone));
    }

    [TestCase(ZoneTrustLevel.Internal)]
    [TestCase(ZoneTrustLevel.Dmz)]
    [TestCase(ZoneTrustLevel.External)]
    public async Task Every_trust_level_is_preserved(ZoneTrustLevel level)
    {
        var zone = ActiveZone with { TrustLevel = level };

        var result = await Build(With(zone)).GetAsync("zone-1", Corr);

        Assert.That(result.Value!.TrustLevel, Is.EqualTo(level));
    }

    [Test]
    public async Task Unknown_zone_is_not_found()
    {
        var result = await Build(With(ActiveZone)).GetAsync("zone-x", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.NotFound));
        Assert.That(result.Value, Is.Null);
    }

    [Test]
    public async Task Inactive_zone_is_inactive_and_carries_no_value()
    {
        var old = ActiveZone with { Id = "zone-old", Status = ZoneStatus.Inactive };

        var result = await Build(With(ActiveZone, old)).GetAsync("zone-old", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Inactive));
        Assert.That(result.Value, Is.Null);
    }

    [Test]
    public async Task Unavailable_upstream_is_never_a_success()
    {
        var world = With(ActiveZone);
        world.Unavailable = true;

        var result = await Build(world).GetAsync("zone-1", Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.Unavailable));
        Assert.That(result.Value, Is.Null);
    }

    [TestCase("../assets/asset-1")]
    [TestCase("Zone-1")]
    [TestCase("zone-1\n")]
    [TestCase("")]
    public async Task Invalid_identifier_is_not_found(string id)
    {
        var result = await Build(With(ActiveZone)).GetAsync(id, Corr);

        Assert.That(result.Outcome, Is.EqualTo(DirectoryOutcome.NotFound));
        Assert.That(result.Value, Is.Null);
    }
}

[TestFixture]
public class StubAssetReaderContractTests : AssetReaderContract
{
    internal override IAssetReader Build(AssetWorld world)
    {
        var stub = new StubAssetDirectory { Unavailable = world.Unavailable };
        foreach (var asset in world.Assets) stub.Add(asset);
        return stub;
    }

    [Test]
    public void Runs_against_the_stub() =>
        Assert.That(Build(new AssetWorld()), Is.TypeOf<StubAssetDirectory>());
}

[TestFixture]
public class HttpAssetReaderContractTests : AssetReaderContract
{
    internal override IAssetReader Build(AssetWorld world) =>
        new HttpAssetDirectory(new FakeUpstream(request => Serve(world, request)).Client());

    /// <summary>
    /// Simulira T01 po C-ASSET-v1: 200 sa telom, 404 ako ne postoji, 409 ASSET_DECOMMISSIONED ako je dekomisionisan,
    /// 503 ako je pao. <c>Inactive</c> nije u ugovoru; fake ga šalje kao 200 sa telom (tolerantan čitalac).
    /// </summary>
    private static HttpResponseMessage Serve(AssetWorld world, HttpRequestMessage request)
    {
        if (world.Unavailable) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        var id = request.RequestUri!.AbsolutePath["/api/v1/assets/".Length..];
        var asset = world.Assets.FirstOrDefault(a => a.Id == id);
        if (asset is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
        if (asset.Status == AssetStatus.Decommissioned)
            return new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    """{"code":"ASSET_DECOMMISSIONED","message":"decommissioned"}""", Encoding.UTF8, "application/json")
            };

        var body = JsonSerializer.Serialize(new
        {
            id = asset.Id,
            ownerId = asset.OwnerId,
            criticality = asset.Criticality.ToString(),
            status = asset.Status.ToString(),
            scope = asset.Scope
        });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    [Test]
    public void Runs_against_the_real_http_adapter() =>
        Assert.That(Build(new AssetWorld()), Is.TypeOf<HttpAssetDirectory>());
}

[TestFixture]
public class StubZoneReaderContractTests : ZoneReaderContract
{
    internal override IZoneReader Build(ZoneWorld world)
    {
        var stub = new StubZoneDirectory { Unavailable = world.Unavailable };
        foreach (var zone in world.Zones) stub.Add(zone);
        return stub;
    }

    [Test]
    public void Runs_against_the_stub() =>
        Assert.That(Build(new ZoneWorld()), Is.TypeOf<StubZoneDirectory>());
}

[TestFixture]
public class HttpZoneReaderContractTests : ZoneReaderContract
{
    internal override IZoneReader Build(ZoneWorld world) =>
        new HttpZoneDirectory(new FakeUpstream(request => Serve(world, request)).Client());

    /// <summary>Simulira T11 po C-ZONE-v1: 200 sa telom, 404 ako ne postoji, 409 ZONE_INACTIVE ako je neaktivna, 503 ako je pao.</summary>
    private static HttpResponseMessage Serve(ZoneWorld world, HttpRequestMessage request)
    {
        if (world.Unavailable) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        var id = request.RequestUri!.AbsolutePath["/api/v1/zones/".Length..];
        var zone = world.Zones.FirstOrDefault(z => z.Id == id);
        if (zone is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
        if (zone.Status == ZoneStatus.Inactive)
            return new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    """{"code":"ZONE_INACTIVE","message":"inactive"}""", Encoding.UTF8, "application/json")
            };

        var body = JsonSerializer.Serialize(new
        {
            id = zone.Id,
            ownerId = zone.OwnerId,
            trustLevel = zone.TrustLevel.ToString(),
            status = zone.Status.ToString()
        });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    [Test]
    public void Runs_against_the_real_http_adapter() =>
        Assert.That(Build(new ZoneWorld()), Is.TypeOf<HttpZoneDirectory>());
}
