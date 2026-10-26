namespace T08.Domain.Exposures;

/// <summary>`Unknown` znači „za proveru": nikad „ranjivost" ni „bezbedno" (domain-model.md).</summary>
public enum Classification { Unknown, Approved, Temporary }

public enum ExposureStatus { Active, Closed }

/// <summary>Povreda domenskog pravila sa stabilnim, mašinski prepoznatljivim kodom (SEC-CONV-03).</summary>
public sealed class DomainRuleException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public static class ExposureErrorCodes
{
    public const string ExposureClosed = "EXPOSURE_CLOSED";
    public const string InvalidClassification = "INVALID_CLASSIFICATION";
    public const string InvalidExpiry = "INVALID_EXPIRY";
    public const string InvalidExposure = "INVALID_EXPOSURE";
}

/// <summary>
/// Exposure kao agregat. Ovde je samo ono što R1-18-UC4 zahteva; registracija, potvrda i zatvaranje
/// proširuju isti agregat u svojim UC-ovima. Tuđe podatke (asset, zona) čuva samo kao ID (SEC-CONV-10).
/// </summary>
public sealed class Exposure
{
    public string Id { get; private init; } = "";
    public string AssetId { get; private init; } = "";
    public string ZoneId { get; private init; } = "";
    public string OwnerId { get; private init; } = "";
    /// <summary>Security domen preuzet iz asset-a pri registraciji.</summary>
    public string Scope { get; private init; } = "";
    public string Justification { get; private init; } = "";
    public Classification Classification { get; private set; } = Classification.Unknown;
    public ExposureStatus Status { get; private set; } = ExposureStatus.Active;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public string? CloseReason { get; private set; }
    /// <summary>Poslednja potvrda da je izloženost i dalje potrebna (periodični pregled); null dok nije potvrđena.</summary>
    public DateTimeOffset? LastConfirmedAt { get; private set; }
    /// <summary>Raste pri svakoj izmeni; koristi se za optimistic concurrency (SEC-CONV-09).</summary>
    public int Version { get; private set; } = 1;

    private Exposure() { }

    /// <summary>Početna klasifikacija je uvek <see cref="Classification.Unknown"/>.</summary>
    public static Exposure Register(string id, string assetId, string zoneId, string ownerId, string scope, string justification)
    {
        foreach (var value in new[] { id, assetId, zoneId, ownerId, scope, justification })
        {
            if (string.IsNullOrWhiteSpace(value)) throw new DomainRuleException(ExposureErrorCodes.InvalidExposure);
        }

        return new Exposure
        {
            Id = id, AssetId = assetId, ZoneId = zoneId, OwnerId = ownerId, Scope = scope, Justification = justification
        };
    }

    /// <summary>
    /// Menja klasifikaciju. <c>Temporary</c> traži <paramref name="expiresAt"/> u budućnosti, ostale klasifikacije ga ne smeju imati.
    /// Povratak u <c>Unknown</c> je dozvoljen (ponovna provera). Vraća false ako se ništa ne menja (idempotentno).
    /// </summary>
    public bool Classify(Classification target, DateTimeOffset? expiresAt, DateTimeOffset now)
    {
        if (Status == ExposureStatus.Closed) throw new DomainRuleException(ExposureErrorCodes.ExposureClosed);
        if (!Enum.IsDefined(target)) throw new DomainRuleException(ExposureErrorCodes.InvalidClassification);

        if (target == Classification.Temporary)
        {
            if (expiresAt is null || expiresAt.Value <= now) throw new DomainRuleException(ExposureErrorCodes.InvalidExpiry);
        }
        else if (expiresAt is not null)
        {
            throw new DomainRuleException(ExposureErrorCodes.InvalidExpiry);
        }

        if (target == Classification && expiresAt == ExpiresAt) return false;

        Classification = target;
        ExpiresAt = expiresAt;
        Version++;
        return true;
    }

    /// <summary>Potvrda da je izloženost i dalje potrebna. Svaka potvrda je nov pregled (nije idempotentna): podiže verziju.</summary>
    public void Confirm(DateTimeOffset now)
    {
        if (Status == ExposureStatus.Closed) throw new DomainRuleException(ExposureErrorCodes.ExposureClosed);

        LastConfirmedAt = now;
        Version++;
    }

    /// <summary>`Closed` je terminalno i ostaje u istoriji (ne briše se, ne menja).</summary>
    public void Close(string reasonCode, DateTimeOffset now)
    {
        if (Status == ExposureStatus.Closed) throw new DomainRuleException(ExposureErrorCodes.ExposureClosed);
        if (string.IsNullOrWhiteSpace(reasonCode)) throw new DomainRuleException(ExposureErrorCodes.InvalidExposure);

        Status = ExposureStatus.Closed;
        ClosedAt = now;
        CloseReason = reasonCode;
        Version++;
    }

    /// <summary>Nezavisna kopija, da repozitorijum ne deli promenljivo stanje sa pozivaocem.</summary>
    public Exposure Copy() => (Exposure)MemberwiseClone();
}
