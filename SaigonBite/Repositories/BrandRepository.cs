using Microsoft.EntityFrameworkCore;
using SaigonBite.Models.Entities;

namespace SaigonBite.Repositories;

public class BrandRepository : IBrandRepository
{
    private readonly SaigonBiteContext _db;

    public BrandRepository(SaigonBiteContext db)
    {
        _db = db;
    }

    public List<Brand> GetAll(string? search)
    {
        IQueryable<Brand> query = _db.Brands
            .AsNoTracking()
            .Include(b => b.MenuItems)
            .Include(b => b.Hubs);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string text = search.Trim().ToLower();
            query = query.Where(b => b.Name.ToLower().Contains(text));
        }

        return query.OrderBy(b => b.Name).ToList();
    }

    public Brand? Find(int brandId) =>
        _db.Brands
            .Include(b => b.MenuItems)
            .Include(b => b.Hubs)
            .FirstOrDefault(b => b.BrandId == brandId);

    public bool NameExists(string name, int? exceptBrandId)
    {
        string text = name.Trim().ToLower();
        return _db.Brands.Any(b => b.Name.ToLower() == text && (exceptBrandId == null || b.BrandId != exceptBrandId));
    }

    public void Add(Brand brand) => _db.Brands.Add(brand);

    public void Save() => _db.SaveChanges();
}
