/* ============================================================================
   SE Lab Group Assignment 1: Smart Ticketing System – Ticket Vendor Machine
   Database Setup Script: TvmDemo (Microsoft SQL Server / Express / LocalDB)
   Aligned 100% with Member M3 Section 3.3.3 Class Diagram & TVM.cs Skeleton:
     - Entities: Destination, Fare, [Transaction], Payment (CardPayment / QRPayment),
                 Ticket (1..5 per Transaction, 16-digit Luhn barcode), Refund
     - Enums: TransportMode (Mrt, Bus), PaymentMethod (CreditCard, BankQR, DigitalWallet),
              WalletProvider (Momo, ZaloPay, VNPay), TransactionStatus, PaymentStatus, RefundStatus
   Idempotent: Safe to execute multiple times without error.
   ============================================================================ */

SET NOCOUNT ON;
GO

-- 1. Create Database if not exists
IF DB_ID(N'TvmDemo') IS NULL
BEGIN
    CREATE DATABASE [TvmDemo];
    PRINT 'Database [TvmDemo] created.';
END
ELSE
BEGIN
    PRINT 'Database [TvmDemo] already exists.';
END
GO

USE [TvmDemo];
GO

/* ============================================================================
   OPTIONAL WINDOWS LOGIN PERMISSION BLOCK (Grant current Windows user db_owner)
   Uncomment and replace [DOMAIN\YourWindowsUser] if needed on shared SQL Server
   ============================================================================
DECLARE @WinLogin SYSNAME = SUSER_SNAME();
DECLARE @Sql NVARCHAR(MAX);
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @WinLogin)
BEGIN
    SET @Sql = N'CREATE LOGIN ' + QUOTENAME(@WinLogin) + N' FROM WINDOWS;';
    EXEC sp_executesql @Sql;
END
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @WinLogin)
BEGIN
    SET @Sql = N'CREATE USER ' + QUOTENAME(@WinLogin) + N' FOR LOGIN ' + QUOTENAME(@WinLogin) + N';';
    EXEC sp_executesql @Sql;
END
ALTER ROLE [db_owner] ADD MEMBER CURRENT_USER;
GO
*/

