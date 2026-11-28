namespace SaigonBite.Services;

/// <summary>
/// Admin Hubs and Capacity (FR17, Figma A04 and A09): list hubs with their live load, add and edit a hub
/// (name, address, kitchen capacity, brands operated) and deactivate it. A hub is never deleted:
/// deactivating sets IsActive = false, so its orders and reports stay correct.
/// </summary>
public interface IHubService
{
    IReadOnlyList<HubSummary> ListHubs();
    ServiceResult<HubDetails> GetHub(int hubId);
    ServiceResult<int> CreateHub(HubChanges changes);
    OperationResult UpdateHub(int hubId, HubChanges changes);

    /// <summary>Soft delete. Refused while the hub still has orders that are not finished.</summary>
    OperationResult DeactivateHub(int hubId);

    /// <summary>The "Brands Operated" check list: the active brands, plus the ids in keepBrandIds even if inactive now.</summary>
    IReadOnlyList<BrandOption> ListBrandOptions(IEnumerable<int>? keepBrandIds);
}
