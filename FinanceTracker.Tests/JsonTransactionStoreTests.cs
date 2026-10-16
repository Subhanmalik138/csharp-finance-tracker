using FinanceTracker.Models;
using FinanceTracker.Storage;

namespace FinanceTracker.Tests;

public sealed class JsonTransactionStoreTests : IDisposable
{
    private readonly string _filePath;

    public JsonTransactionStoreTests()
    {
        _filePath = Path.Combine(Path.GetTempPath(), $"ft-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }

    private static Transaction Lunch(decimal amount = 12.50m) =>
        Transaction.Create(new DateOnly(2026, 10, 16), "Lunch", amount, TransactionType.Expense, Category.Food);

    [Fact]
    public void MissingFile_StartsEmpty()
    {
        var store = new JsonTransactionStore(_filePath);

        Assert.Empty(store.Transactions);
        Assert.Empty(store.Budgets);
    }

    [Fact]
    public void Save_ThenReload_RoundTripsTransactions()
    {
        var store = new JsonTransactionStore(_filePath);
        var tx = Lunch();
        store.AddTransaction(tx);
        store.Save();

        var reloaded = new JsonTransactionStore(_filePath);
        var found = Assert.Single(reloaded.Transactions);

        Assert.Equal(tx.Id, found.Id);
        Assert.Equal(12.50m, found.Amount);
        Assert.Equal("Lunch", found.Description);
        Assert.Equal(Category.Food, found.Category);
        Assert.Equal(TransactionType.Expense, found.Type);
        Assert.Equal(new DateOnly(2026, 10, 16), found.Date);
    }

    [Fact]
    public void Save_ThenReload_RoundTripsBudgets()
    {
        var store = new JsonTransactionStore(_filePath);
        store.SetBudget(Budget.Create(Category.Food, 300m));
        store.Save();

        var reloaded = new JsonTransactionStore(_filePath);
        var budget = Assert.Single(reloaded.Budgets);

        Assert.Equal(Category.Food, budget.Category);
        Assert.Equal(300m, budget.MonthlyLimit);
    }

    [Fact]
    public void SetBudget_ReplacesExistingBudgetForSameCategory()
    {
        var store = new JsonTransactionStore(_filePath);
        store.SetBudget(Budget.Create(Category.Food, 300m));
        store.SetBudget(Budget.Create(Category.Food, 400m));

        var budget = Assert.Single(store.Budgets);

        Assert.Equal(400m, budget.MonthlyLimit);
    }

    [Fact]
    public void RemoveTransaction_ExistingId_RemovesAndPersists()
    {
        var store = new JsonTransactionStore(_filePath);
        var tx = Lunch();
        store.AddTransaction(tx);
        store.Save();

        Assert.True(store.RemoveTransaction(tx.Id));
        store.Save();

        Assert.Empty(new JsonTransactionStore(_filePath).Transactions);
    }

    [Fact]
    public void RemoveTransaction_UnknownId_ReturnsFalse()
    {
        var store = new JsonTransactionStore(_filePath);

        Assert.False(store.RemoveTransaction(Guid.NewGuid()));
    }

    [Fact]
    public void CorruptFile_ThrowsInvalidDataException()
    {
        File.WriteAllText(_filePath, "{ this is not json");

        Assert.Throws<InvalidDataException>(() => new JsonTransactionStore(_filePath));
    }
}
