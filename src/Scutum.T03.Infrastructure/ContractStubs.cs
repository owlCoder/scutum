using Scutum.T03.Application;
using Scutum.T03.Domain;

namespace Scutum.T03.Infrastructure;

// Nema izracunavanja RBAC/klasifikacije/baseline-a u stubovima.
// Dictionary sadrzi prethodno dogovorene izlazne fixture vrednosti.
public sealed class PermissionsStub(IReadOnlyDictionary<string, EffectivePermissions> replies)
    : IEffectivePermissionsProvider
{
    public bool IsUnavailable { get; set; }
    public PortResult<EffectivePermissions> GetFor(string subjectId) => IsUnavailable
        ? PortResult<EffectivePermissions>.Offline()
        : replies.TryGetValue(subjectId, out var value)
            ? PortResult<EffectivePermissions>.Ok(value) : PortResult<EffectivePermissions>.Missing();
}
public sealed class AssetsStub(IReadOnlyDictionary<string, AssetSnapshot> replies) : IAssetCatalog
{
    public bool IsUnavailable { get; set; }
    public PortResult<AssetSnapshot> Find(string id) => IsUnavailable
        ? PortResult<AssetSnapshot>.Offline()
        : replies.TryGetValue(id, out var value) ? PortResult<AssetSnapshot>.Ok(value) : PortResult<AssetSnapshot>.Missing();
}
public sealed class SubjectsStub(IReadOnlyDictionary<string, Subject> replies) : ISubjectDirectory
{
    public bool IsUnavailable { get; set; }
    public PortResult<Subject> Find(string id) => IsUnavailable
        ? PortResult<Subject>.Offline()
        : replies.TryGetValue(id, out var value) ? PortResult<Subject>.Ok(value) : PortResult<Subject>.Missing();
}
public sealed class PolicyCatalogStub(PortResult<PolicyReference> reply) : IPolicyCatalog
{
    public PortResult<PolicyReference> GetActive(string scopeId) =>
        reply.Status == PortStatus.Ok && reply.Value?.ScopeId != scopeId
            ? PortResult<PolicyReference>.Missing() : reply;
}
public sealed class BaselineStub(PortResult<BaselineReference> reply) : IBaselineProvider
{
    public PortResult<BaselineReference> GetFor(string targetType) =>
        reply.Status == PortStatus.Ok && reply.Value?.TargetType != targetType
            ? PortResult<BaselineReference>.Missing() : reply;
}
public sealed class ClassificationStub(PortResult<ClassificationSnapshot> reply) : IClassificationProvider
{
    public PortResult<ClassificationSnapshot> GetFor(string resourceId) =>
        reply.Status == PortStatus.Ok && reply.Value?.ResourceId != resourceId
            ? PortResult<ClassificationSnapshot>.Missing() : reply;
}
public sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
