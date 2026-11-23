-- 02_schema_orders.sql
-- SaigonBite menu, voucher and order tables (report section 2.4.3).
-- Run AFTER 01_schema_accounts.sql. Safe to re-run.
USE SaigonBite;
GO

IF OBJECT_ID('dbo.Brands','U') IS NULL
CREATE TABLE Brands(
  BrandId INT IDENTITY PRIMARY KEY,
  Name NVARCHAR(100) NOT NULL,
  IsActive BIT NOT NULL DEFAULT 1);
GO

-- Which brands a hub operates (many to many, BR12)
IF OBJECT_ID('dbo.HubBrands','U') IS NULL
CREATE TABLE HubBrands(
  HubId INT NOT NULL REFERENCES Hubs(HubId),
  BrandId INT NOT NULL REFERENCES Brands(BrandId),
  PRIMARY KEY (HubId, BrandId));
GO

IF OBJECT_ID('dbo.MenuItems','U') IS NULL
CREATE TABLE MenuItems(
  ItemId INT IDENTITY PRIMARY KEY,
  BrandId INT NOT NULL REFERENCES Brands(BrandId),
  Name NVARCHAR(100) NOT NULL,
  Category NVARCHAR(50) NOT NULL,            -- noodles, rice, drinks ... (R1 groups by this)
  Price DECIMAL(12,2) NOT NULL CHECK (Price >= 0),
  IsAvailable BIT NOT NULL DEFAULT 1,        -- temporary switch (BR12)
  IsActive BIT NOT NULL DEFAULT 1);          -- soft delete
GO

-- Deactivated, never deleted (BR9)
IF OBJECT_ID('dbo.Vouchers','U') IS NULL
CREATE TABLE Vouchers(
  Code NVARCHAR(30) PRIMARY KEY,
  Type NVARCHAR(10) NOT NULL CHECK (Type IN ('PERCENT','FIXED')),
  Value DECIMAL(12,2) NOT NULL CHECK (Value > 0),
  MaxDiscount DECIMAL(12,2) NULL,            -- used by PERCENT only
  MinSubtotal DECIMAL(12,2) NOT NULL DEFAULT 0,
  ValidFrom DATE NOT NULL,
  ValidTo DATE NOT NULL,
  IsActive BIT NOT NULL DEFAULT 1,
  CHECK (ValidTo >= ValidFrom));
GO

-- Never deleted: Cancelled and Failed are statuses (BR15, BR16)
IF OBJECT_ID('dbo.Orders','U') IS NULL
CREATE TABLE Orders(
  OrderId INT IDENTITY PRIMARY KEY,
  CustomerId INT NOT NULL REFERENCES Users(UserId),
  HubId INT NOT NULL REFERENCES Hubs(HubId),
  ShipperId INT NULL REFERENCES Users(UserId),
  VoucherCode NVARCHAR(30) NULL REFERENCES Vouchers(Code),
  Status NVARCHAR(20) NOT NULL DEFAULT 'Placed'
    CHECK (Status IN ('Placed','Confirmed','Preparing','Ready',
                      'Out for Delivery','Delivered','Cancelled','Failed')),
  DistanceKm DECIMAL(5,2) NOT NULL CHECK (DistanceKm >= 0),
  DeliveryAddress NVARCHAR(300) NOT NULL,
  SubtotalS DECIMAL(12,2) NOT NULL,
  QuietDiscountQ DECIMAL(12,2) NOT NULL DEFAULT 0,
  VoucherDiscountV DECIMAL(12,2) NOT NULL DEFAULT 0,
  DeliveryFeeD DECIMAL(12,2) NOT NULL DEFAULT 0,
  RushSurchargeR DECIMAL(12,2) NOT NULL DEFAULT 0,
  RainSurchargeR2 DECIMAL(12,2) NOT NULL DEFAULT 0,
  Total DECIMAL(12,0) NOT NULL,              -- rounded once at the end (BR11)
  CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME());
GO

-- 1 to 30 lines per order is enforced in the business layer (BR12)
IF OBJECT_ID('dbo.OrderLines','U') IS NULL
CREATE TABLE OrderLines(
  OrderId INT NOT NULL REFERENCES Orders(OrderId),
  ItemId INT NOT NULL REFERENCES MenuItems(ItemId),
  Quantity INT NOT NULL CHECK (Quantity BETWEEN 1 AND 20),
  UnitPrice DECIMAL(12,2) NOT NULL,          -- price at placement
  PRIMARY KEY (OrderId, ItemId));
GO

-- One per order; recalculation after an edit uses this (BR17)
IF OBJECT_ID('dbo.PriceSnapshots','U') IS NULL
CREATE TABLE PriceSnapshots(
  OrderId INT PRIMARY KEY REFERENCES Orders(OrderId),
  PlacedAt DATETIME2 NOT NULL,
  HubLoadPct DECIMAL(5,2) NOT NULL,
  HeavyRain BIT NOT NULL);
GO

-- One per order
IF OBJECT_ID('dbo.Payments','U') IS NULL
CREATE TABLE Payments(
  PaymentId INT IDENTITY PRIMARY KEY,
  OrderId INT NOT NULL UNIQUE REFERENCES Orders(OrderId),
  Method NVARCHAR(20) NOT NULL
    CHECK (Method IN ('MoMo','VNPay','Apple Pay','PayPal','COD')),
  Amount DECIMAL(12,0) NOT NULL,
  Status NVARCHAR(20) NOT NULL,
  Refunded BIT NOT NULL DEFAULT 0);
GO

-- Audit trail (NFR5). FromStatus is NULL for the first row (placement).
IF OBJECT_ID('dbo.OrderStatusHistory','U') IS NULL
CREATE TABLE OrderStatusHistory(
  HistoryId INT IDENTITY PRIMARY KEY,
  OrderId INT NOT NULL REFERENCES Orders(OrderId),
  FromStatus NVARCHAR(20) NULL,
  ToStatus NVARCHAR(20) NOT NULL,
  ChangedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
  ChangedBy INT NOT NULL REFERENCES Users(UserId));
GO
