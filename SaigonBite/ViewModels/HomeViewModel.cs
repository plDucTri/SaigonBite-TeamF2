namespace SaigonBite.ViewModels;

/// <summary>The numbers on the Admin dashboard (the home page). Other roles do not use them.</summary>
public class HomeViewModel
{
    public int ActiveAccounts { get; set; }
    public int InactiveAccounts { get; set; }
    public int OrdersThisMonth { get; set; }
    public decimal RevenueThisMonth { get; set; }
}
