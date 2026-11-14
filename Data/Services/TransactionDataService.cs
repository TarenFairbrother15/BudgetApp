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
                .Select(t => new { t.Date, t.Amount, t.Account })
                .ToListAsync(cancellationToken))
            .Select(t => (t.Date, t.Amount, t.Account)).ToHashSet();
        
        var newTransactions = transactions
            .Where(t => !existingKeys.Contains((t.Date, t.Amount, t.Account)))
            .ToList();
        
        db.Transactions.AddRange(newTransactions);
        await db.SaveChangesAsync(cancellationToken);
        
        return newTransactions.Count;
    }
}