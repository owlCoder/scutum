using T08.Application.Audit;
using T08.Application.Directories;
using T08.Domain.Exposures;

namespace T08.Application.Exposures;

public sealed record DescribeSurfaceCommand(
    string ExposureId, string Endpoint, int Port, string Protocol, string Purpose, Guid CorrelationId);

/// <param name="Changed">false ako je opis isti kao postojeći (idempotentno, bez audita).</param>
/// <param name="AuditDelivered">false ako je promena upisana, ali audit događaj nije prihvaćen ni sačuvan.</param>
public sealed record DescribeSurfaceResult(bool Succeeded, string? ErrorCode, bool Changed, bool AuditDelivered = true)
{
    public static DescribeSurfaceResult Ok(bool changed, bool auditDelivered = true) => new(true, null, changed, auditDelivered);
    public static DescribeSurfaceResult Fail(string code) => new(false, code, false);
}

/// <summary>
/// R1-18-UC3: evidencija endpoint-a, porta, protokola i namene uz exposure.
/// Redosled: scope → dozvola (vlasnik ili SA) → zatvoren? → validacija opisa → asset (kritičnost) → upis → audit.
/// Audit nosi samo identifikatore i kodove: endpoint i namena nikad ne idu u audit.
/// </summary>
public sealed class DescribeExposureSurfaceHandler(
    IExposureRepository exposures,
    IAssetReader assets,
    IAuditSink audit,
    TimeProvider time)
{
    private const string Action = "exposure.surface.describe";

    public async Task<DescribeSurfaceResult> HandleAsync(
        ActorContext actor, DescribeSurfaceCommand command, CancellationToken ct = default)
    {
        // Evidentiranje opisa je deo upravljanja exposure-om: ista dozvola i ista objektna provera kao registracija.
        var (exposure, error) = await LifecycleSupport.LoadAuthorizedAsync(
            exposures, audit, time, actor, command.ExposureId, command.CorrelationId, Action, Permissions.ExposureRegister, ct)
            .ConfigureAwait(false);
        if (exposure is null) return DescribeSurfaceResult.Fail(error!);

        if (exposure.Status == ExposureStatus.Closed) return DescribeSurfaceResult.Fail(ExposureErrorCodes.ExposureClosed);

        // Validacija pre poziva ka T01: nevalidan unos ne troši spoljni poziv.
        ExposureSurface surface;
        try
        {
            surface = ExposureSurface.Create(command.Endpoint, command.Port, command.Protocol, command.Purpose);
        }
        catch (DomainRuleException ex)
        {
            return DescribeSurfaceResult.Fail(ex.Code);
        }

        var asset = await assets.GetAsync(exposure.AssetId, command.CorrelationId, ct).ConfigureAwait(false);
        if (asset.Outcome == DirectoryOutcome.Unavailable) return DescribeSurfaceResult.Fail(ErrorCodes.UpstreamUnavailable);
        if (asset.Outcome != DirectoryOutcome.Found) return DescribeSurfaceResult.Fail(ErrorCodes.AssetNotUsable);
        if (asset.Value!.Scope != exposure.Scope) return DescribeSurfaceResult.Fail(ErrorCodes.AssetScopeMismatch);
        var criticality = asset.Value.Criticality;

        var now = time.GetUtcNow();
        var expectedVersion = exposure.Version;
        var hadSurface = exposure.Surface is not null;

        bool changed;
        try
        {
            changed = exposure.DescribeSurface(surface);
        }
        catch (DomainRuleException ex)
        {
            return DescribeSurfaceResult.Fail(ex.Code);
        }

        if (!changed) return DescribeSurfaceResult.Ok(false);   // idempotentno, bez audita

        if (!await exposures.SaveAsync(exposure, expectedVersion, ct).ConfigureAwait(false))
            return DescribeSurfaceResult.Fail(ErrorCodes.Conflict);

        var delivered = await LifecycleSupport.TryRecordAsync(audit,
            ExposureAudit.SurfaceWasDescribed(now, actor.ActorId, command.CorrelationId,
                exposure.Id, exposure.AssetId, criticality), ct).ConfigureAwait(false);

        // Promena izloženosti Critical/High asset-a ima i poseban događaj (spec R1-18-AUDIT).
        if (ExposureAudit.IsMandatory(criticality))
        {
            delivered &= await LifecycleSupport.TryRecordAsync(audit,
                ExposureAudit.CriticalExposureWasChanged(now, actor.ActorId, command.CorrelationId,
                    exposure.Id, exposure.AssetId, "surface", hadSurface ? "described" : "none", "described"), ct)
                .ConfigureAwait(false);
        }

        return DescribeSurfaceResult.Ok(true, delivered);
    }
}