using NUnit.Framework;
using Scutum.T01.Domain.Assets;
using Scutum.T01.Domain.Shared;

namespace Scutum.T01.Tests.Domain;

public sealed class AssetTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly DomainId D1 = new(Guid.NewGuid());
    private static readonly IdentityId Owner = new(Guid.NewGuid());

    private static Asset NewAsset() =>
        Asset.Register("CRM aplikacija", AssetType.Application, D1, Owner, Criticality.High, Now).Value!;

    [Test]
    public void Register_WithExplicitCriticality_IsActive()
    {
        var result = Asset.Register("CRM aplikacija", AssetType.Application, D1, Owner, Criticality.High, Now);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Value!.Status, Is.EqualTo(LifecycleStatus.Active));
        Assert.That(result.Value.Criticality, Is.EqualTo(Criticality.High));
    }

    [Test]
    public void Register_WithoutCriticality_IsRejected()
    {
        var result = Asset.Register("CRM aplikacija", AssetType.Application, D1, Owner, default, Now);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.Validation));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Register_WithoutName_IsRejected(string name)
    {
        var result = Asset.Register(name, AssetType.Application, D1, Owner, Criticality.Low, Now);

        Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.Validation));
    }

    [Test]
    public void ChangeCriticality_ReturnsOldValue()
    {
        var asset = NewAsset();

        var result = asset.ChangeCriticality(Criticality.Critical, Now);

        Assert.That(result.Value, Is.EqualTo(Criticality.High));
        Assert.That(asset.Criticality, Is.EqualTo(Criticality.Critical));
    }

    [Test]
    public void ChangeCriticality_OnDecommissionedAsset_IsRejectedAndStateIsKept()
    {
        var asset = NewAsset();
        asset.Decommission(Now);

        var result = asset.ChangeCriticality(Criticality.Low, Now);

        Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.InvalidTransition));
        Assert.That(asset.Criticality, Is.EqualTo(Criticality.High));
        Assert.That(asset.Status, Is.EqualTo(LifecycleStatus.Decommissioned));
    }
}
