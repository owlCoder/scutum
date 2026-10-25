using Scutum.T03.Application;
using Scutum.T03.Domain;

namespace Scutum.T03.Infrastructure;

public sealed class DemoFixture
{
    public static readonly DateTimeOffset DemoTime = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    public Subject Ana { get; } = new("ana", "dom-a", true, true, AssuranceLevel.Password);
    public Subject Marko { get; } = new("marko", "dom-a", true, true, AssuranceLevel.Password);
    public Subject Ivana { get; } = new("ivana", "dom-b", true, true, AssuranceLevel.Password);
    public Subject Reviewer { get; } = new("reviewer", "dom-a", true, true, AssuranceLevel.Password);
    public InMemoryStore Store { get; } = new([
        new("res-001", "asset-a", "ana", "Interni izveštaj A.", 1, ResourceStatus.Active),
        new("res-002", "asset-b", "ivana", "Interni izveštaj B.", 1, ResourceStatus.Active)]);
    public PermissionsStub Permissions { get; }
    public AssetsStub Assets { get; }
    public SubjectsStub Subjects { get; }

    public DemoFixture()
    {
        static EffectivePermissions Own(string scope) => new([
            new(Domain.Permissions.ReadOwn, scope)]);
        Permissions = new(new Dictionary<string, EffectivePermissions>
        {
            ["ana"] = Own("dom-a"), ["marko"] = Own("dom-a"), ["ivana"] = Own("dom-b"),
            ["reviewer"] = new([new(Domain.Permissions.ReadAny, "dom-a")])
        });
        Assets = new(new Dictionary<string, AssetSnapshot>
        {
            ["asset-a"] = new("asset-a", "dom-a", true), ["asset-b"] = new("asset-b", "dom-b", true)
        });
        Subjects = new(new Dictionary<string, Subject>
        {
            ["ana"] = Ana, ["marko"] = Marko, ["ivana"] = Ivana,
            ["inactive"] = new("inactive", "dom-a", true, false, AssuranceLevel.Password)
        });
    }
    public ResourceHandlers For(Subject subject, IResourceStore? store = null,
        IEffectivePermissionsProvider? permissions = null, IAssetCatalog? assets = null) =>
        new(subject, permissions ?? Permissions, assets ?? Assets,
            store ?? Store, new ResourceAccessPolicy(), new FixedClock(DemoTime));
}
