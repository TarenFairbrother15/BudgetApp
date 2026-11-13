namespace BudgetApp.Models;

public class Transaction
{
    public int Id { get; set; }
    public string Account { get; set; } = "";
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string EntryType { get; set; } = "";
    public string Month { get; set; } = "";
}