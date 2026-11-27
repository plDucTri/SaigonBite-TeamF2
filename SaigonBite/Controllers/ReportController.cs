using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonBite.Helpers;
using SaigonBite.Services;
using SaigonBite.ViewModels;

namespace SaigonBite.Controllers;

/// <summary>
/// Admin Portal: Revenue Report R1. Only the Admin role may open it (NFR4).
/// Tai adds the Hub Utilization report R2 here later.
/// </summary>
[Authorize(Roles = Roles.Admin)]
[Route("Admin/Revenue")]
public class ReportController : Controller
{
    private readonly IReportService _reports;
    private readonly TimeProvider _time;

    public ReportController(IReportService reports, TimeProvider time)
    {
        _reports = reports;
        _time = time;
    }

    // revenueReport(dateRange) in the architecture diagram. It only reads data (P6).
    // Opened without dates it shows this month so far.
    [HttpGet("")]
    public IActionResult RevenueReport(DateOnly? fromDate, DateOnly? toDate)
    {
        // If a date in the address could not be read, say so and show the form again.
        bool unreadable = !ModelState.IsValid;

        DateOnly today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var vm = new RevenueReportViewModel
        {
            FromDate = fromDate ?? new DateOnly(today.Year, today.Month, 1),
            ToDate = toDate ?? today
        };

        if (unreadable)
        {
            ModelState.Clear();
            ModelState.AddModelError(string.Empty, "Choose a valid start date and end date.");
            return View(vm);
        }

        var result = _reports.RevenueReport(vm.FromDate, vm.ToDate);
        if (result.Ok)
        {
            vm.Report = result.Value;
        }
        else
        {
            foreach (string error in result.Errors)
                ModelState.AddModelError(string.Empty, error);
        }

        return View(vm);
    }
}
