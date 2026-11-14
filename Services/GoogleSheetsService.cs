using System.Globalization;
using BudgetApp.Data.Interfaces;
using BudgetApp.Interfaces;
using BudgetApp.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;

// ReSharper disable All

namespace BudgetApp.Services;

public class GoogleSheetsService : IGoogleSheetsService
{
    private readonly string[] scopes =
    [
        SheetsService.Scope.SpreadsheetsReadonly,
        DriveService.Scope.DriveMetadataReadonly
    ];

    // Transactions start on row 5 of the Transactions tab.
    private const int FirstDataRow = 5;

    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly IConfiguration config;
    private readonly ILogger<GoogleSheetsService> logger;
    private readonly ITransactionDataService transactionDataService;

    public GoogleSheetsService(IConfiguration config, ILogger<GoogleSheetsService> logger, ITransactionDataService transactionDataService)
    {
        this.config = config;
        this.logger = logger;
        this.transactionDataService = transactionDataService;
    }


    public async Task<int> ImportTransactionsAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var credential = await GetCredentialAsync(cancellationToken);
        var initializer = CreateInitializer(credential);

        using var driveService = new DriveService(initializer);

        var sheetName = $"{new DateTime(year, month, 1):MMMM yyyy} monthly budget";
        var spreadsheetId = await FindSpreadsheetIdAsync(driveService, sheetName, cancellationToken);

        using var sheetsService = new SheetsService(initializer);

        // Ask for raw values: amounts as numbers (not "$54.69") and dates as serial numbers.
        var request = sheetsService.Spreadsheets.Values.Get(spreadsheetId, $"Transactions!A{FirstDataRow}:J");
        request.ValueRenderOption =
            SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
        request.DateTimeRenderOption =
            SpreadsheetsResource.ValuesResource.GetRequest.DateTimeRenderOptionEnum.SERIALNUMBER;

        var response = await request.ExecuteAsync(cancellationToken);

        var monthKey = $"{year}-{month:D2}";
        var transactions = ParseRows(response.Values, monthKey);
        
        var newTransactionsCount = 
            await this.transactionDataService.StoreTransactionsAsync(transactions, cancellationToken);

        return newTransactionsCount;
    }

    private static BaseClientService.Initializer CreateInitializer(UserCredential credential) => new()
    {
        HttpClientInitializer = credential,
        ApplicationName = "BudgetApp"
    };

    private async Task<UserCredential> GetCredentialAsync(CancellationToken cancellationToken = default)
    {
        var secrets = new ClientSecrets
        {
            ClientId = this.config["Google:ClientId"],
            ClientSecret = this.config["Google:ClientSecret"]
        };

        return await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            this.scopes,
            "user",
            cancellationToken,
            new FileDataStore("token-store", fullPath: false)
        );
    }

    private async Task<string> FindSpreadsheetIdAsync(DriveService driveService, string sheetName,
        CancellationToken cancellationToken = default)
    {
        var request = driveService.Files.List();

        request.Q =
            $"name = '{sheetName}' and mimeType = 'application/vnd.google-apps.spreadsheet' and trashed = false";
        request.Fields = "files(id, name)";

        var result = await request.ExecuteAsync(cancellationToken);
        var file = result.Files.SingleOrDefault()
                   ?? throw new InvalidOperationException($"No spreadsheet found named '{sheetName}'.");
        return file.Id;
    }
    
    private List<Transaction> ParseRows(IList<IList<object>>? rows, string monthKey)
    {
        var transactions = new List<Transaction>();
        if (rows is null) return transactions;

        for (var i = 0; i < rows.Count; i++)
        {
            var sheetRow = FirstDataRow + i;
            // Expense side: A=Account, B=Date, C=Amount, D=Description, E=Category
            AddIfPresent(transactions, rows[i], 0, "Expense", monthKey, sheetRow);
            // Income side: F=Account, G=Date, H=Amount, I=Description, J=Category
            AddIfPresent(transactions, rows[i], 5, "Income", monthKey, sheetRow);
        }

        return transactions;
    }

    private void AddIfPresent(
        List<Transaction> transactions, IList<object> row, int offset, string entryType, string monthKey, int sheetRow)
    {
        var account = GetText(row, offset);
        var dateCell = GetCell(row, offset + 1);
        var amountCell = GetCell(row, offset + 2);
        if (string.IsNullOrWhiteSpace(account) || IsBlank(amountCell)) return;

        if (!TryGetDate(dateCell, out var date))
        {
            logger.LogWarning("Skipped {EntryType} on sheet row {Row}: invalid date '{Value}'.",
                entryType, sheetRow, dateCell);
            return;
        }

        if (!TryGetAmount(amountCell, out var amount))
        {
            logger.LogWarning("Skipped {EntryType} on sheet row {Row}: invalid amount '{Value}'.",
                entryType, sheetRow, amountCell);
            return;
        }

        transactions.Add(new Transaction
        {
            Account = account,
            Date = date,
            Amount = amount,
            Description = GetText(row, offset + 3),
            Category = GetText(row, offset + 4),
            EntryType = entryType,
            Month = monthKey
        });
    }

    /// <summary>
    /// Dates can arrive three ways: yyyyMMdd typed as a number (20261103) or as text ("20261103"),
    /// or a real date cell as a serial number (days since 1899-12-30, e.g. 46329).
    /// </summary>
    private static bool TryGetDate(object? cell, out DateOnly date)
    {
        date = default;
        switch (cell)
        {
            case long number when number >= 10_000_000:
                return TryParseYyyyMmDd(number.ToString(CultureInfo.InvariantCulture), out date);
            case long serial:
                return TryFromSerial(serial, out date);
            case double serial:
                return TryFromSerial(serial, out date);
            case string text:
                return TryParseYyyyMmDd(text.Trim(), out date);
            default:
                return false;
        }
    }

    private static bool TryParseYyyyMmDd(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryFromSerial(double serial, out DateOnly date)
    {
        // Sheets serial dates: 1 = 1899-12-31. Anything outside 1900–9999 is not a real date.
        date = default;
        if (serial is < 1 or > 2_958_465) return false;
        date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
        return true;
    }

    /// <summary>
    /// Number cells arrive as numbers; amounts typed as text (e.g. "$54.69") are parsed as US currency.
    /// </summary>
    private static bool TryGetAmount(object? cell, out decimal amount)
    {
        amount = default;
        switch (cell)
        {
            case long whole:
                amount = whole;
                return true;
            case double number:
                amount = Math.Round((decimal)number, 2);
                return true;
            case string text:
                return decimal.TryParse(text.Trim(), NumberStyles.Currency, UsCulture, out amount);
            default:
                return false;
        }
    }

    private static object? GetCell(IList<object> row, int index) =>
        index < row.Count ? row[index] : null;

    private static string GetText(IList<object> row, int index) =>
        Convert.ToString(GetCell(row, index), CultureInfo.InvariantCulture)?.Trim() ?? "";

    private static bool IsBlank(object? cell) =>
        cell is null || (cell is string text && string.IsNullOrWhiteSpace(text));
} 