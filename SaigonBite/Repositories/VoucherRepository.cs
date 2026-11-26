using Microsoft.EntityFrameworkCore;
using SaigonBite.Models.Entities;

namespace SaigonBite.Repositories;

public class VoucherRepository : IVoucherRepository
{
    private readonly SaigonBiteContext _db;

    public VoucherRepository(SaigonBiteContext db)
    {
        _db = db;
    }

    public Task<Voucher?> GetByCodeAsync(string code) =>
        _db.Vouchers.FirstOrDefaultAsync(v => v.Code == code);
}
