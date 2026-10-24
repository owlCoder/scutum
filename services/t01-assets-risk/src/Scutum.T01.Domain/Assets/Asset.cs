using Scutum.T01.Domain.Shared;

namespace Scutum.T01.Domain.Assets;

// R1-02 invarijante:
//  - kritičnost mora biti eksplicitno zadata (nema podrazumevane vrednosti)
//  - dekomisioniran asset ostaje u istoriji i više se ne menja (nema Delete)
public sealed class Asset
{
    private Asset(AssetId id, string name, AssetType type, DomainId domainId,
                  IdentityId ownerId, Criticality criticality, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Type = type;
        DomainId = domainId;
        OwnerId = ownerId;
        Criticality = criticality;
        Status = LifecycleStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public AssetId Id { get; }
    public string Name { get; private set; }
    public AssetType Type { get; }
    public DomainId DomainId { get; }
    public IdentityId OwnerId { get; private set; }
    public Criticality Criticality { get; private set; }
    public LifecycleStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<Asset> Register(string name, AssetType type, DomainId domainId,
                                         IdentityId ownerId, Criticality criticality, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result<Asset>.Fail(ErrorCodes.Validation, "Naziv je obavezan (1–200 karaktera).");
        if (!Enum.IsDefined(type))
            return Result<Asset>.Fail(ErrorCodes.Validation, "Tip asset-a nije validan.");
        if (!Enum.IsDefined(criticality))
            return Result<Asset>.Fail(ErrorCodes.Validation, "Kritičnost mora biti eksplicitno zadata.");
        if (domainId.Value == Guid.Empty || ownerId.Value == Guid.Empty)
            return Result<Asset>.Fail(ErrorCodes.Validation, "Domen i vlasnik su obavezni.");

        return Result<Asset>.Ok(new Asset(AssetId.New(), name.Trim(), type, domainId, ownerId, criticality, now));
    }

    // Vraća staru kritičnost, da handler može da objavi AssetCriticalityChanged i audit.
    public Result<Criticality> ChangeCriticality(Criticality next, DateTimeOffset now)
    {
        if (Status == LifecycleStatus.Decommissioned)
            return Result<Criticality>.Fail(ErrorCodes.InvalidTransition, "Dekomisioniran asset se ne menja.");
        if (!Enum.IsDefined(next))
            return Result<Criticality>.Fail(ErrorCodes.Validation, "Kritičnost nije validna.");

        var old = Criticality;
        Criticality = next;
        UpdatedAt = now;
        return Result<Criticality>.Ok(old);
    }

    // Nema Delete metode: dekomisioniran asset ostaje u istoriji.
    public Result<LifecycleStatus> Decommission(DateTimeOffset now)
    {
        if (Status == LifecycleStatus.Decommissioned)
            return Result<LifecycleStatus>.Fail(ErrorCodes.InvalidTransition, "Asset je već dekomisioniran.");

        var old = Status;
        Status = LifecycleStatus.Decommissioned;
        UpdatedAt = now;
        return Result<LifecycleStatus>.Ok(old);
    }
}
