using System.Collections.Concurrent;
using T08.Application.Audit;

namespace T08.Infrastructure.Audit;

/// <summary>
/// Stub za R1-09 dok T04 ne objavi servis (SEC-DEP-02): normalan i failure ishod, dedup po ključu iz C-AUDIT-v1,
/// bez ikakve security logike T04 (SEC-DEP-03).
/// </summary>
public sealed class InMemoryAuditSink : IAuditSink
{
    private readonly ConcurrentDictionary<string, AuditEvent> _events = new();

    /// <summary>Simulira pad T04 (503).</summary>
    public bool Unavailable { get; set; }

    public IReadOnlyCollection<AuditEvent> Events => _events.Values.OrderBy(e => e.OccurredAt).ToList();

    public Task<AuditWriteOutcome> RecordAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        if (Unavailable) return Task.FromResult(AuditWriteOutcome.Unavailable);
        _events.TryAdd(auditEvent.IdempotencyKey, auditEvent);
        return Task.FromResult(AuditWriteOutcome.Accepted);
    }
}

/// <summary>
/// Producer ne sme da izgubi događaj bez traga (C-AUDIT-v1): kad T04 nije dostupan, događaj ostaje u redu
/// i šalje se pri sledećem <see cref="FlushAsync"/>. Red je u memoriji, pa se restart procesa računa kao
/// poznato ograničenje (trajni outbox je van obima R1-18-AUDIT).
/// </summary>
public sealed class BufferedAuditSink(IAuditSink inner) : IAuditSink
{
    private readonly object _gate = new();
    private readonly Queue<AuditEvent> _pending = new();

    public int Pending
    {
        get { lock (_gate) return _pending.Count; }
    }

    public async Task<AuditWriteOutcome> RecordAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        await FlushAsync(ct).ConfigureAwait(false);   // redosled: stariji događaji prvi

        var outcome = await inner.RecordAsync(auditEvent, ct).ConfigureAwait(false);
        if (outcome == AuditWriteOutcome.Unavailable)
        {
            lock (_gate) _pending.Enqueue(auditEvent);
        }

        return outcome;
    }

    /// <summary>Pokušava da isprazni red; staje na prvom nedostupnom ishodu. Odbijeni (400) događaj se odbacuje iz reda.</summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        while (true)
        {
            AuditEvent next;
            lock (_gate)
            {
                if (_pending.Count == 0) return;
                next = _pending.Peek();
            }

            if (await inner.RecordAsync(next, ct).ConfigureAwait(false) == AuditWriteOutcome.Unavailable) return;

            lock (_gate)
            {
                if (_pending.Count > 0 && ReferenceEquals(_pending.Peek(), next)) _pending.Dequeue();
            }
        }
    }
}
