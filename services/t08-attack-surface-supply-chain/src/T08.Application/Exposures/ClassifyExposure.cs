using T08.Application.Audit;
using T08.Application.Directories;
using T08.Domain.Exposures;

namespace T08.Application.Exposures;

/// <summary>Serverski kontekst poziva (SEC-CONV-02). Nikad se ne popunjava iz tela zahteva.</summary>
public sealed record ActorContext(string ActorId, string Scope, IReadOnlySet<string> Permissions)
{
    public bool Has(string permission) => Permissions.Contains(permission);
}

public static class Permissions
{
    /// <summary>Security matrica: Allow za SA i AN; Deny za RO, AU i X.</summary>
    public const string ExposureClassify = "exposure.classify";
    /// <summary>Security matrica: Registrovati exposure: Allow za SA i RO (svoj).</summary>
    public const string ExposureRegister = "exposure.register";
    /// <summary>Security matrica: Potvrditi exposure: Allow za SA i RO (svoj).</summary>
    public const string ExposureConfirm = "exposure.confirm";
    /// <summary>Security matrica: Zatvoriti exposure: Allow za SA i RO (svoj).</summary>
    public const string ExposureClose = "exposure.close";
    /// <summary>Razlikuje SA od RO: bez nje se registracija/potvrda/zatvaranje dozvoljavaju samo vlasniku (objektna provera).</summary>
    public const string ExposureManageAny = "exposure.manage.any";
}

public static class ErrorCodes
{
    public const string NotFound = "NOT_FOUND";
    public const string Forbidden = "FORBIDDEN";
    public const string UpstreamUnavailable = "UPSTREAM_UNAVAILABLE";
    public const string AssetNotUsable = "ASSET_NOT_USABLE";
    public const string Conflict = "CONFLICT";
    /// <summary>Asset je u međuvremenu promenio scope (ili je exposure upisan sa pogrešnim): ne odlučujemo nad nepoklopljenim scope-ovima.</summary>
    public const string AssetScopeMismatch = "ASSET_SCOPE_MISMATCH";
    /// <summary>Zona ne postoji ili je neaktivna: nova veza se odbija (registracija).</summary>
    public const string ZoneNotUsable = "ZONE_NOT_USABLE";
}

/// <summary>Repozitorijum agregata. Vraća kopije; <see cref="SaveAsync"/> čuva samo ako verzija odgovara (SEC-CONV-09).</summary>
public interface IExposureRepository
{
    Task<Exposure?> GetAsync(string id, CancellationToken ct = default);

    /// <returns>false ako je neko u međuvremenu izmenio exposure (verzija se ne poklapa).</returns>
    Task<bool> SaveAsync(Exposure exposure, int expectedVersion, CancellationToken ct = default);

    /// <summary>Prvi upis (registracija).</summary>
    /// <returns>false ako exposure sa tim ID-jem već postoji.</returns>
    Task<bool> AddAsync(Exposure exposure, CancellationToken ct = default);
}

public sealed record ClassifyExposureCommand(
    string ExposureId, Classification Classification, DateTimeOffset? ExpiresAt, Guid CorrelationId);

/// <param name="AuditDelivered">false ako je promena upisana, ali audit događaj nije prihvaćen (T04 odbio/nedostupan ili sink pao); pozivalac to mora da vidi.</param>
public sealed record ClassifyExposureResult(bool Succeeded, string? ErrorCode, bool Changed, Classification? Classification, bool AuditDelivered = true)
{
    public static ClassifyExposureResult Ok(bool changed, Classification classification, bool auditDelivered = true) =>
        new(true, null, changed, classification, auditDelivered);
    public static ClassifyExposureResult Fail(string code) => new(false, code, false, null);
}

