using SaigonBite.Models.Entities;
using SaigonBite.Services;

namespace SaigonBite.Repositories;

/// <summary>Data access for menu items (UC3). No business rules here: only queries and saving.</summary>
public interface IMenuItemRepository
{
    /// <summary>Items that are not removed (IsActive), with their brand, filtered. Sorted by brand, then name.</summary>
    List<MenuItem> GetActive(MenuItemFilter filter);

    /// <summary>One item with its brand (tracked, so changes can be saved), or null. Removed items are returned too.</summary>
    MenuItem? Find(int itemId);

    /// <summary>True if the brand already has a not-removed item with this name (letter case ignored).</summary>
    bool NameExists(int brandId, string name, int? exceptItemId);

    void Add(MenuItem item);

    void Save();
}
