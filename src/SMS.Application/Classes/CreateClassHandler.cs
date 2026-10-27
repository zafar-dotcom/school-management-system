using SMS.Application.Abstractions;
using SMS.Application.Common;
using SMS.Domain.SchoolData;

namespace SMS.Application.Classes;

public sealed record CreateClassCommand(string Code, string Name, int SortOrder);

/// <summary>
/// Adds a class to the caller's active branch.
/// <para>
/// Note what the command does <b>not</b> carry: a branch. <c>BranchId</c> is stamped from
/// the resolved tenant context when the row is saved, never supplied by the caller —
/// architecture section 4.6, rules 1 and 2. Accepting one here would reopen exactly the
/// loophole those rules close, for both the web form and the API.
/// </para>
/// </summary>
public sealed class CreateClassHandler
{
    private readonly ISchoolStore _school;

    public CreateClassHandler(ISchoolStore school) => _school = school;

    public async Task<Result<Guid>> HandleAsync(CreateClassCommand command, CancellationToken ct = default)
    {
        var code = command.Code.Trim();

        // Unique per branch, matching the index on (branch_id, code). The lookup is already
        // branch-filtered, so this asks about this branch only.
        if (await _school.ClassCodeExistsAsync(code, ct))
            return Result<Guid>.Failure($"A class with code '{code}' already exists in this branch.");

        var id = Guid.CreateVersion7();

        _school.Add(new SchoolClass
        {
            Id = id,
            Code = code,
            Name = command.Name.Trim(),
            SortOrder = command.SortOrder
        });

        await _school.SaveChangesAsync(ct);

        return Result.Success(id);
    }
}
