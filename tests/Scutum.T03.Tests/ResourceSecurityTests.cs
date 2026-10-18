using NUnit.Framework;
using Scutum.T03.Application;
using Scutum.T03.Domain;
using Scutum.T03.Infrastructure;

namespace Scutum.T03.Tests;

public sealed class ResourceSecurityTests
{
    private DemoFixture _f = null!;
    private static Guid Correlation() => Guid.Parse("a0000000-0000-0000-0000-000000000001");
    [SetUp] public void SetUp() => _f = new DemoFixture();

    [Test] public void OwnerReadsOwnResource()
    {
        var result = _f.For(_f.Ana).Read(new("res-001", Correlation()));
        Assert.That(result.Outcome, Is.EqualTo(OperationOutcome.Granted));
        Assert.That(result.Resource!.OwnerId, Is.EqualTo("ana"));
    }
    [Test] public void OtherOwnerCannotReadByKnownId()
    {
        var result = _f.For(_f.Marko).Read(new("res-001", Correlation()));
        Assert.That(result.Outcome, Is.EqualTo(OperationOutcome.Denied));
        Assert.That(result.Resource, Is.Null);
        Assert.That(_f.Store.Audit.Single().ReasonCode, Is.EqualTo("NO_APPLICABLE_GRANT"));
    }
    [Test] public void OwnerWithoutPermissionIsDenied()
    {
        var grants = new PermissionsStub(new Dictionary<string, EffectivePermissions>
        { ["ana"] = new([]) });
        Assert.That(_f.For(_f.Ana, permissions: grants).Read(new("res-001", Correlation())).Outcome,
            Is.EqualTo(OperationOutcome.Denied));
    }
}
