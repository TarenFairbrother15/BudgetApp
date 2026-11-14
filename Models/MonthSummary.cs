namespace BudgetApp.Models;

/// <summary>Income, expense and per-category totals for one imported month.</summary>
public record MonthSummary(
    int Year,
    int Month,
    decimal Income,
    decimal Expenses,
    decimal Saved,
    IReadOnlyList<CategoryTotal> ExpenseCategories,
    int TransactionCount)
{
    public decimal Net => Income - Expenses;
}

/// <summary>
/// Net spend for one category. Refunds come in as negative expenses,
/// so a category can total zero or below when more came back than went out.
/// </summary>
public record CategoryTotal(string Category, decimal Total);
