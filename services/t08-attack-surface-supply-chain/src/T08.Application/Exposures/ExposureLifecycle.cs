using T08.Application.Audit;
using T08.Application.Directories;
using T08.Domain.Exposures;

namespace T08.Application.Exposures;

public sealed record RegisterExposureCommand(string AssetId, string ZoneId, string Justification, Guid CorrelationId);

/// <param name="AuditDelivered">false ako je exposure upisan, ali audit događaj nije prihvaćen ni sačuvan (isto značenje kao kod klasifikacije).</param>
public sealed record RegisterExposureResult(bool Succeeded, string? ErrorCode, string? ExposureId, bool AuditDelivered = true)
{
    public static RegisterExposureResult Ok(string exposureId, bool auditDelivered) => new(true, null, exposureId, auditDelivered);
    public static RegisterExposureResult Fail(string code) => new(false, code, null);
}

public sealed record ConfirmExposureCommand(string ExposureId, Guid CorrelationId);

/// <param name="ReasonCode">Stabilan kod razloga (ContractIdentifier), nikad slobodan tekst: ide u audit.</param>
public sealed record CloseExposureCommand(string ExposureId, string ReasonCode, Guid CorrelationId);

public sealed record LifecycleResult(bool Succeeded, string? ErrorCode, bool AuditDelivered = true)
{
    public static LifecycleResult Ok(bool auditDelivered) => new(true, null, auditDelivered);
    public static LifecycleResult Fail(string code) => new(false, code);
}

/// <summary>Zajedničko za use case-ove životnog ciklusa: audit koji ne sme da sruši već upisanu promenu i objektna provera vlasnika.</summary>
internal static class LifecycleSupport
{
    /// <summary>Matrica: „Allow (svoj)": SA (manage.any) za sve, ostali samo za svoj resurs. Uz to je potrebna i dozvola za akciju.</summary>
    public static bool MayManage(ActorContext actor, string permission, string ownerId) =>
        actor.Has(permission) && (actor.Has(Permissions.ExposureManageAny) || actor.ActorId == ownerId);

