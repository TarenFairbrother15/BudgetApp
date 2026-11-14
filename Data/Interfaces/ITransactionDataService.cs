using BudgetApp.Models;

namespace BudgetApp.Data.Interfaces;

public interface ITransactionDataService
{
    public Task<int> StoreTransactionsAsync(List<Transaction> transactions,
        CancellationToken cancellationToken = default);
}