using Scutum.T03.Application;
using Scutum.T03.Domain;

namespace Scutum.T03.Infrastructure;

public sealed class InMemoryStore(IEnumerable<ProtectedResource> seed) : IResourceStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ProtectedResource> _resources = seed.ToDictionary(x => x.Id);
    private readonly List<SecurityEvent> _audit = [];
    public IReadOnlyList<SecurityEvent> Audit { get { lock (_gate) return _audit.ToArray(); } }
    public ProtectedResource? Find(string id)
    {
        lock (_gate) return _resources.GetValueOrDefault(id);
    }
    public void Record(SecurityEvent entry) { lock (_gate) _audit.Add(entry); }
}
