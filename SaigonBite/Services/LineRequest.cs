namespace SaigonBite.Services;

/// <summary>One cart line: which menu item and how many.</summary>
public record LineRequest(int ItemId, int Quantity);
