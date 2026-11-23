-- 03_seed_hubs_vouchers.sql
-- Seed data for pricing and boundary tests. Run AFTER 01 and 02. Safe to re-run.
USE SaigonBite;
GO

-- Hubs: capacity 10 (R2 test), capacity 20 (other-hub test), capacity 100 (load tests)
IF NOT EXISTS (SELECT 1 FROM Hubs WHERE Name = N'Hub 1 - District 1')
  INSERT INTO Hubs (Name, Address, KitchenCapacity)
  VALUES (N'Hub 1 - District 1', N'12 Nguyen Hue, District 1, Ho Chi Minh City', 10);

IF NOT EXISTS (SELECT 1 FROM Hubs WHERE Name = N'Hub 2 - District 3')
  INSERT INTO Hubs (Name, Address, KitchenCapacity)
  VALUES (N'Hub 2 - District 3', N'45 Vo Van Tan, District 3, Ho Chi Minh City', 20);

IF NOT EXISTS (SELECT 1 FROM Hubs WHERE Name = N'Test Hub - Capacity 100')
  INSERT INTO Hubs (Name, Address, KitchenCapacity)
  VALUES (N'Test Hub - Capacity 100', N'Test address, Ho Chi Minh City', 100);
GO

-- V10: active PERCENT 10%, cap 30,000, minimum subtotal 100,000, valid 20/11 to 30/11
IF NOT EXISTS (SELECT 1 FROM Vouchers WHERE Code = N'V10')
  INSERT INTO Vouchers (Code, Type, Value, MaxDiscount, MinSubtotal, ValidFrom, ValidTo, IsActive)
  VALUES (N'V10', 'PERCENT', 10, 30000, 100000, '2026-11-20', '2026-11-30', 1);

-- FIXED voucher, minimum 0
IF NOT EXISTS (SELECT 1 FROM Vouchers WHERE Code = N'FIX50')
  INSERT INTO Vouchers (Code, Type, Value, MaxDiscount, MinSubtotal, ValidFrom, ValidTo, IsActive)
  VALUES (N'FIX50', 'FIXED', 50000, NULL, 0, '2026-11-20', '2026-12-31', 1);

-- Expired voucher
IF NOT EXISTS (SELECT 1 FROM Vouchers WHERE Code = N'OLD10')
  INSERT INTO Vouchers (Code, Type, Value, MaxDiscount, MinSubtotal, ValidFrom, ValidTo, IsActive)
  VALUES (N'OLD10', 'PERCENT', 10, 30000, 0, '2026-01-01', '2026-01-31', 1);

-- Inactive voucher (still inside its dates)
IF NOT EXISTS (SELECT 1 FROM Vouchers WHERE Code = N'OFF10')
  INSERT INTO Vouchers (Code, Type, Value, MaxDiscount, MinSubtotal, ValidFrom, ValidTo, IsActive)
  VALUES (N'OFF10', 'PERCENT', 10, 30000, 0, '2026-11-20', '2026-12-31', 0);
GO
