using NUnit.Framework;
using Scutum.T01.Domain.Assets;

namespace Scutum.T01.Tests.Domain;

[TestFixture]
public class AssetLifecycleTests
{
    [TestCase(LifecycleStatus.Active, LifecycleStatus.Active, false)]
    [TestCase(LifecycleStatus.Active, LifecycleStatus.Inactive, true)]
    [TestCase(LifecycleStatus.Active, LifecycleStatus.Decommissioned, true)]

    [TestCase(LifecycleStatus.Inactive, LifecycleStatus.Active, true)]
    [TestCase(LifecycleStatus.Inactive, LifecycleStatus.Inactive, false)]
    [TestCase(LifecycleStatus.Inactive, LifecycleStatus.Decommissioned, true)]

    [TestCase(LifecycleStatus.Decommissioned, LifecycleStatus.Active, false)]
    [TestCase(LifecycleStatus.Decommissioned, LifecycleStatus.Inactive, false)]
    [TestCase(LifecycleStatus.Decommissioned, LifecycleStatus.Decommissioned, false)]
    public void CanTransition_ReturnsExpectedResult(
        LifecycleStatus current,
        LifecycleStatus next,
        bool expected)
    {
        bool result = AssetLifecycle.CanTransition(current, next);

        Assert.That(result, Is.EqualTo(expected));
    }
}