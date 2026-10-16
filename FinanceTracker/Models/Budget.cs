namespace FinanceTracker.Models;

/// <summary>A monthly spending cap for one category.</summary>
public sealed record Budget
{
    public Category Category { get; init; }
    public decimal MonthlyLimit { get; init; }

    public static Budget Create(Category category, decimal monthlyLimit)
    {
        if (!Enum.IsDefined(category))
            throw new ArgumentException($"Unknown category: {category}.", nameof(category));
        if (monthlyLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyLimit), monthlyLimit, "Monthly limit must be greater than zero.");

        return new Budget
        {
            Category = category,
            MonthlyLimit = decimal.Round(monthlyLimit, 2),
        };
    }
}
