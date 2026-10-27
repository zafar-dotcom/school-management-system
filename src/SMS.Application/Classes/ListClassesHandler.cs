using SMS.Application.Abstractions;
using SMS.Domain.SchoolData;

namespace SMS.Application.Classes;

public sealed record ClassList(
    IReadOnlyList<SchoolClass> Classes,
    string SchoolName,
    string BranchName,
    Guid ActiveBranchId);

/// <summary>
/// The classes in the caller's active branch.
/// <para>
/// There is no branch predicate anywhere in this handler. The global query filter supplies
/// it, so a new query cannot forget it — architecture section 4.6, rule 3.
/// </para>
/// </summary>
public sealed class ListClassesHandler
{
    private readonly ISchoolStore _school;
    private readonly IMasterStore _master;
    private readonly ITenantContext _tenant;

    public ListClassesHandler(ISchoolStore school, IMasterStore master, ITenantContext tenant)
    {
        _school = school;
        _master = master;
        _tenant = tenant;
    }

    public async Task<ClassList> HandleAsync(CancellationToken ct = default)
    {
        var classes = await _school.ListClassesAsync(ct);
        var names = await _master.FindContextNamesAsync(_tenant.ActiveBranchId, ct);

        return new ClassList(
            classes,
            names?.SchoolName ?? string.Empty,
            names?.BranchName ?? string.Empty,
            _tenant.ActiveBranchId);
    }
}
