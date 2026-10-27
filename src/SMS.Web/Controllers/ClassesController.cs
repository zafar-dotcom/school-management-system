using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Application.Abstractions;
using SMS.Domain.SchoolData;
using SMS.Infrastructure.Persistence;
using SMS.Web.Models;

namespace SMS.Web.Controllers;

/// <summary>
/// The walking skeleton's one real screen. It exists to prove the branch mechanism end to
/// end: reads go through the global filter, and writes take their BranchId from the
/// resolved context rather than from the form.
/// </summary>
[Authorize]
public class ClassesController : Controller
{
    private readonly SchoolDbContext _school;
    private readonly MasterDbContext _master;
    private readonly ITenantContext _tenant;

    public ClassesController(SchoolDbContext school, MasterDbContext master, ITenantContext tenant)
    {
        _school = school;
        _master = master;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await BuildAsync(new NewClassViewModel(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewClassViewModel newClass, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildAsync(newClass, ct));

        var code = newClass.Code.Trim();

        // Unique per branch, per the index on (branch_id, code).
        if (await _school.Classes.AnyAsync(c => c.Code == code, ct))
        {
            ModelState.AddModelError($"{nameof(ClassListViewModel.NewClass)}.{nameof(NewClassViewModel.Code)}",
                $"A class with code '{code}' already exists in this branch.");
            return View(nameof(Index), await BuildAsync(newClass, ct));
        }

        _school.Classes.Add(new SchoolClass
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            Name = newClass.Name.Trim(),
            SortOrder = newClass.SortOrder
            // BranchId is deliberately absent. The auditing interceptor sets it from the
            // resolved tenant context — architecture section 4.6, rules 1 and 2. Binding it
            // from the form is the loophole those rules exist to close.
        });

        await _school.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }

    private async Task<ClassListViewModel> BuildAsync(NewClassViewModel newClass, CancellationToken ct)
    {
        // No BranchId predicate here on purpose: the global query filter adds it. If the
        // filter were ever removed, this screen would start showing other branches' classes
        // and nothing in this method would change.
        var classes = await _school.Classes
            .AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .ToListAsync(ct);

        var names = await _master.Branches
            .AsNoTracking()
            .Where(b => b.Id == _tenant.ActiveBranchId)
            .Select(b => new { Branch = b.Name, School = b.School.Name })
            .FirstAsync(ct);

        return new ClassListViewModel
        {
            Classes = classes,
            SchoolName = names.School,
            BranchName = names.Branch,
            ActiveBranchId = _tenant.ActiveBranchId,
            NewClass = newClass
        };
    }
}