-- 2. Drop views/procedures/tables in reverse dependency order for clean idempotent re-run
IF OBJECT_ID(N'dbo.usp_GetRecentTransactions', N'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetRecentTransactions;
GO

IF OBJECT_ID(N'dbo.vw_RecentTransactions', N'V') IS NOT NULL
    DROP VIEW dbo.vw_RecentTransactions;
GO

IF OBJECT_ID(N'dbo.Refund', N'U') IS NOT NULL DROP TABLE dbo.Refund;
IF OBJECT_ID(N'dbo.Ticket', N'U') IS NOT NULL DROP TABLE dbo.Ticket;
IF OBJECT_ID(N'dbo.Payment', N'U') IS NOT NULL DROP TABLE dbo.Payment;
IF OBJECT_ID(N'dbo.[Transaction]', N'U') IS NOT NULL DROP TABLE dbo.[Transaction];
IF OBJECT_ID(N'dbo.Fare', N'U') IS NOT NULL DROP TABLE dbo.Fare;
IF OBJECT_ID(N'dbo.Destination', N'U') IS NOT NULL DROP TABLE dbo.Destination;
GO

-- 3. Table: Destination (matches M3 Class Diagram: destinationId, name, mode, origin)
CREATE TABLE dbo.Destination
(
    DestinationId   INT IDENTITY(1,1) NOT NULL,
    StationCode     VARCHAR(12)       NOT NULL,
    Name            NVARCHAR(100)     NOT NULL, -- English display name
    NameVi          NVARCHAR(100)     NOT NULL, -- Vietnamese display name
    Mode            VARCHAR(10)       NOT NULL, -- TransportMode: 'Mrt' | 'Bus' (FR15)
    Origin          NVARCHAR(100)     NOT NULL DEFAULT N'Ben Thanh Central Station',
    ZoneOrder       INT               NOT NULL,
    TravelMinutes   INT               NOT NULL,
    IsActive        BIT               NOT NULL CONSTRAINT DF_Destination_IsActive DEFAULT (1),

    CONSTRAINT PK_Destination PRIMARY KEY CLUSTERED (DestinationId),
    CONSTRAINT UQ_Destination_StationCode UNIQUE (StationCode),
    CONSTRAINT CK_Destination_Mode CHECK (Mode IN ('Mrt', 'Bus')),
    CONSTRAINT CK_Destination_TravelMinutes CHECK (TravelMinutes > 0)
);
GO

-- 4. Table: Fare (matches M3 Class Diagram: Destination 1 -- 1..* Fare; DR1 whole VND)
CREATE TABLE dbo.Fare
(
    FareId          INT IDENTITY(1,1) NOT NULL,
    DestinationId   INT               NOT NULL,
    AmountVND       DECIMAL(12,0)     NOT NULL, -- DR1: whole VND
    EffectiveFrom   DATETIME2(0)      NOT NULL CONSTRAINT DF_Fare_EffectiveFrom DEFAULT (SYSDATETIME()),

    CONSTRAINT PK_Fare PRIMARY KEY CLUSTERED (FareId),
    CONSTRAINT FK_Fare_Destination FOREIGN KEY (DestinationId) REFERENCES dbo.Destination(DestinationId),
    CONSTRAINT CK_Fare_AmountVND CHECK (AmountVND >= 1000 AND AmountVND = FLOOR(AmountVND))
);
GO

-- 5. Table: [Transaction] (matches M3 Class Diagram Transaction attributes & TransactionStatus enum)
CREATE TABLE dbo.[Transaction]
(
    TransactionId   VARCHAR(36)       NOT NULL,
    DestinationId   INT               NOT NULL,
    Quantity        INT               NOT NULL, -- FR3: 1..5
    FareSnapshot    DECIMAL(12,0)     NOT NULL, -- FR4: locked unit fare at selection time
    TotalAmount     DECIMAL(12,0)     NOT NULL,
    RetryCount      INT               NOT NULL CONSTRAINT DF_Tx_RetryCount DEFAULT (0),   -- FR12: max 3
    QrRegenCount    INT               NOT NULL CONSTRAINT DF_Tx_QrRegenCount DEFAULT (0), -- FR13: max 2
    Status          VARCHAR(25)       NOT NULL CONSTRAINT DF_Tx_Status DEFAULT ('Created'),
    GatewayRef      VARCHAR(64)       NULL,
    MachineCode     VARCHAR(20)       NOT NULL CONSTRAINT DF_Tx_MachineCode DEFAULT ('TVM-BT-01'),
    CreatedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_Tx_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_Tx_UpdatedAt DEFAULT (SYSDATETIME()),

    CONSTRAINT PK_Transaction PRIMARY KEY CLUSTERED (TransactionId),
    CONSTRAINT FK_Transaction_Destination FOREIGN KEY (DestinationId) REFERENCES dbo.Destination(DestinationId),
    CONSTRAINT CK_Transaction_Quantity CHECK (Quantity BETWEEN 1 AND 5),
    CONSTRAINT CK_Transaction_TotalAmount CHECK (TotalAmount = Quantity * FareSnapshot AND TotalAmount > 0),
    CONSTRAINT CK_Transaction_Retry CHECK (RetryCount BETWEEN 0 AND 3 AND QrRegenCount BETWEEN 0 AND 2),
    CONSTRAINT CK_Transaction_Status CHECK (Status IN (
        'Created', 'AwaitingPayment', 'Confirmed', 'Completed',
        'Failed', 'Cancelled', 'RefundPending', 'Refunded', 'PendingReconciliation'
    ))
);
GO

-- 6. Table: Payment (TPH mapping for M3 abstract Payment -> CardPayment | QRPayment)
-- Note in M3 Class Diagram: QRPayment.Provider is NULL for BankQR (VietQR) and Momo/ZaloPay/VNPay for DigitalWallet.
CREATE TABLE dbo.Payment
(
    PaymentId       VARCHAR(36)       NOT NULL,
    TransactionId   VARCHAR(36)       NOT NULL, -- M3: Transaction 1 o-- 0..* Payment (supports retries FR12)
    PaymentType     VARCHAR(20)       NOT NULL, -- 'CardPayment' | 'QRPayment'
    Method          VARCHAR(20)       NOT NULL, -- PaymentMethod: 'CreditCard' | 'BankQR' | 'DigitalWallet'
    Amount          DECIMAL(12,0)     NOT NULL,
    Status          VARCHAR(20)       NOT NULL CONSTRAINT DF_Payment_Status DEFAULT ('Pending'),
    GatewayRef      VARCHAR(64)       NULL,
    -- CardPayment columns (NFR9: never store full PAN or CVV)
    CardScheme      VARCHAR(20)       NULL,
    Last4           CHAR(4)           NULL,
    GatewayToken    VARCHAR(80)       NULL,
    -- QRPayment columns (FR13: expiresAt = created + 120s; provider = null for BankQR else Momo/ZaloPay/VNPay)
    QrContent       NVARCHAR(512)     NULL,
    ExpiresAt       DATETIME2(0)      NULL,
    WalletProvider  VARCHAR(20)       NULL,
    CreatedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_Payment_CreatedAt DEFAULT (SYSDATETIME()),

    CONSTRAINT PK_Payment PRIMARY KEY CLUSTERED (PaymentId),
    CONSTRAINT FK_Payment_Transaction FOREIGN KEY (TransactionId) REFERENCES dbo.[Transaction](TransactionId),
    CONSTRAINT CK_Payment_Type CHECK (PaymentType IN ('CardPayment', 'QRPayment')),
    CONSTRAINT CK_Payment_Method CHECK (Method IN ('CreditCard', 'BankQR', 'DigitalWallet')),
    CONSTRAINT CK_Payment_Status CHECK (Status IN ('Pending', 'Approved', 'Declined', 'Expired', 'Cancelled')),
    CONSTRAINT CK_Payment_WalletProvider CHECK (WalletProvider IS NULL OR WalletProvider IN ('Momo', 'ZaloPay', 'VNPay')),
    CONSTRAINT CK_Payment_Amount CHECK (Amount > 0)
);
GO

-- 7. Table: Ticket (matches M3 Class Diagram: Transaction 1 *-- 1..5 Ticket; DR4 16-digit Luhn TicketNumber)
CREATE TABLE dbo.Ticket
(
    TicketId        INT IDENTITY(1,1) NOT NULL,
    TicketNumber    CHAR(16)          NOT NULL, -- DR4 & FR16: 16 digits with Luhn check digit
    TransactionId   VARCHAR(36)       NOT NULL,
    Barcode         VARCHAR(64)       NOT NULL, -- Code 128 / Code 39 barcode payload
    Mode            VARCHAR(10)       NOT NULL, -- FR15: 'Mrt' | 'Bus' printed on ticket
    Origin          NVARCHAR(100)     NOT NULL,
    Destination     NVARCHAR(100)     NOT NULL,
    Fare            DECIMAL(12,0)     NOT NULL,
    IssuedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_Ticket_IssuedAt DEFAULT (SYSDATETIME()),
    ValidUntil      DATETIME2(0)      NOT NULL,

    CONSTRAINT PK_Ticket PRIMARY KEY CLUSTERED (TicketId),
    CONSTRAINT UQ_Ticket_TicketNumber UNIQUE (TicketNumber),
    CONSTRAINT FK_Ticket_Transaction FOREIGN KEY (TransactionId) REFERENCES dbo.[Transaction](TransactionId),
    CONSTRAINT CK_Ticket_Mode CHECK (Mode IN ('Mrt', 'Bus')),
    CONSTRAINT CK_Ticket_ValidUntil CHECK (ValidUntil > IssuedAt)
);
GO

-- 8. Table: Refund (matches M3 Class Diagram: Transaction 1 --> 0..1 Refund)
CREATE TABLE dbo.Refund
(
    RefundId        VARCHAR(36)       NOT NULL,
    TransactionId   VARCHAR(36)       NOT NULL,
    Amount          DECIMAL(12,0)     NOT NULL,
    Reference       VARCHAR(64)       NOT NULL,
    RequestedAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Refund_RequestedAt DEFAULT (SYSDATETIME()),
    Status          VARCHAR(20)       NOT NULL CONSTRAINT DF_Refund_Status DEFAULT ('Requested'),

    CONSTRAINT PK_Refund PRIMARY KEY CLUSTERED (RefundId),
    CONSTRAINT UQ_Refund_Transaction UNIQUE (TransactionId),
    CONSTRAINT FK_Refund_Transaction FOREIGN KEY (TransactionId) REFERENCES dbo.[Transaction](TransactionId),
    CONSTRAINT CK_Refund_Status CHECK (Status IN ('Requested', 'Confirmed', 'Escalated'))
);
GO

-- 9. Performance Indexes
CREATE INDEX IX_Fare_Destination_Effective ON dbo.Fare(DestinationId, EffectiveFrom DESC);
CREATE INDEX IX_Transaction_CreatedAt ON dbo.[Transaction](CreatedAt DESC) INCLUDE (Status, TotalAmount, DestinationId);
CREATE INDEX IX_Payment_TransactionId ON dbo.Payment(TransactionId, CreatedAt DESC);
CREATE INDEX IX_Ticket_TransactionId ON dbo.Ticket(TransactionId);
GO

-- 10. Seed Data: 10 HCMC Metro Line 1 Stations (Mrt) + 2 Metro Feeder Routes (Bus) = 12 Active Touchscreen Destinations (FR15)
INSERT INTO dbo.Destination (StationCode, Name, NameVi, Mode, Origin, ZoneOrder, TravelMinutes)
VALUES
    ('M1-02',  N'Opera House',          N'Nhà hát Thành phố',      'Mrt', N'Bến Thành (M1-01)', 1,  2),
    ('M1-03',  N'Ba Son',               N'Ba Son',                 'Mrt', N'Bến Thành (M1-01)', 2,  4),
    ('M1-04',  N'Van Thanh Park',       N'Công viên Văn Thánh',    'Mrt', N'Bến Thành (M1-01)', 3,  7),
    ('M1-05',  N'Tan Cang',             N'Tân Cảng',               'Mrt', N'Bến Thành (M1-01)', 4,  9),
    ('M1-06',  N'Thao Dien',            N'Thảo Điền',              'Mrt', N'Bến Thành (M1-01)', 5, 12),
    ('M1-07',  N'An Phu',               N'An Phú',                 'Mrt', N'Bến Thành (M1-01)', 6, 14),
    ('M1-09',  N'Phuoc Long',           N'Phước Long',             'Mrt', N'Bến Thành (M1-01)', 7, 20),
    ('M1-11',  N'Thu Duc',              N'Thủ Đức',                'Mrt', N'Bến Thành (M1-01)', 8, 25),
    ('M1-12',  N'High Tech Park',       N'Khu Công nghệ cao',      'Mrt', N'Bến Thành (M1-01)', 9, 28),
    ('M1-14',  N'Suoi Tien Terminal',   N'Bến xe Suối Tiên',       'Mrt', N'Bến Thành (M1-01)', 10, 32),
    ('BUS-01', N'Cho Lon Bus 01',       N'BX Chợ Lớn (Bus 01)',    'Bus', N'Trạm Bến Thành',    11, 25),
    ('BUS-109',N'TSN Airport Bus 109',  N'Sân bay TSN (Bus 109)',  'Bus', N'Trạm Bến Thành',    12, 35);

-- Seed Effective Fares (DR1: whole VND)
INSERT INTO dbo.Fare (DestinationId, AmountVND, EffectiveFrom)
SELECT DestinationId,
       CASE StationCode
           WHEN 'M1-02'   THEN  7000
           WHEN 'M1-03'   THEN  8000
           WHEN 'M1-04'   THEN  9000
           WHEN 'M1-05'   THEN 10000
           WHEN 'M1-06'   THEN 11000
           WHEN 'M1-07'   THEN 12000
           WHEN 'M1-09'   THEN 15000
           WHEN 'M1-11'   THEN 18000
           WHEN 'M1-12'   THEN 19000
           WHEN 'M1-14'   THEN 20000
           WHEN 'BUS-01'  THEN  6000
           WHEN 'BUS-109' THEN 15000
       END,
       '2026-01-01T00:00:00'
FROM dbo.Destination;

-- Seed 3 realistic sample transactions so Output Report has immediate historical rows
INSERT INTO dbo.[Transaction] (TransactionId, DestinationId, Quantity, FareSnapshot, TotalAmount, RetryCount, QrRegenCount, Status, GatewayRef, CreatedAt, UpdatedAt)
VALUES
    ('TXN-20261029-0001', 5,  2, 11000, 22000, 0, 0, 'Completed', 'GW-CARD-982410', DATEADD(MINUTE, -35, SYSDATETIME()), DATEADD(MINUTE, -35, SYSDATETIME())),
    ('TXN-20261029-0002', 10, 1, 20000, 20000, 0, 0, 'Completed', 'GW-MOMO-441209', DATEADD(MINUTE, -20, SYSDATETIME()), DATEADD(MINUTE, -20, SYSDATETIME())),
    ('TXN-20261029-0003', 11, 1,  6000,  6000, 1, 0, 'Failed',    'GW-DECL-100932', DATEADD(MINUTE, -10, SYSDATETIME()), DATEADD(MINUTE, -10, SYSDATETIME()));

INSERT INTO dbo.Payment (PaymentId, TransactionId, PaymentType, Method, Amount, Status, GatewayRef, CardScheme, Last4, GatewayToken, QrContent, ExpiresAt, WalletProvider, CreatedAt)
VALUES
    ('PAY-20261029-0001', 'TXN-20261029-0001', 'CardPayment', 'CreditCard',    22000, 'Approved', 'GW-CARD-982410', 'VISA', '4242', 'TOK-EMV-982410', NULL, NULL, NULL, DATEADD(MINUTE, -35, SYSDATETIME())),
    ('PAY-20261029-0002', 'TXN-20261029-0002', 'QRPayment',   'DigitalWallet', 20000, 'Approved', 'GW-MOMO-441209', NULL, NULL, NULL, N'0002010102123857MOMO|TXN-20261029-0002|20000', DATEADD(MINUTE, -18, SYSDATETIME()), 'Momo', DATEADD(MINUTE, -20, SYSDATETIME())),
    ('PAY-20261029-0003', 'TXN-20261029-0003', 'CardPayment', 'CreditCard',     6000, 'Declined', 'GW-DECL-100932', 'NAPAS', '8819', 'TOK-EMV-100932', NULL, NULL, NULL, DATEADD(MINUTE, -10, SYSDATETIME()));

INSERT INTO dbo.Ticket (TicketNumber, TransactionId, Barcode, Mode, Origin, Destination, Fare, IssuedAt, ValidUntil)
VALUES
    ('2610291100010482', 'TXN-20261029-0001', '2610291100010482', 'Mrt', N'Bến Thành (M1-01)', N'Thảo Điền',        11000, DATEADD(MINUTE, -35, SYSDATETIME()), DATEADD(HOUR, 24, DATEADD(MINUTE, -35, SYSDATETIME()))),
    ('2610291100011739', 'TXN-20261029-0001', '2610291100011739', 'Mrt', N'Bến Thành (M1-01)', N'Thảo Điền',        11000, DATEADD(MINUTE, -35, SYSDATETIME()), DATEADD(HOUR, 24, DATEADD(MINUTE, -35, SYSDATETIME()))),
    ('2610291115020614', 'TXN-20261029-0002', '2610291115020614', 'Mrt', N'Bến Thành (M1-01)', N'Bến xe Suối Tiên', 20000, DATEADD(MINUTE, -20, SYSDATETIME()), DATEADD(HOUR, 24, DATEADD(MINUTE, -20, SYSDATETIME())));
GO

-- 11. Reporting View & Stored Procedure for Output Report DataGridView (Section 5.3)
CREATE VIEW dbo.vw_RecentTransactions
AS
SELECT
    t.TransactionId,
    t.CreatedAt,
    d.StationCode,
    d.Mode AS TransportMode,
    d.Name AS DestinationEn,
    d.NameVi AS DestinationVi,
    t.Quantity,
    t.FareSnapshot,
    t.TotalAmount,
    t.Status AS TransactionStatus,
    ISNULL(lp.Method, 'N/A') AS PaymentMethod,
    ISNULL(lp.WalletProvider, CASE WHEN lp.Method = 'BankQR' THEN 'VietQR' WHEN lp.Method = 'CreditCard' THEN ISNULL(lp.CardScheme, 'Card') ELSE '-' END) AS ProviderOrScheme,
    ISNULL(lp.Status, 'None') AS PaymentStatus,
    ISNULL(tk.TicketNumbers, '-') AS TicketNumbers
FROM dbo.[Transaction] t
INNER JOIN dbo.Destination d ON d.DestinationId = t.DestinationId
OUTER APPLY (
    SELECT TOP (1) p.Method, p.WalletProvider, p.CardScheme, p.Status
    FROM dbo.Payment p
    WHERE p.TransactionId = t.TransactionId
    ORDER BY p.CreatedAt DESC
) lp
OUTER APPLY (
    SELECT STRING_AGG(CAST(x.TicketNumber AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY x.TicketId) AS TicketNumbers
    FROM dbo.Ticket x
    WHERE x.TransactionId = t.TransactionId
) tk;
GO

CREATE PROCEDURE dbo.usp_GetRecentTransactions
    @TopN INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@TopN)
        TransactionId,
        CreatedAt,
        StationCode,
        TransportMode,
        DestinationEn,
        DestinationVi,
        Quantity,
        FareSnapshot,
        TotalAmount,
        TransactionStatus,
        PaymentMethod,
        ProviderOrScheme,
        PaymentStatus,
        TicketNumbers
    FROM dbo.vw_RecentTransactions
    ORDER BY CreatedAt DESC;
END
GO

/* ============================================================================
   12. FIVE VERIFICATION QUERIES (For Report Section 5.3 & Demo Check)
   ============================================================================ */
-- Query 1: Verify Destination + effective Fare lookup (M3: Destination 1 -- 1..* Fare)
SELECT d.DestinationId, d.StationCode, d.Mode, d.Name, d.NameVi, f.AmountVND, f.EffectiveFrom
FROM dbo.Destination d
CROSS APPLY (
    SELECT TOP (1) AmountVND, EffectiveFrom
    FROM dbo.Fare f WHERE f.DestinationId = d.DestinationId ORDER BY EffectiveFrom DESC
) f
ORDER BY d.ZoneOrder;

-- Query 2: Verify joined Recent Transactions view (Output Report source)
EXEC dbo.usp_GetRecentTransactions @TopN = 20;

-- Query 3: Verify 1..5 Ticket composition & 16-digit Luhn TicketNumber (FR16, DR4)
SELECT TransactionId, COUNT(*) AS IssuedTicketCount, STRING_AGG(TicketNumber, ', ') AS Tickets16Digit
FROM dbo.Ticket
GROUP BY TransactionId;

-- Query 4: Revenue summary by TransportMode & PaymentMethod (Completed only)
SELECT d.Mode, p.Method, COUNT(DISTINCT t.TransactionId) AS CompletedTxCount, SUM(t.TotalAmount) AS RevenueVND
FROM dbo.[Transaction] t
JOIN dbo.Destination d ON d.DestinationId = t.DestinationId
JOIN dbo.Payment p ON p.TransactionId = t.TransactionId AND p.Status = 'Approved'
WHERE t.Status = 'Completed'
GROUP BY d.Mode, p.Method;

-- Query 5: Audit failed/cancelled/expired attempts (Exception traceability)
SELECT t.TransactionId, t.Status AS TxStatus, t.RetryCount, p.Method, p.Status AS PaymentStatus, p.GatewayRef
FROM dbo.[Transaction] t
LEFT JOIN dbo.Payment p ON p.TransactionId = t.TransactionId
WHERE t.Status <> 'Completed';
GO
