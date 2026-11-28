namespace SaigonBite.Services;

public interface IReportService
{
    /// <summary>
    /// Revenue Report R1 (revenueReport(dateRange) in the architecture diagram).
    /// The end date is included to the end of that day. An end date before the start date fails (E6).
    /// A range with no orders gives 0.
    /// </summary>
    ServiceResult<RevenueReportData> RevenueReport(DateOnly fromDate, DateOnly toDate);

    /// <summary>
    /// Hub Utilization Report R2 (FR16). One row per active hub: orders handled, average preparation time
    /// (Confirmed to Ready), current load % and Quiet Kitchen discounts granted. Same date rules as R1:
    /// the end date is included to the end of that day and an end date before the start date fails (E6).
    /// </summary>
    ServiceResult<HubUtilizationData> HubUtilizationReport(DateOnly fromDate, DateOnly toDate);
}
