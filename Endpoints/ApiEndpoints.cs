using BudgetApp.Interfaces;

namespace BudgetApp.Endpoints;

/// <summary>
/// Minimal API endpoints served under /api, alongside the Blazor UI.
/// </summary>
public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api")
            .DisableAntiforgery(); // JSON API, not form posts

        api.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }))
            .WithName("GetHealth");

        api.MapPost("/import/{year:int}/{month:int}", async (
                int year, int month, IGoogleSheetsService sheets, CancellationToken cancellationToken) =>
            {
                if (month is < 1 or > 12)
                    return Results.BadRequest("Month must be between 1 and 12.");

                var count = await sheets.ImportTransactionsAsync(year, month, cancellationToken);
                return Results.Ok(new ImportResult(year, month, count));
            })
            .WithName("ImportTransactions");

        return app;
    }
}

/// <summary>Response body for POST /api/import/{year}/{month}.</summary>
public record ImportResult(int Year, int Month, int TransactionCount);
