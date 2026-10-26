using System.Text.RegularExpressions;
using Scutum.T03.Domain;

namespace Scutum.T03.Application;

// Kontekst daje pouzdani ulazni adapter (u ovoj fazi composition root simulatora).
// Handler se poziva i direktno iz negativnih testova, bez UI provera.
public sealed class ResourceHandlers(Subject subject, IEffectivePermissionsProvider permissions,
    IAssetCatalog assets, IResourceStore store, ResourceAccessPolicy policy, IClock clock)
{
    private static readonly Regex IdPattern = new("\\A[a-z][a-z0-9-]{0,63}\\z", RegexOptions.CultureInvariant);

    public OperationResult Read(ReadResourceQuery? query)
    {
        if (query is null)
            return Fail(OperationOutcome.Invalid, "INVALID_INPUT", "Ulaz nije validan.",
                "INVALID_INPUT", "invalid-target", Guid.Empty);

        var id = query.ResourceId;
        var correlationId = query.CorrelationId;
        if (!ValidId(id) || correlationId == Guid.Empty)
            return Fail(OperationOutcome.Invalid, "INVALID_INPUT", "Ulaz nije validan.",
                "INVALID_INPUT", ValidId(id) ? id : "invalid-target", correlationId);

        if (!subject.IsAuthenticated || subject.Assurance == AssuranceLevel.None)
            return Fail(OperationOutcome.Unauthenticated, "NOT_AUTHENTICATED", "Prijava je potrebna.",
                "NOT_AUTHENTICATED", id, correlationId);
        if (!subject.IsActive) return Deny("INACTIVE_SUBJECT", id, correlationId);

        var permissionReply = permissions.GetFor(subject.Id);
        if (permissionReply.Status == PortStatus.Unavailable)
            return Deny("PERMISSIONS_UNAVAILABLE", id, correlationId);
        if (permissionReply.Status != PortStatus.Ok || permissionReply.Value is null)
            return Deny("PERMISSIONS_NOT_FOUND", id, correlationId);

        var resource = store.Find(id);
        if (resource is null) return Deny("RESOURCE_NOT_FOUND", id, correlationId);
        var assetReply = assets.Find(resource.AssetId);
        if (assetReply.Status == PortStatus.Unavailable)
            return Deny("ASSET_UNAVAILABLE", id, correlationId);
        if (assetReply.Status != PortStatus.Ok || assetReply.Value is null)
            return Deny("ASSET_NOT_FOUND", id, correlationId);

        var decision = policy.Decide(subject, ResourceAction.Read, resource, assetReply.Value, permissionReply.Value);
        if (!decision.Allowed) return Deny(decision.Code, id, correlationId);

        store.Record(Event(id, correlationId, "ALLOW", decision.Code));
        return new(OperationOutcome.Granted, "GRANTED", "Operacija je dozvoljena.",
            new(resource.Id, resource.OwnerId, resource.Content, resource.Version));
    }

    private static bool ValidId(string? id) => id is not null && IdPattern.IsMatch(id);
    // Interni razlog ostaje u auditu; javni deny ne otkriva postojanje resursa ni pri outage-u.
    private OperationResult Deny(string reason, string id, Guid correlation) =>
        Fail(OperationOutcome.Denied, "RESOURCE_UNAVAILABLE", "Resurs nije dostupan.", reason, id, correlation);
    private OperationResult Fail(OperationOutcome outcome, string code, string message,
        string reason, string id, Guid correlation)
    {
        store.Record(Event(id, correlation, outcome.ToString().ToUpperInvariant(), reason));
        return new(outcome, code, message);
    }
    private SecurityEvent Event(string id, Guid correlation, string outcome, string reason) =>
        new(1, clock.UtcNow, subject.Id, ResourceAction.Read.ToString(), id, outcome, correlation,
            "Confidential", reason, ResourceAccessPolicy.Version, subject.ScopeId);
}
