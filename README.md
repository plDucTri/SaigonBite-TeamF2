# SaigonBite - Team F2
ASP.NET Core MVC (Tier 2), Database First, SQL Server, Bootstrap.

## How to run
1. Run Database/01_schema_accounts.sql, 02_schema_orders.sql, then 03_seed_hubs_vouchers.sql in SSMS.
2. Open SaigonBite/SaigonBite.slnx in Visual Studio and press F5.
3. The demo accounts below are created automatically when the app starts (run the 03 script first so the kitchen accounts get their hubs).

## Tests
Test > Run All Tests (project SaigonBite.Tests).

## Test accounts
Password for every demo account: `Saigon@123`
Sign in at /Account/Login. Letter case does not matter for the identifier.

| Role | Sign in with | Notes |
|---|---|---|
| Admin | admin | System Admin |
| Kitchen | kitchen1, kitchen2, kitchen3 | Hub 1, Hub 2, Test Hub (Capacity 100) |
| Shipper | shipper1, shipper2, shipper3 | |
| Local customer | 0901234567 | phone number |
| Foreign tourist | demo12345 | passport number |

New customers can register at /Account/RegisterLocal and /Account/RegisterTourist.

## Roles and pages (for the whole team)
Use the constants in `Helpers/Roles.cs`. Every controller action needs an attribute. Only AccountController, HomeController and LanguageController are open to everyone.

| Pages | Attribute |
|---|---|
| Admin portal (accounts, reports, menu, hubs, vouchers, order oversight) | `[Authorize(Roles = Roles.Admin)]` |
| Kitchen staff pages | `[Authorize(Roles = Roles.Kitchen)]` |
| Shipper pages | `[Authorize(Roles = Roles.Shipper)]` |
| Customer pages (place order, My Orders) | `[Authorize(Roles = Roles.Customers)]` (Local and Tourist) |

The signed-in user id is `User.FindFirst(ClaimTypes.NameIdentifier)`. A person without the right role is sent to /Account/AccessDenied, and a person who is not signed in is sent to /Account/Login.

Temporary check pages: /AccessCheck/Admin, /AccessCheck/Kitchen, /AccessCheck/Shipper, /AccessCheck/Customer (delete when the real pages exist).
