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
    public const string Closed = "exposure.closed";
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

    public static AuditEvent ClassificationWasChanged(DateTimeOffset at, string actor, Guid correlationId,
        string exposureId, string assetId, string from, string to, AssetCriticality criticality) =>
        AuditEvent.Create(at, actor, ClassificationChanged, Target(exposureId), AuditOutcome.Success, correlationId,
            SensitivityFor(criticality),
            new Dictionary<string, string> { ["assetId"] = assetId, ["from"] = from, ["to"] = to });

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
}
