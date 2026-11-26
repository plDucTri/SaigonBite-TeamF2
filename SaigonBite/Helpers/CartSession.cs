using System.Text.Json;
using SaigonBite.Services;

namespace SaigonBite.Helpers;

/// <summary>The shopping cart of one hub, kept in the session until the order is placed (FR1).</summary>
public class CartState
{
    public int HubId { get; set; }
    public List<LineRequest> Lines { get; set; } = new();
    public int Count => Lines.Sum(l => l.Quantity);
}

public static class CartSession
{
    private const string Key = "saigonbite.cart";

    public static CartState Get(ISession session)
    {
        var json = session.GetString(Key);
        if (string.IsNullOrEmpty(json)) return new CartState();
        try { return JsonSerializer.Deserialize<CartState>(json) ?? new CartState(); }
        catch (JsonException) { return new CartState(); }
    }

    public static void Save(ISession session, CartState cart) =>
        session.SetString(Key, JsonSerializer.Serialize(cart));

    public static void Clear(ISession session) => session.Remove(Key);
}
