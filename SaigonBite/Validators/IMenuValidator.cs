using SaigonBite.Services;

namespace SaigonBite.Validators;

/// <summary>Input rules of UC3 (no database). Returns a list of messages; an empty list means the input is fine.</summary>
public interface IMenuValidator
{
    IReadOnlyList<string> ValidateBrand(BrandChanges changes);
    IReadOnlyList<string> ValidateItem(MenuItemChanges changes);
}
