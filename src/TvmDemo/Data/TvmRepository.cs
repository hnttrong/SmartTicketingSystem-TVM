using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TvmDemo.Models;

namespace TvmDemo.Data;

/// <summary>
/// Configuration model bound from <c>appsettings.json</c>.
/// </summary>
public class AppSettingsConfig
{
    public string ActiveProfile { get; set; } = "LocalDb";
    public string MachineCode { get; set; } = "TVM-BT-01";
    public string OriginStationEn { get; set; } = "Ben Thanh Station";
    public string OriginStationVi { get; set; } = "Ga Bến Thành";
    public Dictionary<string, string> ConnectionStrings { get; set; } = new()
    {
        ["LocalDb"] = @"Server=(localdb)\MSSQLLocalDB;Database=TvmDemo;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=4;",
        ["SqlExpress"] = @"Server=.\SQLEXPRESS;Database=TvmDemo;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=4;",
        ["Localhost"] = @"Server=localhost;Database=TvmDemo;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=4;",
        ["InvalidDbTest"] = @"Server=(localdb)\NON_EXISTENT_INSTANCE;Database=TvmDemo;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=2;"
    };

    public static AppSettingsConfig Load()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<AppSettingsConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (cfg != null && cfg.ConnectionStrings.Count > 0)
                {
                    return cfg;
                }
            }
        }
        catch
        {
            // Fall back to safe built-in defaults
        }

        return new AppSettingsConfig();
    }

    public string GetActiveConnectionString() =>
        ConnectionStrings.TryGetValue(ActiveProfile, out string? cs) && !string.IsNullOrWhiteSpace(cs)
            ? cs
            : ConnectionStrings.Values.First();
}

/// <summary>
/// Data access contract for the TVM persistence layer (aligned with Member M3's TransactionRepository &amp; Domain Model).
/// </summary>
public interface ITvmRepository
{
    string ConnectionString { get; set; }
    Task<(bool IsOnline, string? ErrorMessage)> CheckDatabaseHealthAsync();
    Task<List<Destination>> GetActiveDestinationsAsync();
    Task<int> CreatePendingTransactionAsync(Transaction transaction);
    Task CompleteTransactionWithTicketsAsync(Transaction transaction, Payment payment, IReadOnlyList<Ticket> tickets);
    Task FailOrCancelTransactionAsync(Transaction transaction, Payment? payment, TransactionStatus finalStatus, string reason);
    Task<List<TransactionReportRow>> GetRecentTransactionsAsync(int topN = 25, string? statusFilter = null);
}

/// <summary>
/// Microsoft SQL Server implementation of <see cref="ITvmRepository"/> using <c>Microsoft.Data.SqlClient</c>
/// with parameterized SQL queries against the M3-aligned <c>TvmDemo</c> relational schema
/// (<c>Destination</c>, <c>Fare</c>, <c>[Transaction]</c>, <c>Payment</c>, <c>Ticket</c>, <c>Refund</c>).
/// </summary>
public class SqlTvmRepository : ITvmRepository
{
    public string ConnectionString { get; set; }

    public SqlTvmRepository(string connectionString)
    {
        ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<(bool IsOnline, string? ErrorMessage)> CheckDatabaseHealthAsync()
    {
        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.Destination WHERE IsActive = 1;", conn);
            object? scalar = await cmd.ExecuteScalarAsync();
            int count = Convert.ToInt32(scalar ?? 0);
            return count > 0
                ? (true, null)
                : (false, "Database connected, but dbo.Destination contains 0 active stations. Run db/TvmDemo_Setup.sql first.");
        }
        catch (SqlException ex)
        {
            return (false, $"SQL Error #{ex.Number}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Database connection error: {ex.Message}");
        }
    }

