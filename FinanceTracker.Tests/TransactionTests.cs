using FinanceTracker.Models;

namespace FinanceTracker.Tests;

public sealed class TransactionTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    [Fact]
    public void Create_ValidInput_StoresPositiveAmountAndAssignsId()
    {
        var tx = Transaction.Create(Today, "Lunch", 12.50m, TransactionType.Expense, Category.Food);

        Assert.NotEqual(Guid.Empty, tx.Id);
        Assert.Equal(12.50m, tx.Amount);
        Assert.Equal("Lunch", tx.Description);
        Assert.Equal(TransactionType.Expense, tx.Type);
        Assert.Equal(Category.Food, tx.Category);
    }

    [Fact]
    public void Create_TrimsDescription()
    {
        var tx = Transaction.Create(Today, "  Lunch  ", 5m, TransactionType.Expense, Category.Food);

        Assert.Equal("Lunch", tx.Description);
    }

    [Fact]
    public void Create_EmptyDescription_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Transaction.Create(Today, "   ", 5m, TransactionType.Expense, Category.Food));
    }

    [Fact]
    public void Create_ZeroOrNegativeAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Transaction.Create(Today, "Lunch", 0m, TransactionType.Expense, Category.Food));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Transaction.Create(Today, "Lunch", -3m, TransactionType.Expense, Category.Food));
    }

    [Fact]
    public void Create_FutureDate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Transaction.Create(Today.AddDays(2), "Lunch", 5m, TransactionType.Expense, Category.Food));
    }

    [Fact]
    public void Create_RoundsAmountToTwoDecimals()
    {
        var tx = Transaction.Create(Today, "Lunch", 12.567m, TransactionType.Expense, Category.Food);

        Assert.Equal(12.57m, tx.Amount);
    }

    [Fact]
    public void SignedAmount_Expense_IsNegative_Income_IsPositive()
    {
        var expense = Transaction.Create(Today, "Lunch", 10m, TransactionType.Expense, Category.Food);
        var income = Transaction.Create(Today, "Pay", 100m, TransactionType.Income, Category.Salary);

        Assert.Equal(-10m, expense.SignedAmount);
        Assert.Equal(100m, income.SignedAmount);
    }

    [Fact]
    public void BudgetCreate_NonPositiveLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Budget.Create(Category.Food, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Budget.Create(Category.Food, -50m));
    }

    [Fact]
    public void BudgetCreate_ValidInput_SetsProperties()
    {
        var budget = Budget.Create(Category.Food, 300m);

        Assert.Equal(Category.Food, budget.Category);
        Assert.Equal(300m, budget.MonthlyLimit);
    }
}
