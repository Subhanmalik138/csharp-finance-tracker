using FinanceTracker.Models;
using FinanceTracker.Storage;

namespace FinanceTracker.Services;

/// <summary>
/// Read-only reporting over the store. Kept separate from the CLI so the
/// math is unit-testable without touching argument parsing or the console.
/// </summary>
public sealed class ReportService
{
    private readonly ITransactionStore _store;

    public ReportService(ITransactionStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public MonthlyReport MonthlyReport(int year, int month)
    {
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be between 1 and 12.");

        var inMonth = _store.Transactions
            .Where(t => t.Date.Year == year && t.Date.Month == month)
            .ToList();

        var income = inMonth.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var expenses = inMonth.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
        var byCategory = inMonth
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        return new MonthlyReport(year, month, income, expenses, byCategory);
    }

    public IReadOnlyList<BudgetStatus> BudgetStatus(int year, int month)
    {
        var report = MonthlyReport(year, month);

        return _store.Budgets
            .Select(b =>
            {
                report.ExpensesByCategory.TryGetValue(b.Category, out var spent);
                return new BudgetStatus(
                    b.Category,
                    b.MonthlyLimit,
                    spent,
                    b.MonthlyLimit - spent,
                    spent > b.MonthlyLimit);
            })
            .OrderBy(s => s.Category.ToString())
            .ToList()
            .AsReadOnly();
    }
}
