using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SMS.Application.Abstractions;

namespace SMS.Infrastructure.Persistence;

/// <summary>
/// Runs work that spans <c>master</c> and the school schema inside <b>one</b> database
/// transaction.
/// <para>
/// Both contexts are given the same <see cref="NpgsqlConnection"/> at registration, so one
/// ordinary transaction covers both. Without this, creating a staff member who also gets a
/// login could leave a login with no staff record when the second save fails —
/// Master_Schema_Design section 6.
/// </para>
/// <para>
/// The school context is enlisted only when a tenant has been resolved. During setup there
/// is no school yet, so there is nothing school-scoped to enlist, and asking for the context
/// would throw.
/// </para>
/// </summary>
public sealed class CrossSchemaTransaction
{
    private readonly NpgsqlConnection _connection;
    private readonly MasterDbContext _master;
    private readonly Func<SchoolDbContext> _school;
    private readonly ITenantContext _tenant;

    public CrossSchemaTransaction(
        NpgsqlConnection connection,
        MasterDbContext master,
        Func<SchoolDbContext> school,
        ITenantContext tenant)
    {
        _connection = connection;
        _master = master;
        _school = school;
        _tenant = tenant;
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
        => await ExecuteAsync<object?>(async token => { await work(token); return null; }, ct);

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);

        await using var tx = await _connection.BeginTransactionAsync(ct);

        var school = _tenant.IsResolved ? _school() : null;

        await _master.Database.UseTransactionAsync(tx, ct);
        if (school is not null)
            await school.Database.UseTransactionAsync(tx, ct);

        try
        {
            var result = await work(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            // Detach, or the contexts keep pointing at a transaction that no longer exists.
            await _master.Database.UseTransactionAsync(null, ct);
            if (school is not null)
                await school.Database.UseTransactionAsync(null, ct);
        }
    }
}
