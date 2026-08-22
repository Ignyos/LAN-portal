namespace Ignyos.LanPortal.Web.Services;

/// <summary>
/// Thrown when a 401 Unauthorized response is received from an authenticated context,
/// indicating the session has been revoked or has naturally expired.
/// </summary>
public sealed class SessionRevokedException : UnauthorizedAccessException
{
    /// <summary>
    /// Gets the reason for the session becoming invalid.
    /// Either "revoked" (admin revoked) or "expired" (natural token expiration).
    /// </summary>
    public string Reason { get; }

    public SessionRevokedException(string reason = "expired")
        : base("Your session is no longer valid. Please request access again.")
    {
        Reason = reason;
    }
}
