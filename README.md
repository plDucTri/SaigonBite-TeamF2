# SaigonBite - Team F2
ASP.NET Core MVC (Tier 2), Database First, SQL Server, Bootstrap.

## How to run
1. In SSMS run the scripts in the Database folder in this order: `01_schema_accounts.sql`, `02_schema_orders.sql`, `03_seed_hubs_vouchers.sql`, `04_alter_status_history_reason.sql`, `05_seed_demo_menu.sql`.
2. Open `SaigonBite/SaigonBite.slnx` in Visual Studio and press F5.
3. The demo accounts below are created automatically when the app starts. Run script 03 first, so the kitchen accounts get their hubs.

## Tests
Test > Run All Tests (project SaigonBite.Tests).

## Test accounts
Password for every demo account: `Saigon@123`
Sign in at `/Account/Login`. Letter case does not matter for the identifier.

| Role           | Sign in with | Full name              | Notes                                                              |
|----------------|--------------|------------------------|--------------------------------------------------------------------|
| Admin          | `admin`      | System Admin           | Admin portal: dashboard, accounts, revenue report, order oversight |
| Kitchen Staff  | `kitchen1`   | Kitchen Staff Hub 1    | Hub 1 - District 1 (capacity 10)                                   |
| Kitchen Staff  | `kitchen2`   | Kitchen Staff Hub 2    | Hub 2 - District 3 (capacity 20)                                   |
| Kitchen Staff  | `kitchen3`   | Kitchen Staff Test Hub | Test Hub - Capacity 100                                            |
| Shipper        | `shipper1`   | Shipper One            |                                                                    |
| Shipper        | `shipper2`   | Shipper Two            |                                                                    |
| Shipper        | `shipper3`   | Shipper Three          |                                                                    |
| Local Customer | `0901234567` | Demo Local Customer    | Phone number. Pays with MoMo, VNPay or cash                        |
| Foreign Tourist| `demo12345`  | Demo Foreign Tourist   | Passport number. Pays with Apple Pay, PayPal or cash               |


## Demo vouchers
| Code    | Type    | Value            | Valid               | Notes                     |
|---------|---------|------------------|---------------------|---------------------------|
| `V10`   | Percent | 10% (cap 30,000) | 20/11 to 30/11/2026 | Minimum subtotal 100,000  |
| `FIX50` | Fixed   | 50,000           | 20/11 to 31/12/2026 | No minimum                |
| `OLD10` | Percent | 10% (cap 30,000) | January 2026        | Expired, for testing      |
| `OFF10` | Percent | 10% (cap 30,000) | 20/11 to 31/12/2026 | Switched off, for testing |