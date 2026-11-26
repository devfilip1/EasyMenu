namespace EasyMenu.Application.Abstractions;

/// <summary>
/// Ends a user's open real-time sessions (research R2, FR-034). HTTP requests are already cut by the security
/// stamp check; this closes the connections that were authenticated before the deactivation.
/// </summary>
public interface ISessionTerminator
{
    void TerminateSessions(Guid userId);
}
