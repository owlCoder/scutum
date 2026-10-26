using T08.Domain.Exposures;

namespace T08.Tests.Exposures;

/// <summary>
/// Matrica prelaza agregata Exposure (1.6, 3.5): svaka kombinacija početne i ciljne klasifikacije, i sve operacije nad `Closed`.
/// Izvor pravila: docs/domain-model.md (odeljak „Prelazi stanja").
/// </summary>
[TestFixture]
public class ExposureTransitionMatrixTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddDays(30);

    private static Exposure New() => Exposure.Register("e-1", "asset-1", "zone-1", "owner-1", "scope-a", "javni API partnera");

    private static DateTimeOffset? ExpiryFor(Classification c) => c == Classification.Temporary ? Later : null;

    private static Exposure In(Classification c)
    {
        var e = New();
        if (c != Classification.Unknown) e.Classify(c, ExpiryFor(c), Now);
        return e;
    }

    private static readonly Classification[] All = [Classification.Unknown, Classification.Approved, Classification.Temporary];

    public static IEnumerable<TestCaseData> ClassifyPairs() =>
        from from_ in All from to in All select new TestCaseData(from_, to).SetName($"Classify_{from_}_to_{to}");

    [TestCaseSource(nameof(ClassifyPairs))]
    public void Every_classification_pair_is_allowed_while_active(Classification from, Classification to)
    {
        var e = In(from);
        var versionBefore = e.Version;

        var changed = e.Classify(to, ExpiryFor(to), Now);

        // Isto stanje sa istim istekom je no-op (idempotentno, bez nove verzije); svaki drugi par menja stanje.
        Assert.That(changed, Is.EqualTo(from != to));
        Assert.That(e.Classification, Is.EqualTo(to));
        Assert.That(e.ExpiresAt, Is.EqualTo(ExpiryFor(to)));
        Assert.That(e.Version, Is.EqualTo(changed ? versionBefore + 1 : versionBefore));
        Assert.That(e.Status, Is.EqualTo(ExposureStatus.Active));
    }

    [Test]
    public void Temporary_to_Temporary_with_a_new_expiry_is_a_change()
    {
        var e = In(Classification.Temporary);
        var versionBefore = e.Version;

        Assert.That(e.Classify(Classification.Temporary, Later.AddDays(7), Now), Is.True);
        Assert.That(e.ExpiresAt, Is.EqualTo(Later.AddDays(7)));
        Assert.That(e.Version, Is.EqualTo(versionBefore + 1));
    }

    [TestCase(Classification.Unknown)]
    [TestCase(Classification.Approved)]
    public void Leaving_Temporary_clears_the_expiry(Classification to)
    {
        var e = In(Classification.Temporary);

        e.Classify(to, null, Now);

        Assert.That(e.ExpiresAt, Is.Null);
    }

    [Test]
    public void Undefined_classification_value_is_rejected_and_leaves_state_unchanged()
    {
        var e = In(Classification.Approved);

        var code = Assert.Throws<DomainRuleException>(() => e.Classify((Classification)99, null, Now))!.Code;

        Assert.That(code, Is.EqualTo(ExposureErrorCodes.InvalidClassification));
        Assert.That(e.Classification, Is.EqualTo(Classification.Approved));
    }

    // --- Closed je terminalno: nijedna operacija ne sme da ga menja. ---

    private static Exposure Closed(Classification c)
    {
        var e = In(c);
        e.Close("NO_LONGER_NEEDED", Now);
        return e;
    }

    [TestCaseSource(nameof(All))]
    public void Closed_exposure_rejects_classify_confirm_and_close_and_stays_unchanged(Classification c)
    {
        var e = Closed(c);
        var version = e.Version;
        var confirmedAt = e.LastConfirmedAt;

        foreach (var target in All)
        {
            var code = Assert.Throws<DomainRuleException>(() => e.Classify(target, ExpiryFor(target), Now))!.Code;
            Assert.That(code, Is.EqualTo(ExposureErrorCodes.ExposureClosed));
        }

        Assert.That(Assert.Throws<DomainRuleException>(() => e.Confirm(Later))!.Code, Is.EqualTo(ExposureErrorCodes.ExposureClosed));
        Assert.That(Assert.Throws<DomainRuleException>(() => e.Close("OTHER", Later))!.Code, Is.EqualTo(ExposureErrorCodes.ExposureClosed));

        Assert.That(e.Status, Is.EqualTo(ExposureStatus.Closed));
        Assert.That(e.Classification, Is.EqualTo(c));
        Assert.That(e.CloseReason, Is.EqualTo("NO_LONGER_NEEDED"));
        Assert.That(e.ClosedAt, Is.EqualTo(Now));
        Assert.That(e.LastConfirmedAt, Is.EqualTo(confirmedAt));
        Assert.That(e.Version, Is.EqualTo(version));
    }

    [TestCaseSource(nameof(All))]
    public void Close_is_allowed_from_every_classification_and_keeps_it(Classification c)
    {
        var e = In(c);

        e.Close("NO_LONGER_NEEDED", Now);

        Assert.That(e.Status, Is.EqualTo(ExposureStatus.Closed));
        Assert.That(e.Classification, Is.EqualTo(c));
    }

    [TestCaseSource(nameof(All))]
    public void Confirm_is_allowed_from_every_classification_and_does_not_change_it(Classification c)
    {
        var e = In(c);
        var versionBefore = e.Version;

        e.Confirm(Later);

        Assert.That(e.Classification, Is.EqualTo(c));
        Assert.That(e.LastConfirmedAt, Is.EqualTo(Later));
        Assert.That(e.Version, Is.EqualTo(versionBefore + 1));
    }
}
