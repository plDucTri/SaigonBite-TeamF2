using SaigonBite.Services;

namespace SaigonBite.Validators;

/// <summary>
/// Input rules for a hub. Rules that need the database (a hub name that already exists, brands that exist,
/// open orders) are in HubService.
/// </summary>
public class HubValidator : IHubValidator
{
    public const int MaxNameLength = 100;       // NVARCHAR(100) in the schema
    public const int MaxAddressLength = 200;    // NVARCHAR(200) in the schema
    public const int MinCapacity = 1;           // the schema has CHECK (KitchenCapacity >= 1)
    public const int MaxCapacity = 1000;        // far above any real kitchen; stops a typing mistake such as 10000

    public IReadOnlyList<string> ValidateHub(HubChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var errors = new List<string>();
        string name = (changes.Name ?? string.Empty).Trim();
        string address = (changes.Address ?? string.Empty).Trim();

        if (name.Length == 0)
            errors.Add("Hub name is required.");
        else if (name.Length > MaxNameLength)
            errors.Add("Hub name must be 100 characters or fewer.");

        if (address.Length == 0)
            errors.Add("Address is required.");
        else if (address.Length > MaxAddressLength)
            errors.Add("Address must be 200 characters or fewer.");

        if (changes.KitchenCapacity is null)
            errors.Add("Kitchen capacity is required.");
        else if (changes.KitchenCapacity < MinCapacity)
            errors.Add("Kitchen capacity must be at least 1 order.");
        else if (changes.KitchenCapacity > MaxCapacity)
            errors.Add("Kitchen capacity must not be more than 1,000 orders.");

        return errors;
    }
}
