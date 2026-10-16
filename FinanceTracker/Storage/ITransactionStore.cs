using FinanceTracker.Models;

namespace FinanceTracker.Storage;

/// <summary>
/// Persistence boundary for the app. The CLI and reporting code only ever
/// talk to this interface, so tests can substitute an in-memory fake and the
/// storage implementation can change without touching callers.
/// </summary>
public interface ITransactionStore
{
    IReadOnlyList<Transaction> Transactions { get; }
    IReadOnlyList<Budget> Budgets { get; }
    void AddTransaction(Transaction transaction);
    bool RemoveTransaction(Guid id);
    void SetBudget(Budget budget);
    void Save();
}
