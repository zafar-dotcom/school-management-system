namespace SMS.Application.Abstractions;

/// <summary>
/// Runs work spanning both schemas inside one database transaction.
/// <para>
/// Implemented over a single shared connection, so a failure part-way cannot leave a login
/// in <c>master</c> with no matching record in the school schema —
/// Master_Schema_Design section 6.
/// </para>
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);

    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}
