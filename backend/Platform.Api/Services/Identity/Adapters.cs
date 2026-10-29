using System.Collections.Concurrent;
using Platform.Api.Domain.Identity;

namespace Platform.Api.Services.Identity;

/// Privremeno cuvanje u memoriji dok se ne doda PostgreSQL.
public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<Guid, UserAccount> _items = new();
    public UserAccount? Find(Guid id) => _items.GetValueOrDefault(id);
    public void Save(UserAccount account) => _items[account.Id] = account;
}

/// Stub dok IDN-02 ne napravi pravi opoziv sesija.
public sealed class NoOpSessionRevoker : ISessionRevoker
{
    public void RevokeAllForUser(Guid userId) { }
}

public sealed class LoggerAuditWriter(ILogger<LoggerAuditWriter> logger) : IAuditWriter
{
    public void Write(AuditEvent e) =>
        logger.LogInformation(
            "AUDIT {Time} actor={Actor} action={Action} target={Target} outcome={Outcome} corr={Corr} detail={Detail}",
            e.Time, e.ActorId, e.Action, e.Target, e.Outcome, e.CorrelationId, e.Detail);
}

public interface IAdminDirectory
{
    bool IsAdministrator(Guid userId);
}

/// Samo administrator sme da menja status. Sve ostalo je zabranjeno.
public sealed class AdminOnlyAccessDecision(IAdminDirectory admins) : IAccessDecision
{
    public bool CanChangeAccountStatus(CallerIdentity caller) =>
        caller.IsAuthenticated && admins.IsAdministrator(caller.ActorId);
}

/// DEV STUB do IDN-01/IDN-02: pozivalac se cita iz headera X-Stub-User.
/// Registruje se SAMO u Development okruzenju.
public sealed class StubIdentityContext(IHttpContextAccessor http) : IIdentityContext
{
    public CallerIdentity? Current
    {
        get
        {
            var raw = http.HttpContext?.Request.Headers["X-Stub-User"].FirstOrDefault();
            return Guid.TryParse(raw, out var id) ? new CallerIdentity(id, true) : null;
        }
    }
}

/// Izvan Developmenta nema pozivaoca, pa je svaki zahtev 401.
public sealed class AnonymousIdentityContext : IIdentityContext
{
    public CallerIdentity? Current => null;
}

/// DEV STUB: id-jevi administratora iz konfiguracije (Stub:AdminIds, odvojeni zarezom).
public sealed class ConfigAdminDirectory(IConfiguration config) : IAdminDirectory
{
    private readonly HashSet<Guid> _ids = (config["Stub:AdminIds"] ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
        .Where(g => g != Guid.Empty)
        .ToHashSet();

    public bool IsAdministrator(Guid userId) => _ids.Contains(userId);
}