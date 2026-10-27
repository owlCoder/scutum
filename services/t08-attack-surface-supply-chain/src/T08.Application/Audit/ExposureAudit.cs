using T08.Application.Directories;

namespace T08.Application.Audit;

/// <summary>
/// Audit događaji za exposure (R1-18). Jedino mesto gde se grade, da imena akcija, osetljivost i sadržaj
/// <c>details</c> ostanu isti svuda. Details nosi samo identifikatore i kodove, nikad endpoint, justification ni tajne.
/// </summary>
public static class ExposureAudit
{
    public const string Registered = "exposure.registered";
    public const string ClassificationChanged = "exposure.classification.changed";
    public const string Confirmed = "exposure.confirmed";
    public const string Closed = "exposure.closed";
    /// <summary>Promena izloženosti asset-a Critical/High; piše se uz događaj konkretne promene (npr. klasifikacije).</summary>
    public const string SurfaceDescribed = "exposure.surface.described";
    public const string CriticalExposureChanged = "exposure.critical.changed";
    public const string AccessDenied = "exposure.access.denied";

    public static string Target(string exposureId) => $"exposure:{exposureId}";

    /// <summary>Osetljivost prati kritičnost asset-a: Critical/High → High.</summary>
    public static AuditSensitivity SensitivityFor(AssetCriticality criticality) => criticality switch
    {
        AssetCriticality.Critical or AssetCriticality.High => AuditSensitivity.High,
        AssetCriticality.Medium => AuditSensitivity.Medium,
        _ => AuditSensitivity.Low
    };

    /// <summary>Domen model: promena klasifikacije/statusa exposure-a Critical/High asset-a je obavezno auditovana.</summary>
    public static bool IsMandatory(AssetCriticality criticality) =>
        criticality is AssetCriticality.Critical or AssetCriticality.High;

    public static AuditEvent ExposureRegistered(DateTimeOffset at, string actor, Guid correlationId,
        string exposureId, string assetId, string zoneId, AssetCriticality criticality) =>
        AuditEvent.Create(at, actor, Registered, Target(exposureId), AuditOutcome.Success, correlationId,
            SensitivityFor(criticality),
            new Dictionary<string, string> { ["assetId"] = assetId, ["zoneId"] = zoneId });

    /// <summary>
    /// <paramref name="expiresAt"/> (rok <c>Temporary</c>) se beleži da se produženje roka razlikuje od istog stanja
    /// (from i to su tada isti, a rok je menjan).
    /// </summary>
    public static AuditEvent ClassificationWasChanged(DateTimeOffset at, string actor, Guid correlationId,
        string exposureId, string assetId, string from, string to, AssetCriticality criticality,
        DateTimeOffset? expiresAt = null)
    {
        var details = new Dictionary<string, string> { ["assetId"] = assetId, ["from"] = from, ["to"] = to };
        if (expiresAt is not null) details["expiresAt"] = expiresAt.Value.ToUniversalTime().ToString("O");

        return AuditEvent.Create(at, actor, ClassificationChanged, Target(exposureId), AuditOutcome.Success, correlationId,
            SensitivityFor(criticality), details);
    }

    /// <summary>UC3: evidentiran opis izloženosti. Endpoint, port i namena se NE upisuju (samo identifikatori).</summary>
    public static AuditEvent SurfaceWasDescribed(DateTimeOffset at, string actor, Guid correlationId,
        string exposureId, string assetId, AssetCriticality criticality) =>
        AuditEvent.Create(at, actor, SurfaceDescribed, Target(exposureId), AuditOutcome.Success, correlationId,
            SensitivityFor(criticality),
            new Dictionary<string, string> { ["assetId"] = assetId });

    /// <summary>Potvrda da je izloženost i dalje potrebna (periodični pregled). Nosi samo identifikatore.</summary>
    public static AuditEvent ExposureConfirmed(DateTimeOffset at, string actor, Guid correlationId,
        string exposureId, string assetId, AssetCriticality criticality) =>
        AuditEvent.Create(at, actor, Confirmed, Target(exposureId), AuditOutcome.Success, correlationId,
            SensitivityFor(criticality),
            new Dictionary<string, string> { ["assetId"] = assetId });

    /// <summary>
    /// Promena izloženosti kritičnog (Critical/High) asset-a. <paramref name="change"/> je kod vrste promene
    /// (npr. <c>classification</c>, <c>closed</c>), a <paramref name="from"/>/<paramref name="to"/> su kodovi stanja. Uvek High.
    /// </summary>
    public static AuditEvent CriticalExposureWasChanged(DateTimeOffset at, string actor, Guid correlationId,
        string exposureId, string assetId, string change, string from, string to) =>
        AuditEvent.Create(at, actor, CriticalExposureChanged, Target(exposureId), AuditOutcome.Success, correlationId,
            AuditSensitivity.High,
            new Dictionary<string, string> { ["assetId"] = assetId, ["change"] = change, ["from"] = from, ["to"] = to });

    public static AuditEvent ExposureClosed(DateTimeOffset at, string actor, Guid correlationId,
        string exposureId, string assetId, string reasonCode, AssetCriticality criticality) =>
        AuditEvent.Create(at, actor, Closed, Target(exposureId), AuditOutcome.Success, correlationId,
            SensitivityFor(criticality),
            new Dictionary<string, string> { ["assetId"] = assetId, ["reason"] = reasonCode });

    /// <summary>
    /// Odbijen pristup (uključujući resurs van scope-a). <paramref name="attemptedAction"/> je akcija koja je tražena,
    /// <paramref name="errorCode"/> stabilan kod greške. Kritičnost nije poznata (ili nije smela da se otkrije), pa je Medium.
    /// </summary>
    public static AuditEvent Denied(DateTimeOffset at, string actor, Guid correlationId,
        string attemptedAction, string exposureId, string errorCode) =>
        AuditEvent.Create(at, actor, AccessDenied, Target(exposureId), AuditOutcome.Failure, correlationId,
            AuditSensitivity.Medium,
            new Dictionary<string, string> { ["attempted"] = attemptedAction, ["code"] = errorCode });

    /// <summary>
    /// Odbijena registracija: exposure još ne postoji, pa je cilj asset (<c>asset:{id}</c>). Pozivalac mora da je
    /// proverio format <paramref name="assetId"/> (ContractIdentifier), da se tuđi tekst ne upisuje u audit.
    /// </summary>
    public static AuditEvent RegistrationDenied(DateTimeOffset at, string actor, Guid correlationId,
        string assetId, string errorCode) =>
        AuditEvent.Create(at, actor, AccessDenied, $"asset:{assetId}", AuditOutcome.Failure, correlationId,
            AuditSensitivity.Medium,
            new Dictionary<string, string> { ["attempted"] = Registered, ["code"] = errorCode });
}
