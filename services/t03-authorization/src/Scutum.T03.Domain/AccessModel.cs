namespace Scutum.T03.Domain;

public enum ResourceAction { Read, Update, ChangeOwner }
public enum ResourceStatus { Active, Archived }
public enum AssuranceLevel { None, Password, Mfa }

// Pouzdan snapshot sesije; nije deo korisnickog komandnog DTO-a.
public sealed record Subject(string Id, string ScopeId, bool IsAuthenticated,
    bool IsActive, AssuranceLevel Assurance);
public sealed record ScopedPermission(string Permission, string ScopeId);
public sealed record EffectivePermissions(IReadOnlyList<ScopedPermission> Grants);
public sealed record AssetSnapshot(string Id, string ScopeId, bool IsActive);
public sealed record ProtectedResource(string Id, string AssetId, string OwnerId,
    string Content, int Version, ResourceStatus Status);
public sealed record AccessDecision(bool Allowed, string Code, string Reason);

public static class Permissions
{
    public const string ReadOwn = "resources:read:own";
    public const string ReadAny = "resources:read:any";
    public const string UpdateOwn = "resources:update:own";
    public const string ChangeOwner = "resources:owner:change";
}

public sealed class ResourceAccessPolicy
{
    public const string Version = "r1-06:v1";

    public AccessDecision Decide(Subject subject, ResourceAction action,
        ProtectedResource resource, AssetSnapshot asset, EffectivePermissions permissions)
    {
        if (!subject.IsAuthenticated || subject.Assurance == AssuranceLevel.None)
            return Deny("NOT_AUTHENTICATED", "Identitet nije potvrđen.");
        if (!subject.IsActive) return Deny("INACTIVE_SUBJECT", "Nalog nije aktivan.");
        if (asset.Id != resource.AssetId || !asset.IsActive || resource.Status != ResourceStatus.Active)
            return Deny("INACTIVE_RESOURCE", "Resurs ili asset nije aktivan.");
        if (subject.ScopeId != asset.ScopeId)
            return Deny("SCOPE_MISMATCH", "Resurs je izvan opsega subjekta.");

        bool Has(string permission) => permissions.Grants.Any(g =>
            g.Permission == permission && g.ScopeId == asset.ScopeId);
        bool owner = resource.OwnerId == subject.Id;
        return action switch
        {
            ResourceAction.Read when Has(Permissions.ReadOwn) && owner =>
                Allow("OWNER_ACCESS", "Vlasnik ima dozvolu čitanja u scope-u."),
            ResourceAction.Read when Has(Permissions.ReadAny) =>
                Allow("SCOPED_READ_ANY", "Eksplicitna dozvola čitanja svih resursa u scope-u."),
            ResourceAction.Update when Has(Permissions.UpdateOwn) && owner =>
                Allow("OWNER_UPDATE", "Vlasnik ima dozvolu izmene u scope-u."),
            ResourceAction.ChangeOwner when Has(Permissions.ChangeOwner) && owner =>
                Allow("OWNER_TRANSFER", "Trenutni vlasnik ima dozvolu prenosa vlasništva."),
            _ => Deny("NO_APPLICABLE_GRANT", "Nema odgovarajuće dozvole ili vlasništva.")
        };
    }

    private static AccessDecision Allow(string code, string reason) => new(true, code, reason);
    private static AccessDecision Deny(string code, string reason) => new(false, code, reason);
}
