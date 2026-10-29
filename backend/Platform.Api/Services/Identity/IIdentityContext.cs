namespace Platform.Api.Services.Identity;

public sealed record CallerIdentity(Guid ActorId, bool IsAuthenticated);

public interface IIdentityContext
{
    /// null = niko nije prijavljen
    CallerIdentity? Current { get; }
}