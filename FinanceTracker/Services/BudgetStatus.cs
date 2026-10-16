using FinanceTracker.Models;

namespace FinanceTracker.Services;

/// <summary>How a category budget is tracking for a given month.</summary>
public sealed record BudgetStatus(
    Category Category,
    decimal Limit,
    decimal Spent,
    decimal Remaining,
    bool OverBudget);
