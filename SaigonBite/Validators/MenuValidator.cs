using SaigonBite.Helpers;
using SaigonBite.Services;

namespace SaigonBite.Validators;

/// <summary>
/// Input rules for brands and menu items (FR15, test T-50). Rules that need the database
/// (a brand name that already exists) are in MenuService.
/// </summary>
public class MenuValidator : IMenuValidator
{
    public const int MaxNameLength = 100;          // NVARCHAR(100) in the schema
    public const decimal MaxPrice = 99_999_999m;   // 99,999,999 VND: far above any real dish, well inside DECIMAL(12,2)

    public IReadOnlyList<string> ValidateBrand(BrandChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var errors = new List<string>();
        string name = (changes.Name ?? string.Empty).Trim();

        if (name.Length == 0)
            errors.Add("Brand name is required.");
        else if (name.Length > MaxNameLength)
            errors.Add("Brand name must be 100 characters or fewer.");

        return errors;
    }

    public IReadOnlyList<string> ValidateItem(MenuItemChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var errors = new List<string>();
        string name = (changes.Name ?? string.Empty).Trim();

        if (name.Length == 0)
            errors.Add("Item name is required.");
        else if (name.Length > MaxNameLength)
            errors.Add("Item name must be 100 characters or fewer.");

        if (changes.BrandId <= 0)
            errors.Add("Choose a brand.");

        if (MenuCategories.Find(changes.Category) is null)
            errors.Add("Choose a category from the list.");

        if (changes.Price is null)
            errors.Add("Price is required.");
        else if (changes.Price <= 0)
            errors.Add("Price must be greater than 0 VND.");
        else if (changes.Price > MaxPrice)
            errors.Add("Price must not be more than 99,999,999 VND.");
        else if (changes.Price != decimal.Truncate(changes.Price.Value))
            errors.Add("Price must be a whole number of VND.");

        return errors;
    }
}
