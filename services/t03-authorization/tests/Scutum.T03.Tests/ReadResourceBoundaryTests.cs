using NUnit.Framework;
using Scutum.T03.Application;
using Scutum.T03.Domain;
using Scutum.T03.Infrastructure;

namespace Scutum.T03.Tests;

public sealed class ReadResourceBoundaryTests
{
    private DemoFixture _fixture = null!;
    private static readonly Guid Correlation = Guid.Parse("a0000000-0000-0000-0000-000000000002");

    [SetUp]
    public void SetUp() => _fixture = new DemoFixture();

    [Test]
    public void Read_WhenSubjectScopeDiffers_DeniesEvenWithGrantForTargetScope()
    {
        // Grant namerno odgovara asset-u: test izoluje proveru scope-a subjekta.
        var grants = Grants(_fixture.Ivana.Id, Permissions.ReadAny, "dom-a");
        var result = _fixture.For(_fixture.Ivana, permissions: grants)
            .Read(new("res-001", Correlation));

        AssertDenied(result, "SCOPE_MISMATCH");
    }

    [TestCase(Permissions.ReadOwn)]
    [TestCase(Permissions.ReadAny)]
    public void Read_WhenGrantIsForAnotherScope_DeniesOwner(string permission)
    {
        // Subjekt i asset jesu u istom scope-u; pogrešan je samo grant.
        var grants = Grants(_fixture.Ana.Id, permission, "dom-b");
        var result = _fixture.For(_fixture.Ana, permissions: grants)
            .Read(new("res-001", Correlation));

        AssertDenied(result, "NO_APPLICABLE_GRANT");
    }

    [Test]
    public void Read_WhenReadAnyMatchesScope_AllowsNonOwner()
    {
        var result = _fixture.For(_fixture.Reviewer).Read(new("res-001", Correlation));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(OperationOutcome.Granted));
            Assert.That(result.Resource?.OwnerId, Is.EqualTo("ana"));
            Assert.That(_fixture.Store.Audit.Single().ReasonCode, Is.EqualTo("SCOPED_READ_ANY"));
        }
    }

    [Test]
    public void Read_WhenAssetIsInactive_Denies()
    {
        var assets = new AssetsStub(new Dictionary<string, AssetSnapshot>
        {
            ["asset-a"] = new("asset-a", "dom-a", false)
        });
        var result = _fixture.For(_fixture.Ana, assets: assets).Read(new("res-001", Correlation));

        AssertDenied(result, "INACTIVE_RESOURCE");
    }

    [Test]
    public void Read_WhenSubjectIsInactive_DeniesDespiteExistingPermission()
    {
        var subject = _fixture.Ana with { IsActive = false };
        var result = _fixture.For(subject).Read(new("res-001", Correlation));

        AssertDenied(result, "INACTIVE_SUBJECT");
    }

    [TestCase("permissions")]
    [TestCase("assets")]
    public void Read_WhenDependencyIsUnavailable_FailsClosed(string dependency)
    {
        var before = _fixture.Store.Find("res-001");
        _fixture.Permissions.IsUnavailable = dependency == "permissions";
        _fixture.Assets.IsUnavailable = dependency == "assets";

        var result = _fixture.For(_fixture.Ana).Read(new("res-001", Correlation));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(new OperationResult(OperationOutcome.Unavailable,
                "DEPENDENCY_UNAVAILABLE", "Operacija trenutno nije dostupna.")));
            Assert.That(_fixture.Store.Find("res-001"), Is.EqualTo(before));
            Assert.That(_fixture.Store.Audit.Single().Outcome, Is.EqualTo("UNAVAILABLE"));
            Assert.That(_fixture.Store.Audit.Single().ReasonCode, Is.EqualTo("DEPENDENCY_UNAVAILABLE"));
            Assert.That(_fixture.Store.Audit.Single().CorrelationId, Is.EqualTo(Correlation));
        }
    }

    [Test]
    public void Read_WhenResourceIsMissingOrForeign_ReturnsIdenticalPublicResponse()
    {
        var handler = _fixture.For(_fixture.Marko);
        var foreign = handler.Read(new("res-001", Correlation));
        var missing = handler.Read(new("res-missing", Correlation));

        using (Assert.EnterMultipleScope())
        {
            // Record jednakost poredi sva javna polja, ne samo Outcome.
            Assert.That(foreign, Is.EqualTo(new OperationResult(OperationOutcome.Denied,
                "RESOURCE_UNAVAILABLE", "Resurs nije dostupan.")));
            Assert.That(missing, Is.EqualTo(foreign));
            Assert.That(_fixture.Store.Audit.Select(entry => entry.ReasonCode),
                Is.EqualTo(new[] { "NO_APPLICABLE_GRANT", "RESOURCE_NOT_FOUND" }));
        }
    }

    private void AssertDenied(OperationResult result, string reason)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(new OperationResult(OperationOutcome.Denied,
                "RESOURCE_UNAVAILABLE", "Resurs nije dostupan.")));
            Assert.That(_fixture.Store.Audit.Single().ReasonCode, Is.EqualTo(reason));
            Assert.That(_fixture.Store.Audit.Single().Outcome, Is.EqualTo("DENIED"));
            Assert.That(_fixture.Store.Audit.Single().CorrelationId, Is.EqualTo(Correlation));
        }
    }

    private static PermissionsStub Grants(string subjectId, string permission, string scope) =>
        new(new Dictionary<string, EffectivePermissions>
        {
            [subjectId] = new([new(permission, scope)])
        });
}
