using SMS.Domain.Common;

namespace SMS.Domain.Master;

/// <summary>
/// Exactly one row, written at setup. Identifies this installation and detects clock tampering.
/// </summary>
public class Installation
{
    public Guid Id { get; set; }

    /// <summary>
    /// Shown to the customer and quoted on their licence. A licence is issued against this
    /// key, so a licence file copied to another installation will not validate.
    /// </summary>
    public Guid InstallationKey { get; set; }

    public DateTimeOffset InstalledAt { get; set; }
    public string AppVersion { get; set; } = null!;

    /// <summary>
    /// The latest moment this installation has ever observed, moved forward on startup and
    /// periodically. If the system clock reads earlier than this, it has been set back and
    /// the licence is treated as invalid.
    /// </summary>
    public DateTimeOffset ClockHighWater { get; set; }

    public string? MachineName { get; set; }
}
