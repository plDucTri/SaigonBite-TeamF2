using SaigonBite.Models.Entities;

namespace SaigonBite.Repositories;

/// <summary>
/// What the Hub Utilization Report needs to know about one order: the hub, the status, whether a Quiet Kitchen
/// discount was granted, and when the order was confirmed and became Ready (null if it never did).
/// </summary>
public record HubOrderFact(int HubId, string Status, decimal QuietDiscount, DateTime? ConfirmedAt, DateTime? ReadyAt);

/// <summary>How many orders of a hub are Confirmed and how many are Preparing right now (BR8 counts both as load).</summary>
public record HubOrderCounts(int Confirmed, int Preparing);

/// <summary>Data access for hubs: the Hub Utilization Report R2 and the Admin Hubs and Capacity page. No business rules here.</summary>
public interface IHubRepository
{
    /// <summary>Active hubs, by name.</summary>
    List<Hub> GetActiveHubs();

    /// <summary>BR8: for each hub, the number of orders that are Confirmed or Preparing right now. Hubs with none are missing.</summary>
    Dictionary<int, int> CountActiveLoad();

    /// <summary>One fact per order created between start and end (both included), whatever its status.</summary>
    List<HubOrderFact> GetOrderFacts(DateTime start, DateTime end);

    // ----- Hubs and Capacity page (FR17) -----

    /// <summary>All hubs, active and inactive, by name (no related rows loaded).</summary>
    List<Hub> GetAll();

    /// <summary>Confirmed and Preparing orders per hub right now. Hubs with none are missing.</summary>
    Dictionary<int, HubOrderCounts> CountLoadByStatus();

    /// <summary>One hub with its brands and users (tracked, so changes can be saved), or null.</summary>
    Hub? Find(int hubId);

    /// <summary>True if another hub already has this name (letter case ignored, inactive hubs included).</summary>
    bool NameExists(string name, int? exceptHubId);

    /// <summary>True if the hub has an order that is not finished yet (Placed, Confirmed, Preparing, Ready or Out for Delivery).</summary>
    bool HasOpenOrders(int hubId);

    /// <summary>The brands with these ids (tracked), so they can be linked to a hub.</summary>
    List<Brand> GetBrands(IEnumerable<int> brandIds);

    void Add(Hub hub);

    void Save();
}
