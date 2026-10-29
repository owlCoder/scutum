namespace Platform.Api.Services.Identity;

public interface ISessionRevoker
{
    void RevokeAllForUser(Guid userId);
}