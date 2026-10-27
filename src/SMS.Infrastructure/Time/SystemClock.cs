using SMS.Application.Abstractions;

namespace SMS.Infrastructure.Time;

/// <summary>
/// The real clock. Registered as a singleton.
/// <para>
/// Always UTC. Npgsql rejects a non-UTC <see cref="DateTimeOffset"/> written to a
/// <c>timestamptz</c> column, so this is the only shape that works anyway — see
/// architecture section 8.
/// </para>
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
