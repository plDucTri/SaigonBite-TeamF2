-- 05_seed_demo_menu.sql
-- DEMO menu so the ordering screens (UC1) can be tried before the Menu screens (UC3) exist.
-- Run AFTER 01, 02, 03 and 04. Safe to run again (existing rows are skipped).
-- Users are NOT created here: the demo accounts come from DbSeeder when the app starts
-- (0901234567, DEMO12345, KITCHEN1, KITCHEN2, KITCHEN3, SHIPPER1-3, ADMIN; password Saigon@123).
USE SaigonBite;
GO

-- Brands
IF NOT EXISTS (SELECT 1 FROM Brands WHERE Name = N'Pho 24')          INSERT INTO Brands (Name) VALUES (N'Pho 24');
IF NOT EXISTS (SELECT 1 FROM Brands WHERE Name = N'Banh Mi Saigon')  INSERT INTO Brands (Name) VALUES (N'Banh Mi Saigon');
IF NOT EXISTS (SELECT 1 FROM Brands WHERE Name = N'Drinks Hub')      INSERT INTO Brands (Name) VALUES (N'Drinks Hub');
GO

-- Every demo brand is sold by the three hubs of script 03
INSERT INTO HubBrands (HubId, BrandId)
SELECT h.HubId, b.BrandId
FROM Hubs h CROSS JOIN Brands b
WHERE h.Name IN (N'Hub 1 - District 1', N'Hub 2 - District 3', N'Test Hub - Capacity 100')
  AND b.Name IN (N'Pho 24', N'Banh Mi Saigon', N'Drinks Hub')
  AND NOT EXISTS (SELECT 1 FROM HubBrands x WHERE x.HubId = h.HubId AND x.BrandId = b.BrandId);
GO

-- Menu items (prices in VND)
INSERT INTO MenuItems (BrandId, Name, Category, Price)
SELECT b.BrandId, v.Name, v.Category, v.Price
FROM (VALUES
    (N'Pho 24',         N'Pho Bo',         N'Noodles',    65000),
    (N'Pho 24',         N'Pho Ga',         N'Noodles',    70000),
    (N'Banh Mi Saigon', N'Banh Mi Thit',   N'Sandwiches', 45000),
    (N'Drinks Hub',     N'Tra Dao',        N'Drinks',     30000),
    (N'Drinks Hub',     N'Ca Phe Sua Da',  N'Drinks',     35000)
) AS v(Brand, Name, Category, Price)
JOIN Brands b ON b.Name = v.Brand
WHERE NOT EXISTS (SELECT 1 FROM MenuItems m WHERE m.BrandId = b.BrandId AND m.Name = v.Name);
GO

SELECT h.Name AS Hub, b.Name AS Brand, COUNT(m.ItemId) AS MenuItems
FROM Hubs h
JOIN HubBrands hb ON hb.HubId = h.HubId
JOIN Brands b ON b.BrandId = hb.BrandId
LEFT JOIN MenuItems m ON m.BrandId = b.BrandId
GROUP BY h.Name, b.Name
ORDER BY h.Name, b.Name;
GO
