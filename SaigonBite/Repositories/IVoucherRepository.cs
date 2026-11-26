using SaigonBite.Models.Entities;

namespace SaigonBite.Repositories;

public interface IVoucherRepository
{
    /// <summary>The voucher with this code (any state: the PricingEngine decides if it qualifies), or null.</summary>
    Task<Voucher?> GetByCodeAsync(string code);
}
