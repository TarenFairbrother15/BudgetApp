namespace BudgetApp.Interfaces;

public interface IGoogleSheetsService
{
    Task<int> ImportTransactionsAsync(int year, int month, CancellationToken cancellationToken = default);
}