using SMS.Application.Abstractions;

namespace SMS.Infrastructure.Persistence.Stores;

/// <summary>
/// <see cref="IUnitOfWork"/> over <see cref="CrossSchemaTransaction"/>, so the application
/// layer can span both schemas without knowing that EF Core or Npgsql exist.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CrossSchemaTransaction _transaction;

    public UnitOfWork(CrossSchemaTransaction transaction) => _transaction = transaction;

    public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct = default) =>
        _transaction.ExecuteAsync(work, ct);

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default) =>
        _transaction.ExecuteAsync(work, ct);
}
