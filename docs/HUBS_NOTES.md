# Hubs & Capacity page (FR17, Figma A04 and A09)

Admin only. `/Admin/Hubs` (list), `/Admin/Hubs/New`, `/Admin/Hubs/{id}/Edit`. Soft delete only.
It is the page where a brand's "Operating Hubs" are set (the Brands form links to it).

## Needs your team's attention
- Workbook task **T15** (Admin hub, capacity and voucher management, new FR17) is assigned to 524K0040. Tell them this page now exists so you do not both build it, and add FR17 to the Requirements and RTM sheets.
- FR17 has no text in the workbook, so the rules below are assumptions to log (suggested A-18 to A-20).

## Assumptions
- A-18 Capacity: a whole number from 1 to 1,000 (the schema only requires >= 1). Lowering it never touches existing orders; it only raises the load, so BR8 may then reject new orders.
- A-19 A hub cannot be deactivated while it has an order in Placed, Confirmed, Preparing, Ready or Out for Delivery. Delivered, Cancelled and Failed orders do not block. Reactivation is done with the Active radio on the edit form.
- A-20 Brands operated: only active brands can be newly added; a brand the hub already operates may stay even if it is inactive now. Removing a brand from a hub does not change open orders, it only hides the brand from new orders. Kitchen staff are assigned to a hub on the User Accounts pages, so this page only lists them.
- Load = (Confirmed + Preparing) / capacity (BR8). "Quiet Discount Eligible" = load below 30% (BR7, `PricingEngine.QuietBelowPct`).

## Not verified
No .NET SDK in the sandbox: nothing was compiled or run. Run `dotnet build` and Test Explorer first.
