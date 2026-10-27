using T08.Domain.Exposures;

namespace T08.Tests.Exposures;

[TestFixture]
public class ExposureSurfaceTests
{
    private static Exposure New() => Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", "scope-a", "javni API partnera");

    private static ExposureSurface Valid() => ExposureSurface.Create("app.example.com", 443, "HTTPS", "Javni portal");

    private static string CodeOf(TestDelegate act) => Assert.Throws<DomainRuleException>(act)!.Code;

    [Test]
    public void Create_trims_and_normalizes_input()
    {
        var s = ExposureSurface.Create("  app.example.com ", 443, "https", " Javni portal ");

        Assert.That(s.Endpoint, Is.EqualTo("app.example.com"));
        Assert.That(s.Protocol, Is.EqualTo("HTTPS"));
        Assert.That(s.Purpose, Is.EqualTo("Javni portal"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("bad host.example.com")]
    public void Invalid_endpoint_is_rejected(string? endpoint)
    {
        Assert.That(CodeOf(() => ExposureSurface.Create(endpoint, 443, "HTTPS", "x")), Is.EqualTo("INVALID_ENDPOINT"));
    }

    [Test]
    public void Endpoint_longer_than_dns_limit_is_rejected()
    {
        var tooLong = new string('a', ExposureSurface.MaxEndpointLength + 1);

        Assert.That(CodeOf(() => ExposureSurface.Create(tooLong, 443, "HTTPS", "x")), Is.EqualTo("INVALID_ENDPOINT"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(65536)]
    public void Port_out_of_range_is_rejected(int port)
    {
        Assert.That(CodeOf(() => ExposureSurface.Create("app.example.com", port, "HTTPS", "x")), Is.EqualTo("INVALID_PORT"));
    }

    [TestCase(1)]
    [TestCase(65535)]
    public void Port_boundaries_are_accepted(int port)
    {
        Assert.That(ExposureSurface.Create("app.example.com", port, "HTTPS", "x").Port, Is.EqualTo(port));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("FTP")]
    public void Unknown_protocol_is_rejected(string? protocol)
    {
        Assert.That(CodeOf(() => ExposureSurface.Create("app.example.com", 443, protocol, "x")), Is.EqualTo("INVALID_PROTOCOL"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Purpose_is_required(string? purpose)
    {
        Assert.That(CodeOf(() => ExposureSurface.Create("app.example.com", 443, "HTTPS", purpose)), Is.EqualTo("INVALID_PURPOSE"));
    }

    [Test]
    public void Purpose_longer_than_limit_is_rejected()
    {
        var tooLong = new string('p', ExposureSurface.MaxPurposeLength + 1);

        Assert.That(CodeOf(() => ExposureSurface.Create("app.example.com", 443, "HTTPS", tooLong)), Is.EqualTo("INVALID_PURPOSE"));
    }

    [Test]
    public void New_exposure_has_no_surface_until_described()
    {
        Assert.That(New().Surface, Is.Null);
    }

    [Test]
    public void DescribeSurface_stores_it_and_bumps_version()
    {
        var e = New();

        var changed = e.DescribeSurface(Valid());

        Assert.That(changed, Is.True);
        Assert.That(e.Surface, Is.EqualTo(Valid()));
        Assert.That(e.Version, Is.EqualTo(2));
    }

    [Test]
    public void DescribeSurface_with_same_values_is_idempotent()
    {
        var e = New();
        e.DescribeSurface(Valid());

        var changed = e.DescribeSurface(Valid());

        Assert.That(changed, Is.False);
        Assert.That(e.Version, Is.EqualTo(2));
    }

    [Test]
    public void Closed_exposure_cannot_be_described_and_keeps_its_surface()
    {
        var e = New();
        e.DescribeSurface(Valid());
        e.Close("REMOVED", DateTimeOffset.UtcNow);

        var other = ExposureSurface.Create("other.example.com", 80, "HTTP", "Drugo");

        Assert.That(CodeOf(() => e.DescribeSurface(other)), Is.EqualTo("EXPOSURE_CLOSED"));
        Assert.That(e.Surface, Is.EqualTo(Valid()));
    }
}