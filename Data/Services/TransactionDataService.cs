using BudgetApp.Data.Interfaces;
using BudgetApp.Models;
using Microsoft.EntityFrameworkCore;
// ReSharper disable All

namespace BudgetApp.Data.Services;

public class TransactionDataService : ITransactionDataService
{
    private readonly IDbContextFactory<BudgetDbContext> dbFactory;

    public TransactionDataService(IDbContextFactory<BudgetDbContext> dbFactory)
    {
        this.dbFactory = dbFactory;
    }

    public async Task<int> StoreTransactionsAsync(List<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        await using var db = await this.dbFactory.CreateDbContextAsync(cancellationToken);

        var dates = transactions.Select(t => t.Date).Distinct().ToList();

        var existingKeys = (await db.Transactions.Where(t => dates.Contains(t.Date))
                .Select(t => new { t.Date, t.Amount, t.Account, t.EntryType })
                .ToListAsync(cancellationToken))
            .Select(t => (t.Date, t.Amount, t.Account, t.EntryType)).ToHashSet();

        var newTransactions = transactions
            .Where(t => !existingKeys.Contains((t.Date, t.Amount, t.Account, t.EntryType)))
            .ToList();
        
        db.Transactions.AddRange(newTransactions);
        await db.SaveChangesAsync(cancellationToken);
        
        return newTransactions.Count;
    }

    public async Task<MonthSummary> GetMonthSummaryAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        await using var db = await this.dbFactory.CreateDbContextAsync(cancellationToken);

        // Same "yyyy-MM" key the import stores in Transaction.Month.
        var monthKey = $"{year}-{month:D2}";

        var rows = await db.Transactions
            .Where(t => t.Month == monthKey)
            .Select(t => new { t.EntryType, t.Category, t.Amount })
            .ToListAsync(cancellationToken);

        // SQLite can't Sum decimal columns in SQL, so the month's rows are totalled here.
        // Expenses are positive; negative expenses are refunds and reduce their category.
        var income = rows.Where(r => r.EntryType == "Income").Sum(r => r.Amount);
        var expenseRows = rows.Where(r => r.EntryType == "Expense").ToList();

        var categories = expenseRows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Category) ? "Uncategorised" : r.Category.Trim())
            .Select(g => new CategoryTotal(g.Key, g.Sum(r => r.Amount)))
            .OrderByDescending(c => c.Total)
            .ToList();

        // Money moved into savings is recorded as an expense in the "Savings" category.
        var saved = expenseRows
            .Where(r => string.Equals(r.Category?.Trim(), "Savings", StringComparison.OrdinalIgnoreCase))
            .Sum(r => r.Amount);

        return new MonthSummary(year, month, income, expenseRows.Sum(r => r.Amount), saved, categories, rows.Count);
    }
}