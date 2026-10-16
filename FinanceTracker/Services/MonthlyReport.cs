using FinanceTracker.Models;

namespace FinanceTracker.Services;

/// <summary>Income/expense totals for a single calendar month.</summary>
public sealed record MonthlyReport(
    int Year,
    int Month,
    decimal TotalIncome,
    decimal TotalExpenses,
    IReadOnlyDictionary<Category, decimal> ExpensesByCategory)
{
    public decimal Net => TotalIncome - TotalExpenses;
}