    /// <summary>Izuzetak iz audit sink-a ne sme da pretvori već upisanu promenu u grešku.</summary>
    public static async Task<bool> TryRecordAsync(IAuditSink audit, AuditEvent auditEvent, CancellationToken ct)
    {
        try
        {
            var outcome = await audit.RecordAsync(auditEvent, ct).ConfigureAwait(false);
            return outcome is AuditWriteOutcome.Accepted or AuditWriteOutcome.Buffered;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Zajednički početak potvrde i zatvaranja: format ID-a → postoji → isti scope → dozvola i vlasnik.
    /// Neispravan, nepostojeći i tuđ (van scope-a) resurs daju isti odgovor; tuđ se interno audituje.
    /// </summary>
    public static async Task<(Exposure? Exposure, string? ErrorCode)> LoadAuthorizedAsync(
        IExposureRepository exposures, IAuditSink audit, TimeProvider time, ActorContext actor,
        string exposureId, Guid correlationId, string action, string permission, CancellationToken ct)
    {
        if (!ContractIdentifier.IsValid(exposureId)) return (null, ErrorCodes.NotFound);

        var exposure = await exposures.GetAsync(exposureId, ct).ConfigureAwait(false);
        if (exposure is null) return (null, ErrorCodes.NotFound);

        if (exposure.Scope != actor.Scope)
        {
            await TryRecordAsync(audit,
                ExposureAudit.Denied(time.GetUtcNow(), actor.ActorId, correlationId, action, exposureId, ErrorCodes.NotFound), ct)
                .ConfigureAwait(false);
            return (null, ErrorCodes.NotFound);
        }

        if (!MayManage(actor, permission, exposure.OwnerId))
        {
            await TryRecordAsync(audit,
                ExposureAudit.Denied(time.GetUtcNow(), actor.ActorId, correlationId, action, exposureId, ErrorCodes.Forbidden), ct)
                .ConfigureAwait(false);
            return (null, ErrorCodes.Forbidden);
        }

        return (exposure, null);
    }
}

/// <summary>
/// Registracija exposure-a. Redosled: format ID-eva → dozvola → asset (scope, vlasnik) → zona → upis → audit.
/// Vlasnik i scope se uzimaju iz asset-a (T01), nikad iz zahteva. Početna klasifikacija je uvek Unknown.
/// </summary>
public sealed class RegisterExposureHandler(
    IExposureRepository exposures,
    IAssetReader assets,
    IZoneReader zones,
    IAuditSink audit,
    TimeProvider time,
    Func<string>? newId = null)
{
    private readonly Func<string> _newId = newId ?? (() => $"exp-{Guid.NewGuid():N}");

    public async Task<RegisterExposureResult> HandleAsync(
        ActorContext actor, RegisterExposureCommand command, CancellationToken ct = default)
    {
        // Format se proverava pre svega, da se tuđ tekst ne upiše u audit (target asset:{id}) i ne stigne do T01/T11.
        if (!ContractIdentifier.IsValid(command.AssetId) || !ContractIdentifier.IsValid(command.ZoneId))
            return RegisterExposureResult.Fail(ExposureErrorCodes.InvalidExposure);

        if (!actor.Has(Permissions.ExposureRegister))
            return await DeniedAsync(actor, command, ErrorCodes.Forbidden, ct).ConfigureAwait(false);

        var asset = await assets.GetAsync(command.AssetId, command.CorrelationId, ct).ConfigureAwait(false);
        if (asset.Outcome == DirectoryOutcome.Unavailable) return RegisterExposureResult.Fail(ErrorCodes.UpstreamUnavailable);
        if (asset.Outcome != DirectoryOutcome.Found || asset.Value!.Status != AssetStatus.Active)
            return RegisterExposureResult.Fail(ErrorCodes.AssetNotUsable);

        // Asset iz tuđeg scope-a izgleda spolja isto kao nepostojeći (nema curenja postojanja), ali se interno audituje.
        if (asset.Value.Scope != actor.Scope)
            return await DeniedAsync(actor, command, ErrorCodes.AssetNotUsable, ct).ConfigureAwait(false);

        if (!LifecycleSupport.MayManage(actor, Permissions.ExposureRegister, asset.Value.OwnerId))
            return await DeniedAsync(actor, command, ErrorCodes.Forbidden, ct).ConfigureAwait(false);

        var zone = await zones.GetAsync(command.ZoneId, command.CorrelationId, ct).ConfigureAwait(false);
        if (zone.Outcome == DirectoryOutcome.Unavailable) return RegisterExposureResult.Fail(ErrorCodes.UpstreamUnavailable);
        if (zone.Outcome != DirectoryOutcome.Found) return RegisterExposureResult.Fail(ErrorCodes.ZoneNotUsable);

        Exposure exposure;
        try
        {
            exposure = Exposure.Register(_newId(), asset.Value.Id, zone.Value!.Id, asset.Value.OwnerId, asset.Value.Scope, command.Justification);
        }
        catch (DomainRuleException ex)
        {
            return RegisterExposureResult.Fail(ex.Code);
        }

        if (!await exposures.AddAsync(exposure, ct).ConfigureAwait(false))
            return RegisterExposureResult.Fail(ErrorCodes.Conflict);

        var now = time.GetUtcNow();
        var criticality = asset.Value.Criticality;
        var delivered = await LifecycleSupport.TryRecordAsync(audit,
            ExposureAudit.ExposureRegistered(now, actor.ActorId, command.CorrelationId,
                exposure.Id, exposure.AssetId, exposure.ZoneId, criticality), ct).ConfigureAwait(false);

        if (ExposureAudit.IsMandatory(criticality))
        {
            delivered &= await LifecycleSupport.TryRecordAsync(audit,
                ExposureAudit.CriticalExposureWasChanged(now, actor.ActorId, command.CorrelationId,
                    exposure.Id, exposure.AssetId, "registered", "none", exposure.Classification.ToString()), ct)
                .ConfigureAwait(false);
        }

        return RegisterExposureResult.Ok(exposure.Id, delivered);
    }

    private async Task<RegisterExposureResult> DeniedAsync(
        ActorContext actor, RegisterExposureCommand command, string code, CancellationToken ct)
    {
        await LifecycleSupport.TryRecordAsync(audit,
            ExposureAudit.RegistrationDenied(time.GetUtcNow(), actor.ActorId, command.CorrelationId, command.AssetId, code), ct)
            .ConfigureAwait(false);
        return RegisterExposureResult.Fail(code);
    }
}

/// <summary>
/// Potvrda da je izloženost i dalje potrebna. Traži upotrebljiv asset: potvrđivati izloženost dekomisionisanog
/// asset-a nema smisla (taj exposure se zatvara). Potvrda ne menja izloženost, pa nema događaja o kritičnoj promeni.
/// </summary>
public sealed class ConfirmExposureHandler(
    IExposureRepository exposures,
    IAssetReader assets,
    IAuditSink audit,
    TimeProvider time)
{
    private const string Action = "exposure.confirm";

    public async Task<LifecycleResult> HandleAsync(
        ActorContext actor, ConfirmExposureCommand command, CancellationToken ct = default)
    {
        var (exposure, error) = await LifecycleSupport.LoadAuthorizedAsync(
            exposures, audit, time, actor, command.ExposureId, command.CorrelationId, Action, Permissions.ExposureConfirm, ct)
            .ConfigureAwait(false);
        if (exposure is null) return LifecycleResult.Fail(error!);

        if (exposure.Status == ExposureStatus.Closed) return LifecycleResult.Fail(ExposureErrorCodes.ExposureClosed);

        var asset = await assets.GetAsync(exposure.AssetId, command.CorrelationId, ct).ConfigureAwait(false);
        if (asset.Outcome == DirectoryOutcome.Unavailable) return LifecycleResult.Fail(ErrorCodes.UpstreamUnavailable);
        if (asset.Outcome != DirectoryOutcome.Found) return LifecycleResult.Fail(ErrorCodes.AssetNotUsable);
        if (asset.Value!.Scope != exposure.Scope) return LifecycleResult.Fail(ErrorCodes.AssetScopeMismatch);

        var now = time.GetUtcNow();
        var expectedVersion = exposure.Version;
        try
        {
            exposure.Confirm(now);
        }
        catch (DomainRuleException ex)
        {
            return LifecycleResult.Fail(ex.Code);
        }

        if (!await exposures.SaveAsync(exposure, expectedVersion, ct).ConfigureAwait(false))
            return LifecycleResult.Fail(ErrorCodes.Conflict);

        var delivered = await LifecycleSupport.TryRecordAsync(audit,
            ExposureAudit.ExposureConfirmed(now, actor.ActorId, command.CorrelationId,
                exposure.Id, exposure.AssetId, asset.Value.Criticality), ct).ConfigureAwait(false);

        return LifecycleResult.Ok(delivered);
    }
}

/// <summary>
/// Zatvaranje exposure-a (terminalno). Za razliku od klasifikacije, dekomisionisan ili nepostojeći asset NE blokira
/// zatvaranje (to je glavni razlog za zatvaranje); tada kritičnost nije poznata pa se audit vodi kao Critical (obavezan, High).
/// Ako se asset ne može pročitati zbog nedostupnosti T01, ne menjamo ništa (fail closed).
/// </summary>
public sealed class CloseExposureHandler(
    IExposureRepository exposures,
    IAssetReader assets,
    IAuditSink audit,
    TimeProvider time)
{
    private const string Action = "exposure.close";

    public async Task<LifecycleResult> HandleAsync(
        ActorContext actor, CloseExposureCommand command, CancellationToken ct = default)
    {
        var (exposure, error) = await LifecycleSupport.LoadAuthorizedAsync(
            exposures, audit, time, actor, command.ExposureId, command.CorrelationId, Action, Permissions.ExposureClose, ct)
            .ConfigureAwait(false);
        if (exposure is null) return LifecycleResult.Fail(error!);

        // Razlog je stabilan kod, ne slobodan tekst: ide u audit i ne sme da nosi tuđ sadržaj.
        if (!ContractIdentifier.IsValid(command.ReasonCode)) return LifecycleResult.Fail(ExposureErrorCodes.InvalidExposure);

        if (exposure.Status == ExposureStatus.Closed) return LifecycleResult.Fail(ExposureErrorCodes.ExposureClosed);

        var asset = await assets.GetAsync(exposure.AssetId, command.CorrelationId, ct).ConfigureAwait(false);
        if (asset.Outcome == DirectoryOutcome.Unavailable) return LifecycleResult.Fail(ErrorCodes.UpstreamUnavailable);

        var criticality = AssetCriticality.Critical;   // konzervativno kad asset više nije upotrebljiv
        if (asset.Outcome == DirectoryOutcome.Found)
        {
            if (asset.Value!.Scope != exposure.Scope) return LifecycleResult.Fail(ErrorCodes.AssetScopeMismatch);
            criticality = asset.Value.Criticality;
        }

        var now = time.GetUtcNow();
        var expectedVersion = exposure.Version;
        try
        {
            exposure.Close(command.ReasonCode, now);
        }
        catch (DomainRuleException ex)
        {
            return LifecycleResult.Fail(ex.Code);
        }

        if (!await exposures.SaveAsync(exposure, expectedVersion, ct).ConfigureAwait(false))
            return LifecycleResult.Fail(ErrorCodes.Conflict);

        var delivered = await LifecycleSupport.TryRecordAsync(audit,
            ExposureAudit.ExposureClosed(now, actor.ActorId, command.CorrelationId,
                exposure.Id, exposure.AssetId, command.ReasonCode, criticality), ct).ConfigureAwait(false);

        if (ExposureAudit.IsMandatory(criticality))
        {
            delivered &= await LifecycleSupport.TryRecordAsync(audit,
                ExposureAudit.CriticalExposureWasChanged(now, actor.ActorId, command.CorrelationId,
                    exposure.Id, exposure.AssetId, "status", nameof(ExposureStatus.Active), nameof(ExposureStatus.Closed)), ct)
                .ConfigureAwait(false);
        }

        return LifecycleResult.Ok(delivered);
    }
}
