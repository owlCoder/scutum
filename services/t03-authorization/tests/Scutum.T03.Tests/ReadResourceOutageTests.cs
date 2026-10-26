using NUnit.Framework;
using Scutum.T03.Application;
using Scutum.T03.Infrastructure;

namespace Scutum.T03.Tests;

public sealed class ReadResourceOutageTests
{
    [TestCase("assets", "ASSET_UNAVAILABLE", "RESOURCE_NOT_FOUND")]
    [TestCase("permissions", "PERMISSIONS_UNAVAILABLE", "PERMISSIONS_UNAVAILABLE")]
    public void Read_WhenDependencyIsUnavailable_MissingAndForeignHaveSamePublicResponse(
        string dependency, string foreignReason, string missingReason)
    {
        var fixture = new DemoFixture();
        var before = fixture.Store.Find("res-001");
        var foreignCorrelation = Guid.Parse("a0000000-0000-0000-0000-000000000003");
        var missingCorrelation = Guid.Parse("a0000000-0000-0000-0000-000000000004");
        fixture.Assets.IsUnavailable = dependency == "assets";
        fixture.Permissions.IsUnavailable = dependency == "permissions";
        var handler = fixture.For(fixture.Marko);

        var foreign = handler.Read(new("res-001", foreignCorrelation));
        var missing = handler.Read(new("res-missing", missingCorrelation));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(foreign, Is.EqualTo(new OperationResult(OperationOutcome.Denied,
                "RESOURCE_UNAVAILABLE", "Resurs nije dostupan.")));
            Assert.That(missing, Is.EqualTo(foreign));
            Assert.That(fixture.Store.Find("res-001"), Is.EqualTo(before));
            Assert.That(fixture.Store.Find("res-missing"), Is.Null);
            Assert.That(fixture.Store.Audit, Has.Count.EqualTo(2));
            Assert.That(fixture.Store.Audit.Select(entry => entry.Outcome),
                Is.EqualTo(new[] { "DENIED", "DENIED" }));
            Assert.That(fixture.Store.Audit.Select(entry => entry.ReasonCode),
                Is.EqualTo(new[] { foreignReason, missingReason }));
            Assert.That(fixture.Store.Audit.Select(entry => entry.TargetId),
                Is.EqualTo(new[] { "res-001", "res-missing" }));
            Assert.That(fixture.Store.Audit.Select(entry => entry.CorrelationId),
                Is.EqualTo(new[] { foreignCorrelation, missingCorrelation }));
        }
    }
}
