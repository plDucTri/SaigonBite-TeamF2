using SaigonBite.Services;

namespace SaigonBite.ViewModels;

/// <summary>The date range shown in the two date boxes and, when it is valid, the report for it.</summary>
public class RevenueReportViewModel
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    /// <summary>Null when the range was not valid. The reason is in ModelState.</summary>
    public RevenueReportData? Report { get; set; }
}
