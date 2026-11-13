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
        return app;
    }
}
