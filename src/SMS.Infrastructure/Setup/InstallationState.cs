namespace SMS.Infrastructure.Setup;

public enum InstallationStatus
{
    /// <summary>The check has not run yet.</summary>
    Unknown,

    /// <summary>Schemas are current but no school row exists. The setup wizard must run.</summary>
    NotInstalled,

    /// <summary>Normal operation.</summary>
    Ready,

    /// <summary>The database schema does not match this build. Refuse to serve.</summary>
    SchemaMismatch,

    /// <summary>The database could not be reached or read at startup.</summary>
    Unavailable
}

/// <summary>
/// The outcome of the startup schema check, held as a singleton and consulted by the
/// gate middleware on every request.
/// <para>
/// Architecture section 4.8: the application refuses to serve a schema it does not match,
/// rather than querying it anyway. On-premise there is no DBA watching, so running new code
/// against an un-migrated database is how data gets corrupted quietly.
/// </para>
/// </summary>
public sealed class InstallationState
{
    public InstallationStatus Status { get; private set; } = InstallationStatus.Unknown;

    /// <summary>Operator-facing explanation. Shown on the maintenance page and logged.</summary>
    public string? Detail { get; private set; }

    public bool IsReady => Status == InstallationStatus.Ready;

    public void Set(InstallationStatus status, string? detail = null)
    {
        Status = status;
        Detail = detail;
    }
}
