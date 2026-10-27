using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMS.Application.Classes;
using SMS.Web.Models;

namespace SMS.Web.Controllers;

/// <summary>
/// The walking skeleton's one real screen, now a thin shell over two handlers that the API
/// also calls.
/// </summary>
[Authorize]
public class ClassesController : Controller
{
    private readonly ListClassesHandler _list;
    private readonly CreateClassHandler _create;

    public ClassesController(ListClassesHandler list, CreateClassHandler create)
    {
        _list = list;
        _create = create;
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

        var result = await _create.HandleAsync(
            new CreateClassCommand(newClass.Code, newClass.Name, newClass.SortOrder), ct);

        if (!result.Succeeded)
        {
            // Attach to the code field: every failure this handler reports is about the code.
            foreach (var error in result.Errors)
                ModelState.AddModelError(
                    $"{nameof(ClassListViewModel.NewClass)}.{nameof(NewClassViewModel.Code)}", error);

            return View(nameof(Index), await BuildAsync(newClass, ct));
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<ClassListViewModel> BuildAsync(NewClassViewModel newClass, CancellationToken ct)
    {
        var list = await _list.HandleAsync(ct);

        return new ClassListViewModel
        {
            Classes = list.Classes,
            SchoolName = list.SchoolName,
            BranchName = list.BranchName,
            ActiveBranchId = list.ActiveBranchId,
            NewClass = newClass
        };
    }
}
