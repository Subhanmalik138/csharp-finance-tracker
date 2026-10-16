using FinanceTracker.Models;
using FinanceTracker.Services;
using FinanceTracker.Storage;

namespace FinanceTracker.Tests;

/// <summary>In-memory <see cref="ITransactionStore"/> so reporting math can be
/// tested without touching the file system.</summary>
public sealed class FakeTransactionStore : ITransactionStore
{
    private readonly List<Transaction> _transactions = new();
    private readonly List<Budget> _budgets = new();

    public IReadOnlyList<Transaction> Transactions => _transactions;
    public IReadOnlyList<Budget> Budgets => _budgets;

    public void AddTransaction(Transaction transaction) => _transactions.Add(transaction);

    public bool RemoveTransaction(Guid id)
    {
        var tx = _transactions.FirstOrDefault(t => t.Id == id);
        if (tx is null)
            return false;
        _transactions.Remove(tx);
        return true;
    }

    public void SetBudget(Budget budget)
    {
        _budgets.RemoveAll(b => b.Category == budget.Category);
        _budgets.Add(budget);
    }

    public void Save()
    {
    }
}

public sealed class ReportServiceTests
{
    private static Transaction Tx(int year, int month, int day, decimal amount, TransactionType type, Category category) =>
        Transaction.Create(new DateOnly(year, month, day), "test", amount, type, category);

    [Fact]
    public void MonthlyReport_ComputesIncomeExpensesAndNet()
    {
        var store = new FakeTransactionStore();
        store.AddTransaction(Tx(2026, 10, 1, 2500m, TransactionType.Income, Category.Salary));
        store.AddTransaction(Tx(2026, 10, 2, 300m, TransactionType.Expense, Category.Food));
        store.AddTransaction(Tx(2026, 10, 3, 200m, TransactionType.Expense, Category.Transport));

        var report = new ReportService(store).MonthlyReport(2026, 10);

        Assert.Equal(2500m, report.TotalIncome);
        Assert.Equal(500m, report.TotalExpenses);
        Assert.Equal(2000m, report.Net);
    }

    [Fact]
    public void MonthlyReport_GroupsExpensesByCategory()
    {
        var store = new FakeTransactionStore();
        store.AddTransaction(Tx(2026, 10, 1, 10m, TransactionType.Expense, Category.Food));
        store.AddTransaction(Tx(2026, 10, 2, 20m, TransactionType.Expense, Category.Food));
        store.AddTransaction(Tx(2026, 10, 3, 50m, TransactionType.Expense, Category.Transport));

        var report = new ReportService(store).MonthlyReport(2026, 10);

        Assert.Equal(30m, report.ExpensesByCategory[Category.Food]);
        Assert.Equal(50m, report.ExpensesByCategory[Category.Transport]);
    }

    [Fact]
    public void MonthlyReport_IgnoresTransactionsFromOtherMonths()
    {
        var store = new FakeTransactionStore();
        store.AddTransaction(Tx(2026, 9, 30, 999m, TransactionType.Expense, Category.Food));
        store.AddTransaction(Tx(2026, 10, 1, 10m, TransactionType.Expense, Category.Food));

        var report = new ReportService(store).MonthlyReport(2026, 10);

        Assert.Equal(10m, report.TotalExpenses);
    }

    [Fact]
    public void MonthlyReport_InvalidMonth_Throws()
    {
        var store = new FakeTransactionStore();

        Assert.Throws<ArgumentOutOfRangeException>(() => new ReportService(store).MonthlyReport(2026, 13));
    }

    [Fact]
    public void BudgetStatus_FlagsOverBudgetAndComputesRemaining()
    {
        var store = new FakeTransactionStore();
        store.AddTransaction(Tx(2026, 10, 1, 350m, TransactionType.Expense, Category.Food));
        store.AddTransaction(Tx(2026, 10, 2, 80m, TransactionType.Expense, Category.Transport));
        store.SetBudget(Budget.Create(Category.Food, 300m));
        store.SetBudget(Budget.Create(Category.Transport, 200m));

        var statuses = new ReportService(store).BudgetStatus(2026, 10);

        var food = Assert.Single(statuses, s => s.Category == Category.Food);
        Assert.True(food.OverBudget);
        Assert.Equal(350m, food.Spent);
        Assert.Equal(-50m, food.Remaining);

        var transport = Assert.Single(statuses, s => s.Category == Category.Transport);
        Assert.False(transport.OverBudget);
        Assert.Equal(80m, transport.Spent);
        Assert.Equal(120m, transport.Remaining);
    }

    [Fact]
    public void BudgetStatus_CategoryWithNoSpending_ShowsZeroSpent()
    {
        var store = new FakeTransactionStore();
        store.SetBudget(Budget.Create(Category.Health, 100m));

        var status = Assert.Single(new ReportService(store).BudgetStatus(2026, 10));

        Assert.Equal(0m, status.Spent);
        Assert.False(status.OverBudget);
        Assert.Equal(100m, status.Remaining);
    }
}