/// <summary>
/// R1-18-UC4: klasifikacija exposure-a (Approved / Temporary / Unknown).
/// Redosled je namerno takav: scope → dozvola → asset (kritičnost) → domenska pravila → upis → audit.
/// </summary>
public sealed class ClassifyExposureHandler(
    IExposureRepository exposures,
    IAssetReader assets,
    IAuditSink audit,
    TimeProvider time)
{
    private const string Action = "exposure.classify";

    public async Task<ClassifyExposureResult> HandleAsync(
        ActorContext actor, ClassifyExposureCommand command, CancellationToken ct = default)
    {
        // Neispravan ID ne dolazi do baze ni do audita; isti odgovor kao nepostojeći resurs.
        if (!ContractIdentifier.IsValid(command.ExposureId)) return ClassifyExposureResult.Fail(ErrorCodes.NotFound);

        var exposure = await exposures.GetAsync(command.ExposureId, ct).ConfigureAwait(false);

        // Nepostojeći resurs se ne audituje: ID koji ne postoji može da generiše svako, pa bi audit (i red u memoriji) bio zatrpan.
        if (exposure is null) return ClassifyExposureResult.Fail(ErrorCodes.NotFound);

        // Van scope-a je spolja isti odgovor kao nepostojeći (nema curenja postojanja), ali se interno audituje.
        if (exposure.Scope != actor.Scope)
            return await DeniedAsync(actor, command, ErrorCodes.NotFound, ct).ConfigureAwait(false);

        if (!actor.Has(Permissions.ExposureClassify))
            return await DeniedAsync(actor, command, ErrorCodes.Forbidden, ct).ConfigureAwait(false);

        // Zatvoren exposure je terminalan: odbija se pre poziva ka T01, da kod ne zavisi od stanja asset-a (npr. već dekomisionisan).
        if (exposure.Status == ExposureStatus.Closed) return ClassifyExposureResult.Fail(ExposureErrorCodes.ExposureClosed);

        // Kritičnost se čita pri svakoj odluci (referenca, ne kopija). Ako se ne može pročitati, ne menjamo ništa.
        var asset = await assets.GetAsync(exposure.AssetId, command.CorrelationId, ct).ConfigureAwait(false);
        if (asset.Outcome == DirectoryOutcome.Unavailable) return ClassifyExposureResult.Fail(ErrorCodes.UpstreamUnavailable);
        if (asset.Outcome != DirectoryOutcome.Found) return ClassifyExposureResult.Fail(ErrorCodes.AssetNotUsable);

        // Autorizacija ide po scope-u upisanom u exposure; ako je asset u međuvremenu prešao u drugi scope, ne odlučujemo.
        if (asset.Value!.Scope != exposure.Scope) return ClassifyExposureResult.Fail(ErrorCodes.AssetScopeMismatch);
        var criticality = asset.Value.Criticality;

        var now = time.GetUtcNow();
        var before = exposure.Classification;
        var expectedVersion = exposure.Version;

        bool changed;
        try
        {
            changed = exposure.Classify(command.Classification, command.ExpiresAt, now);
        }
        catch (DomainRuleException ex)
        {
            return ClassifyExposureResult.Fail(ex.Code);
        }

        if (!changed) return ClassifyExposureResult.Ok(false, exposure.Classification);   // idempotentno, bez audita

        if (!await exposures.SaveAsync(exposure, expectedVersion, ct).ConfigureAwait(false))
            return ClassifyExposureResult.Fail(ErrorCodes.Conflict);

        // Za Critical/High je audit obavezan. Stanje je već upisano, pa neuspeh audita ne poništava promenu,
        // ali se vraća kao AuditDelivered=false (pozivalac/operater mora da reaguje). BufferedAuditSink čuva događaj dok T04 ne radi.
        var delivered = await TryRecordAsync(
            ExposureAudit.ClassificationWasChanged(now, actor.ActorId, command.CorrelationId,
                exposure.Id, exposure.AssetId, before.ToString(), exposure.Classification.ToString(), criticality,
                exposure.ExpiresAt), ct).ConfigureAwait(false);

        // Promena izloženosti Critical/High asset-a ima i poseban događaj (spec R1-18-AUDIT), da se kritične promene filtriraju bez čitanja details.
        if (ExposureAudit.IsMandatory(criticality))
        {
            var criticalDelivered = await TryRecordAsync(
                ExposureAudit.CriticalExposureWasChanged(now, actor.ActorId, command.CorrelationId,
                    exposure.Id, exposure.AssetId, "classification", before.ToString(), exposure.Classification.ToString()), ct)
                .ConfigureAwait(false);
            delivered &= criticalDelivered;
        }

        return ClassifyExposureResult.Ok(true, exposure.Classification, delivered);
    }

    private async Task<ClassifyExposureResult> DeniedAsync(
        ActorContext actor, ClassifyExposureCommand command, string code, CancellationToken ct)
    {
        await TryRecordAsync(
            ExposureAudit.Denied(time.GetUtcNow(), actor.ActorId, command.CorrelationId, Action, command.ExposureId, code), ct)
            .ConfigureAwait(false);
        return ClassifyExposureResult.Fail(code);
    }

    /// <summary>Izuzetak iz audit sink-a ne sme da pretvori već upisanu promenu u grešku niti da otkrije razliku u odgovoru.</summary>
    private async Task<bool> TryRecordAsync(AuditEvent auditEvent, CancellationToken ct)
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
}
