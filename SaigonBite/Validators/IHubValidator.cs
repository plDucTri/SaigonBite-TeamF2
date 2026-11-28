using SaigonBite.Services;

namespace SaigonBite.Validators;

/// <summary>Input rules of the Hubs and Capacity page (no database). An empty list means the input is fine.</summary>
public interface IHubValidator
{
    IReadOnlyList<string> ValidateHub(HubChanges changes);
}
