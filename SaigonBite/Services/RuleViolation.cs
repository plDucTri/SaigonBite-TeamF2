namespace SaigonBite.Services;

/// <summary>A business rule that an order or request breaks, with a hint on how to fix it (Figma D17).</summary>
public record RuleViolation(string Rule, string Message, string Fix = "");
