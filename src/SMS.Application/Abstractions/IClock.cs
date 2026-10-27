namespace SMS.Application.Abstractions;

/// <summary>
/// Time, injected rather than read from DateTimeOffset.UtcNow directly, so that audit
/// stamping and licence expiry are testable.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
