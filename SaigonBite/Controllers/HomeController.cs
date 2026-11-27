using Microsoft.AspNetCore.Mvc;
using SaigonBite.Helpers;
using SaigonBite.Models;
using SaigonBite.Repositories;
using SaigonBite.Services;
using SaigonBite.ViewModels;
using System.Diagnostics;

namespace SaigonBite.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAccountService _accounts;
        private readonly IReportService _reports;
        private readonly TimeProvider _time;

        public HomeController(IAccountService accounts, IReportService reports, TimeProvider time)
        {
            _accounts = accounts;
            _reports = reports;
            _time = time;
        }

        public IActionResult Index()
        {
            var vm = new HomeViewModel();

            // The dashboard numbers are only for the Admin. The services do the work, so there is no rule here.
            if (User.IsInRole(Roles.Admin))
            {
                vm.ActiveAccounts = CountAccounts(AccountStatus.Active);
                vm.InactiveAccounts = CountAccounts(AccountStatus.Inactive);

                // This month so far
                DateOnly today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
                var report = _reports.RevenueReport(new DateOnly(today.Year, today.Month, 1), today);
                if (report.Ok)
                {
                    vm.OrdersThisMonth = report.Value!.OrderCount;
                    vm.RevenueThisMonth = report.Value.IncludedRevenue;
                }
            }

            return View(vm);
        }

        // Page size 1, because only the total number is needed.
        private int CountAccounts(string status)
        {
            var filter = new UserFilter { Status = status, Page = 1, PageSize = 1 };
            return _accounts.ListAccounts(filter).TotalCount;
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
