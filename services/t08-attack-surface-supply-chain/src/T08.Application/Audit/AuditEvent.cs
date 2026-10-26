using System.Text;

namespace T08.Application.Audit;

public enum AuditOutcome { Success, Failure }

public enum AuditSensitivity { Low, Medium, High }

/// <summary>
/// Audit događaj sa poljima iz SEC-CONV-04 / C-AUDIT-v1. Pravi se samo preko <see cref="Create"/>,
/// koji odbija prazna polja, prevelik <c>details</c> i ključeve koji liče na tajne (SEC-CONV-05).
/// </summary>
public sealed record AuditEvent
{
    public const int MaxDetailsBytes = 2048;

    private static readonly string[] ForbiddenKeyParts = ["password", "secret", "token", "authorization", "credential", "apikey"];

    // Vrednosti koje liče na tajnu (SEC-CONV-05): JWT, Bearer zaglavlje, privatni ključ ili "password=..." u slobodnom tekstu.
    private static readonly System.Text.RegularExpressions.Regex SecretLikeValue = new(
        @"eyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*"
        + @"|\bBearer\s+\S{8,}"
        + @"|-----BEGIN [A-Z ]*PRIVATE KEY-----"
        + @"|\b(password|passwd|secret|token|api[_-]?key|authorization)\s*[=:]\s*\S+",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public DateTimeOffset OccurredAt { get; }
    public string Actor { get; }
    public string Action { get; }
    public string Target { get; }
    public AuditOutcome Outcome { get; }
    public Guid CorrelationId { get; }
    public AuditSensitivity Sensitivity { get; }
    public IReadOnlyDictionary<string, string> Details { get; }

    private AuditEvent(DateTimeOffset occurredAt, string actor, string action, string target, AuditOutcome outcome,
        Guid correlationId, AuditSensitivity sensitivity, IReadOnlyDictionary<string, string> details)
    {
        OccurredAt = occurredAt;
        Actor = actor;
        Action = action;
        Target = target;
        Outcome = outcome;
        CorrelationId = correlationId;
        Sensitivity = sensitivity;
        Details = details;
    }

    /// <summary>Ključ za idempotenciju iz C-AUDIT-v1: correlationId + action + target + occurredAt.</summary>
    public string IdempotencyKey => $"{CorrelationId:N}|{Action}|{Target}|{OccurredAt.UtcTicks}";

    public static AuditEvent Create(DateTimeOffset occurredAt, string actor, string action, string target,
        AuditOutcome outcome, Guid correlationId, AuditSensitivity sensitivity,
        IReadOnlyDictionary<string, string>? details = null)
    {
        Require(actor, nameof(actor));
        Require(action, nameof(action));
        Require(target, nameof(target));
        if (correlationId == Guid.Empty) throw new ArgumentException("correlationId je obavezan.", nameof(correlationId));
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        if (!Enum.IsDefined(sensitivity)) throw new ArgumentOutOfRangeException(nameof(sensitivity));

        var copy = new Dictionary<string, string>(details ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        var size = 0;
        foreach (var (key, value) in copy)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Prazan ključ u details.", nameof(details));
            if (ForbiddenKeyParts.Any(p => key.Contains(p, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Ključ '{key}' u details liči na tajnu.", nameof(details));
            if (value is null) throw new ArgumentException($"Vrednost za '{key}' u details je null.", nameof(details));
            if (SecretLikeValue.IsMatch(value))
                throw new ArgumentException($"Vrednost za '{key}' u details liči na tajnu.", nameof(details));
            size += Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(value);
        }

        if (size > MaxDetailsBytes) throw new ArgumentException($"details je veći od {MaxDetailsBytes} B.", nameof(details));

        return new AuditEvent(occurredAt.ToUniversalTime(), actor, action, target, outcome, correlationId, sensitivity, copy);
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} je obavezan.", name);
    }
}

public enum AuditWriteOutcome
{
    Accepted,
    /// <summary>T04 je odbio događaj (400). Ponavljanje istog događaja nema smisla.</summary>
    Rejected,
    /// <summary>T04 nije dostupan; događaj nije upisan i producer ga mora sačuvati.</summary>
    Unavailable,
    /// <summary>Samo za sink sa baferom: T04 trenutno ne prima događaj, ali ga je producer zadržao i poslaće ga kasnije.</summary>
    Buffered
}

/// <summary>Port ka R1-09 (T04), ugovor C-AUDIT-v1.</summary>
public interface IAuditSink
{
    Task<AuditWriteOutcome> RecordAsync(AuditEvent auditEvent, CancellationToken ct = default);
}
