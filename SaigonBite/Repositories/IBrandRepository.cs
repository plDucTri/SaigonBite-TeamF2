using SaigonBite.Models.Entities;

namespace SaigonBite.Repositories;

/// <summary>Data access for brands (UC3). No business rules here: only queries and saving.</summary>
public interface IBrandRepository
{
    /// <summary>All brands, active and inactive, with their menu items and operating hubs. Search matches part of the name.</summary>
    List<Brand> GetAll(string? search);

    /// <summary>One brand with its menu items and operating hubs (tracked, so changes can be saved), or null.</summary>
    Brand? Find(int brandId);

    /// <summary>True if another brand already has this name (letter case ignored, inactive brands included).</summary>
    bool NameExists(string name, int? exceptBrandId);

    void Add(Brand brand);

    void Save();
}
