namespace SaigonBite.Services;

public interface IReportService
{
    /// <summary>
    /// Revenue Report R1 (revenueReport(dateRange) in the architecture diagram).
    /// The end date is included to the end of that day. An end date before the start date fails (E6).
    /// A range with no orders gives 0.
    /// </summary>
    ServiceResult<RevenueReportData> RevenueReport(DateOnly fromDate, DateOnly toDate);
}
