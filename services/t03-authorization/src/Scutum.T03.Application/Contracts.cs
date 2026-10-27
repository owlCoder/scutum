using Scutum.T03.Domain;

namespace Scutum.T03.Application;

public enum PortStatus { Ok, NotFound, Unavailable }
public sealed record PortResult<T>(PortStatus Status, T? Value) where T : class
{
    public static PortResult<T> Ok(T value) => new(PortStatus.Ok, value);
    public static PortResult<T> Missing() => new(PortStatus.NotFound, null);
    public static PortResult<T> Offline() => new(PortStatus.Unavailable, null);
}

// Cross-team portovi: stub samo reprodukuje ugovorene odgovore.
public interface IEffectivePermissionsProvider
{
    PortResult<EffectivePermissions> GetFor(string subjectId);
}
public interface IAssetCatalog
{
    PortResult<AssetSnapshot> Find(string assetId);
}
public interface ISubjectDirectory
{
    PortResult<Subject> Find(string subjectId);
}
public interface IClock { DateTimeOffset UtcNow { get; } }

public sealed record SecurityEvent(int SchemaVersion, DateTimeOffset Timestamp,
    string ActorId, string Action, string TargetId, string Outcome, Guid CorrelationId,
    string Sensitivity, string ReasonCode, string PolicyVersion, string? ScopeId);

public interface IResourceStore
{
    ProtectedResource? Find(string id);
    void Record(SecurityEvent entry);
}

public enum OperationOutcome { Granted, Denied, Invalid, Unauthenticated, Conflict, Unavailable }
public sealed record ResourceView(string Id, string OwnerId, string? Content, int Version);
public sealed record OperationResult(OperationOutcome Outcome, string Code,
    string Message, ResourceView? Resource = null);
public sealed record ReadResourceQuery(string ResourceId, Guid CorrelationId);

// DRAFT DTO-i i portovi za buduce celine (R1-08, R1-10, R1-07); bez implementacija.
public sealed record PolicyReference(string PolicyId, int Version, string ScopeId,
    string Status, DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil);
public sealed record BaselineReference(string BaselineId, int Version,
    string TargetType, string PolicyId, int PolicyVersion);
public sealed record ClassificationSnapshot(string ResourceId, string Level,
    string DataOwnerId, int Version);
public interface IPolicyCatalog { PortResult<PolicyReference> GetActive(string scopeId); }
public interface IBaselineProvider { PortResult<BaselineReference> GetFor(string targetType); }
public interface IClassificationProvider { PortResult<ClassificationSnapshot> GetFor(string resourceId); }