    public async Task<List<Destination>> GetActiveDestinationsAsync()
    {
        const string sql = @"
            SELECT d.DestinationId, d.StationCode, d.Name, d.NameVi,
                   d.Mode, d.ZoneOrder, f.AmountVND, d.TravelMinutes, d.IsActive
            FROM dbo.Destination d
            CROSS APPLY (
                SELECT TOP (1) AmountVND
                FROM dbo.Fare f
                WHERE f.DestinationId = d.DestinationId
                ORDER BY f.EffectiveFrom DESC
            ) f
            WHERE d.IsActive = @IsActive
            ORDER BY d.ZoneOrder ASC;";

        var list = new List<Destination>();
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = true });

        await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string mode = reader.GetString(4);
            int zone = reader.GetInt32(5);
            list.Add(new Destination
            {
                DestinationId = reader.GetInt32(0),
                StationCode = reader.GetString(1),
                StationNameEn = reader.GetString(2),
                StationNameVi = reader.GetString(3),
                LineType = mode.Equals("Bus", StringComparison.OrdinalIgnoreCase) ? "BUS" : "MRT",
                ZoneNumber = zone,
                DistanceKm = Math.Round(zone * 1.6m, 1),
                UnitFareVnd = reader.GetDecimal(6),
                EstimatedMinutes = reader.GetInt32(7),
                IsActive = reader.GetBoolean(8)
            });
        }

        return list;
    }

    public async Task<int> CreatePendingTransactionAsync(Transaction transaction)
    {
        const string sql = @"
            INSERT INTO dbo.[Transaction]
                (TransactionId, DestinationId, Quantity, FareSnapshot, TotalAmount, RetryCount, QrRegenCount, Status, GatewayRef, MachineCode)
            VALUES
                (@TransactionId, @DestinationId, @Quantity, @FareSnapshot, @TotalAmount, 0, 0, 'AwaitingPayment', NULL, @MachineCode);";

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add(new SqlParameter("@TransactionId", SqlDbType.VarChar, 36) { Value = transaction.TransactionCode });
        cmd.Parameters.Add(new SqlParameter("@DestinationId", SqlDbType.Int) { Value = transaction.DestinationId });
        cmd.Parameters.Add(new SqlParameter("@Quantity", SqlDbType.Int) { Value = transaction.Quantity });
        cmd.Parameters.Add(new SqlParameter("@FareSnapshot", SqlDbType.Decimal) { Precision = 12, Scale = 0, Value = transaction.UnitFareVnd });
        cmd.Parameters.Add(new SqlParameter("@TotalAmount", SqlDbType.Decimal) { Precision = 12, Scale = 0, Value = transaction.TotalAmountVnd });
        cmd.Parameters.Add(new SqlParameter("@MachineCode", SqlDbType.VarChar, 20) { Value = transaction.MachineCode });

        await cmd.ExecuteNonQueryAsync();
        transaction.TransactionId = Math.Abs(transaction.TransactionCode.GetHashCode());
        return transaction.TransactionId;
    }

    public async Task CompleteTransactionWithTicketsAsync(
        Transaction transaction,
        Payment payment,
        IReadOnlyList<Ticket> tickets)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using SqlTransaction sqlTx = (SqlTransaction)await conn.BeginTransactionAsync();

        try
        {
            string payId = $"PAY-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(10, 99)}";
            bool isCard = transaction.PaymentMode == PaymentModeType.CreditCard;
            string dbType = isCard ? "CardPayment" : "QRPayment";
            string? walletProv = transaction.PaymentMode == PaymentModeType.DigitalWallet
                ? NormalizeWallet(payment.ProviderName)
                : null;

            const string insertPaySql = @"
                INSERT INTO dbo.Payment
                    (PaymentId, TransactionId, PaymentType, Method, Amount, Status, GatewayRef,
                     CardScheme, Last4, GatewayToken, QrContent, ExpiresAt, WalletProvider)
                VALUES
                    (@PaymentId, @TransactionId, @PaymentType, @Method, @Amount, 'Approved', @GatewayRef,
                     @CardScheme, @Last4, @GatewayToken, @QrContent, @ExpiresAt, @WalletProvider);";

            await using (var payCmd = new SqlCommand(insertPaySql, conn, sqlTx))
            {
                payCmd.Parameters.Add(new SqlParameter("@PaymentId", SqlDbType.VarChar, 36) { Value = payId });
                payCmd.Parameters.Add(new SqlParameter("@TransactionId", SqlDbType.VarChar, 36) { Value = transaction.TransactionCode });
                payCmd.Parameters.Add(new SqlParameter("@PaymentType", SqlDbType.VarChar, 20) { Value = dbType });
                payCmd.Parameters.Add(new SqlParameter("@Method", SqlDbType.VarChar, 20) { Value = transaction.PaymentMode.ToString() });
                payCmd.Parameters.Add(new SqlParameter("@Amount", SqlDbType.Decimal) { Precision = 12, Scale = 0, Value = payment.AmountVnd });
                payCmd.Parameters.Add(new SqlParameter("@GatewayRef", SqlDbType.VarChar, 64) { Value = (object?)payment.GatewayReference ?? DBNull.Value });
                payCmd.Parameters.Add(new SqlParameter("@CardScheme", SqlDbType.VarChar, 20) { Value = isCard ? "VISA" : DBNull.Value });
                payCmd.Parameters.Add(new SqlParameter("@Last4", SqlDbType.Char, 4) { Value = isCard ? "4242" : DBNull.Value });
                payCmd.Parameters.Add(new SqlParameter("@GatewayToken", SqlDbType.VarChar, 80) { Value = isCard ? $"TOK-{payId}" : DBNull.Value });
                payCmd.Parameters.Add(new SqlParameter("@QrContent", SqlDbType.NVarChar, 512) { Value = !isCard ? payment.BuildPaymentInstruction(transaction.TransactionCode) : DBNull.Value });
                payCmd.Parameters.Add(new SqlParameter("@ExpiresAt", SqlDbType.DateTime2) { Value = !isCard ? DateTime.Now.AddSeconds(120) : DBNull.Value });
                payCmd.Parameters.Add(new SqlParameter("@WalletProvider", SqlDbType.VarChar, 20) { Value = (object?)walletProv ?? DBNull.Value });
                await payCmd.ExecuteNonQueryAsync();
            }

            const string insertTicketSql = @"
                INSERT INTO dbo.Ticket
                    (TicketNumber, TransactionId, Barcode, Mode, Origin, Destination, Fare, IssuedAt, ValidUntil)
                OUTPUT INSERTED.TicketId
                VALUES
                    (@TicketNumber, @TransactionId, @Barcode, @Mode, @Origin, @Destination, @Fare, @IssuedAt, @ValidUntil);";

            string modeStr = (transaction.Destination?.LineType == "BUS") ? "Bus" : "Mrt";
            string destName = transaction.Destination?.StationNameVi ?? "Ga Metro";

            foreach (Ticket t in tickets)
            {
                await using var tktCmd = new SqlCommand(insertTicketSql, conn, sqlTx);
                tktCmd.Parameters.Add(new SqlParameter("@TicketNumber", SqlDbType.Char, 16) { Value = t.TicketNumber });
                tktCmd.Parameters.Add(new SqlParameter("@TransactionId", SqlDbType.VarChar, 36) { Value = transaction.TransactionCode });
                tktCmd.Parameters.Add(new SqlParameter("@Barcode", SqlDbType.VarChar, 64) { Value = t.BarcodeValue });
                tktCmd.Parameters.Add(new SqlParameter("@Mode", SqlDbType.VarChar, 10) { Value = modeStr });
                tktCmd.Parameters.Add(new SqlParameter("@Origin", SqlDbType.NVarChar, 100) { Value = t.OriginStation });
                tktCmd.Parameters.Add(new SqlParameter("@Destination", SqlDbType.NVarChar, 100) { Value = destName });
                tktCmd.Parameters.Add(new SqlParameter("@Fare", SqlDbType.Decimal) { Precision = 12, Scale = 0, Value = transaction.UnitFareVnd });
                tktCmd.Parameters.Add(new SqlParameter("@IssuedAt", SqlDbType.DateTime2) { Value = t.IssuedAt });
                tktCmd.Parameters.Add(new SqlParameter("@ValidUntil", SqlDbType.DateTime2) { Value = t.ValidUntil });

                t.TicketId = Convert.ToInt32(await tktCmd.ExecuteScalarAsync());
            }

            const string updateTxnSql = @"
                UPDATE dbo.[Transaction]
                SET Status = 'Completed',
                    GatewayRef = @GatewayRef,
                    UpdatedAt = SYSDATETIME()
                WHERE TransactionId = @TransactionId;";

            await using (var updCmd = new SqlCommand(updateTxnSql, conn, sqlTx))
            {
                updCmd.Parameters.Add(new SqlParameter("@GatewayRef", SqlDbType.VarChar, 64) { Value = (object?)payment.GatewayReference ?? DBNull.Value });
                updCmd.Parameters.Add(new SqlParameter("@TransactionId", SqlDbType.VarChar, 36) { Value = transaction.TransactionCode });
                await updCmd.ExecuteNonQueryAsync();
            }

            await sqlTx.CommitAsync();
        }
        catch
        {
            await sqlTx.RollbackAsync();
            throw;
        }
    }

    public async Task FailOrCancelTransactionAsync(
        Transaction transaction,
        Payment? payment,
        TransactionStatus finalStatus,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(transaction.TransactionCode))
        {
            return;
        }

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using SqlTransaction sqlTx = (SqlTransaction)await conn.BeginTransactionAsync();

        try
        {
            if (payment != null)
            {
                string payId = $"PAY-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(10, 99)}";
                bool isCard = transaction.PaymentMode == PaymentModeType.CreditCard;
                string dbType = isCard ? "CardPayment" : "QRPayment";
                string payStatus = payment.PaymentStatus switch
                {
                    PaymentResultStatus.Declined => "Declined",
                    PaymentResultStatus.Timeout => "Expired",
                    PaymentResultStatus.Cancelled => "Cancelled",
                    _ => "Declined"
                };
                string? walletProv = transaction.PaymentMode == PaymentModeType.DigitalWallet
                    ? NormalizeWallet(payment.ProviderName)
                    : null;

                const string insertPaySql = @"
                    INSERT INTO dbo.Payment
                        (PaymentId, TransactionId, PaymentType, Method, Amount, Status, GatewayRef, WalletProvider)
                    VALUES
                        (@PaymentId, @TransactionId, @PaymentType, @Method, @Amount, @Status, @GatewayRef, @WalletProvider);";

                await using var payCmd = new SqlCommand(insertPaySql, conn, sqlTx);
                payCmd.Parameters.Add(new SqlParameter("@PaymentId", SqlDbType.VarChar, 36) { Value = payId });
                payCmd.Parameters.Add(new SqlParameter("@TransactionId", SqlDbType.VarChar, 36) { Value = transaction.TransactionCode });
                payCmd.Parameters.Add(new SqlParameter("@PaymentType", SqlDbType.VarChar, 20) { Value = dbType });
                payCmd.Parameters.Add(new SqlParameter("@Method", SqlDbType.VarChar, 20) { Value = transaction.PaymentMode.ToString() });
                payCmd.Parameters.Add(new SqlParameter("@Amount", SqlDbType.Decimal) { Precision = 12, Scale = 0, Value = payment.AmountVnd });
                payCmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.VarChar, 20) { Value = payStatus });
                payCmd.Parameters.Add(new SqlParameter("@GatewayRef", SqlDbType.VarChar, 64) { Value = (object?)payment.GatewayReference ?? DBNull.Value });
                payCmd.Parameters.Add(new SqlParameter("@WalletProvider", SqlDbType.VarChar, 20) { Value = (object?)walletProv ?? DBNull.Value });
                await payCmd.ExecuteNonQueryAsync();
            }

            string dbTxStatus = finalStatus == TransactionStatus.Cancelled ? "Cancelled" : "Failed";
            const string updateTxnSql = @"
                UPDATE dbo.[Transaction]
                SET Status = @Status,
                    RetryCount = CASE WHEN @Status = 'Failed' AND RetryCount < 3 THEN RetryCount + 1 ELSE RetryCount END,
                    UpdatedAt = SYSDATETIME()
                WHERE TransactionId = @TransactionId;";

            await using (var updCmd = new SqlCommand(updateTxnSql, conn, sqlTx))
            {
                updCmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.VarChar, 25) { Value = dbTxStatus });
                updCmd.Parameters.Add(new SqlParameter("@TransactionId", SqlDbType.VarChar, 36) { Value = transaction.TransactionCode });
                await updCmd.ExecuteNonQueryAsync();
            }

            await sqlTx.CommitAsync();
        }
        catch
        {
            await sqlTx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<TransactionReportRow>> GetRecentTransactionsAsync(int topN = 25, string? statusFilter = null)
    {
        var rows = new List<TransactionReportRow>();
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        const string sql = @"
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
            WHERE (@Filter IS NULL OR TransactionStatus = @Filter)
            ORDER BY CreatedAt DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@TopN", SqlDbType.Int) { Value = topN });
        cmd.Parameters.Add(new SqlParameter("@Filter", SqlDbType.VarChar, 25)
        {
            Value = string.IsNullOrWhiteSpace(statusFilter) || statusFilter == "ALL"
                ? DBNull.Value
                : statusFilter
        });

        int seq = 1;
        await using SqlDataReader r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            string ticketNums = r.GetString(13);
            int issuedCnt = ticketNums == "-" ? 0 : ticketNums.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
            rows.Add(new TransactionReportRow
            {
                TransactionId = seq++,
                TransactionCode = r.GetString(0),
                CreatedAt = r.GetDateTime(1),
                StationCode = $"{r.GetString(2)} ({r.GetString(3)})",
                StationNameEn = r.GetString(4),
                StationNameVi = r.GetString(5),
                Quantity = r.GetInt32(6),
                UnitFareVnd = r.GetDecimal(7),
                TotalAmountVnd = r.GetDecimal(8),
                TransactionStatus = r.GetString(9),
                PaymentMode = r.GetString(10),
                ProviderName = r.GetString(11),
                PaymentStatus = r.GetString(12),
                TicketNumbers = ticketNums,
                PrimaryBarcode = issuedCnt > 0 ? ticketNums.Split(',')[0].Trim() : "-",
                TicketsIssuedCount = issuedCnt,
                Notes = r.GetString(3).Equals("Bus", StringComparison.OrdinalIgnoreCase) ? "Bus Feeder" : "Metro Line 1"
            });
        }

        return rows;
    }

    private static string NormalizeWallet(string? p)
    {
        if (string.IsNullOrWhiteSpace(p)) return "Momo";
        if (p.Contains("Zalo", StringComparison.OrdinalIgnoreCase)) return "ZaloPay";
        if (p.Contains("VNPay", StringComparison.OrdinalIgnoreCase)) return "VNPay";
        return "Momo";
    }
}
